using System.Security.Cryptography;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Text;
using SkiaSharp;

namespace Novolis.Pdf.Rendering.Skia;

/// <summary>Skia painter that applies PDF text rendering matrices onto a live canvas.</summary>
public sealed class PdfSkiaPageRenderer : IPdfPageRenderer
{
    private readonly PdfLimits _limits;
    private readonly PdfPagePlanCache _plans = new();
    private readonly Dictionary<string, SKTypeface> _typefaces = new(StringComparer.Ordinal);
    private readonly List<SKData> _fontData = [];
    private readonly List<MemoryStream> _fontPrograms = [];

    /// <summary>Creates a renderer with bounded resource limits.</summary>
    public PdfSkiaPageRenderer(PdfLimits? limits = null)
    {
        _limits = limits ?? PdfLimits.Default;
        _limits.Validate();
    }

    /// <inheritdoc />
    public ValueTask<PdfRenderedPage> RenderAsync(
        PdfParsedDocument document,
        PdfRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var pages = PdfPageTree.Resolve(document, _limits);
        if (request.PageIndex >= pages.Count)
        {
            return ValueTask.FromResult(new PdfRenderedPage(
                request.PageIndex,
                1,
                1,
                EmptyPixelPng(),
                PdfRenderStatus.Failed,
                [
                    new PdfDiagnosticEntry(
                        PdfDiagnosticSeverity.Error,
                        "PDF040",
                        $"Page index {request.PageIndex} is outside the document page range."),
                ]));
        }

        var page = pages[request.PageIndex];
        var plan = _plans.Get(document, page, _limits);
        var native = page.Info;
        var scale = request.DotsPerInch / 72d * System.Math.Max(0.01, native.UserUnit);
        var width = System.Math.Clamp((int)System.Math.Ceiling(native.DisplayWidth * scale), 1, 16_384);
        var height = System.Math.Clamp((int)System.Math.Ceiling(native.DisplayHeight * scale), 1, 16_384);
        var view = PdfViewportTransform.Raster(native.DisplayHeight, request.DotsPerInch * native.UserUnit);
        return ValueTask.FromResult(
            Rasterize(document, page, plan, view, width, height, request.Rotation, cancellationToken));
    }

    /// <summary>Paints a page into an existing canvas using a PDF→device view matrix.</summary>
    public void Paint(
        SKCanvas canvas,
        PdfParsedDocument document,
        PdfResolvedPage page,
        PdfPageRenderPlan plan,
        PdfMatrix view,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(plan);

        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.SetMatrix(PdfSkiaCanvas.ToSkia(view));
        using var paint = new SKPaint { IsAntialias = true };
        foreach (var command in plan.Commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (command)
            {
                case PdfPathCommand path:
                    DrawPath(canvas, paint, path);
                    break;
                case PdfTextCommand text:
                    DrawText(canvas, paint, document, page, text);
                    break;
            }
        }

        canvas.Restore();
    }

    /// <summary>Paints the current page into a viewport-sized bitmap (zoom is a view transform).</summary>
    public PdfRenderedPage RenderViewport(
        PdfParsedDocument document,
        PdfResolvedPage page,
        int viewportWidth,
        int viewportHeight,
        double zoom,
        int rotation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        var plan = _plans.Get(document, page, _limits);
        var native = page.Info;
        var viewRotation = NormalizeRotation(rotation);
        var cellWidth = System.Math.Clamp(viewportWidth, 1, 16_384);
        var cellHeight = System.Math.Clamp(viewportHeight, 1, 16_384);
        var paintWidth = viewRotation is 90 or 270 ? cellHeight : cellWidth;
        var paintHeight = viewRotation is 90 or 270 ? cellWidth : cellHeight;
        var view = PdfViewportTransform.FitPage(
            native.DisplayWidth,
            native.DisplayHeight,
            paintWidth,
            paintHeight,
            zoom);
        return Rasterize(document, page, plan, view, paintWidth, paintHeight, viewRotation, cancellationToken);
    }

    /// <summary>Paints a page into a bitmap using a uniform fit (no X/Y stretch).</summary>
    public PdfRenderedPage RenderScaled(
        PdfParsedDocument document,
        PdfResolvedPage page,
        int pixelWidth,
        int pixelHeight,
        int rotation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        var plan = _plans.Get(document, page, _limits);
        var native = page.Info;
        var viewRotation = NormalizeRotation(rotation);
        var cellWidth = System.Math.Clamp(pixelWidth, 1, 16_384);
        var cellHeight = System.Math.Clamp(pixelHeight, 1, 16_384);
        var paintWidth = viewRotation is 90 or 270 ? cellHeight : cellWidth;
        var paintHeight = viewRotation is 90 or 270 ? cellWidth : cellHeight;
        var view = PdfViewportTransform.FitPage(
            native.DisplayWidth,
            native.DisplayHeight,
            paintWidth,
            paintHeight,
            zoom: 1);
        return Rasterize(document, page, plan, view, paintWidth, paintHeight, viewRotation, cancellationToken);
    }

    private PdfRenderedPage Rasterize(
        PdfParsedDocument document,
        PdfResolvedPage page,
        PdfPageRenderPlan plan,
        PdfMatrix view,
        int width,
        int height,
        int viewRotation,
        CancellationToken cancellationToken)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
            Paint(canvas, document, page, plan, view, cancellationToken);

        using var oriented = Orient(bitmap, viewRotation);
        using var image = SKImage.FromBitmap(oriented);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var status = plan.Diagnostics.Any(static diagnostic => diagnostic.Severity == PdfDiagnosticSeverity.Error)
            ? PdfRenderStatus.Failed
            : plan.Diagnostics.Count > 0 ? PdfRenderStatus.Partial : PdfRenderStatus.Rendered;
        return new PdfRenderedPage(
            page.Info.Index,
            oriented.Width,
            oriented.Height,
            data.ToArray(),
            status,
            plan.Diagnostics);
    }

    private static SKBitmap Orient(SKBitmap source, int rotation)
    {
        var degrees = NormalizeRotation(rotation);
        if (degrees == 0)
            return source.Copy() ?? source;

        var dest = degrees is 90 or 270
            ? new SKBitmap(source.Height, source.Width, SKColorType.Rgba8888, SKAlphaType.Premul)
            : new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(dest);
        canvas.Clear(SKColors.White);
        switch (degrees)
        {
            case 90:
                canvas.Translate(dest.Width, 0);
                canvas.RotateDegrees(90);
                break;
            case 180:
                canvas.Translate(dest.Width, dest.Height);
                canvas.RotateDegrees(180);
                break;
            case 270:
                canvas.Translate(0, dest.Height);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        return dest;
    }

    private static int NormalizeRotation(int rotation) =>
        ((rotation % 360) + 360) % 360;

    private static void DrawPath(SKCanvas canvas, SKPaint paint, PdfPathCommand command)
    {
        using var path = new SKPath();
        for (var index = 0; index < command.Points.Count; index++)
        {
            var point = command.Points[index];
            if (index == 0)
                path.MoveTo((float)point.X, (float)point.Y);
            else
                path.LineTo((float)point.X, (float)point.Y);
        }

        if (command.Closed)
            path.Close();

        if (command.Fill)
        {
            paint.Style = SKPaintStyle.Fill;
            paint.Color = ToColor(command.FillColor);
            canvas.DrawPath(path, paint);
        }

        if (command.Stroke)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = (float)System.Math.Max(0.1, command.StrokeWidth);
            paint.Color = ToColor(command.StrokeColor);
            canvas.DrawPath(path, paint);
        }
    }

    private void DrawText(
        SKCanvas canvas,
        SKPaint paint,
        PdfParsedDocument document,
        PdfResolvedPage page,
        PdfTextCommand command)
    {
        var fontResource = page.Resources is { } resources
            ? new PdfResourceDictionary(document, resources).ResolveFont(command.FontName)
            : null;
        var decoded = PdfFontDecoder.Decode(document, page, command, _limits);
        if (decoded.Text.Length > 0 && string.IsNullOrWhiteSpace(decoded.Text))
            return;

        var typeface = ResolveTypeface(document, command, fontResource);
        using var font = new SKFont(typeface, 1);
        var glyphs = ResolveGlyphs(font, typeface, decoded, command, fontResource);
        if (glyphs.Length == 0)
            return;

        var advances = PdfAdvances(command, fontResource, glyphs.Length);
        paint.Style = SKPaintStyle.Fill;
        paint.Color = ToColor(command.Color);
        var view = PdfSkiaCanvas.FromSkia(canvas.TotalMatrix);
        canvas.Save();
        canvas.SetMatrix(PdfSkiaCanvas.ToSkia(TextBlobMatrix(view, command.TextRenderingMatrix)));
        DrawGlyphs(canvas, paint, font, glyphs, advances);
        canvas.Restore();
    }

    // SKTextBlob is Y-down; Trm+view already include the PDF Y-up flip.
    private static PdfMatrix TextBlobMatrix(PdfMatrix view, PdfMatrix textRenderingMatrix) =>
        view.Multiply(textRenderingMatrix).Multiply(new PdfMatrix(1, 0, 0, -1, 0, 0));

    private static ushort[] ResolveGlyphs(
        SKFont font,
        SKTypeface typeface,
        PdfDecodedText decoded,
        PdfTextCommand command,
        PdfFontResource? fontResource)
    {
        var identity = PdfFontDecoder.TryReadIdentityGlyphs(command, fontResource, out var cids)
            && cids.Length > 0
            && cids.All(glyph => glyph > 0 && glyph < typeface.GlyphCount)
            ? cids
            : [];
        if (identity.Length > 0)
            return identity;

        if (decoded.FromToUnicode
            && !string.IsNullOrEmpty(decoded.Text)
            && !decoded.Text.All(static value => value == '\uFFFD'))
        {
            var mapped = font.GetGlyphs(decoded.Text);
            if (mapped.Length > 0 && !mapped.All(static glyph => glyph == 0))
                return mapped;
        }

        return cids ?? [];
    }

    private static float[] PdfAdvances(PdfTextCommand command, PdfFontResource? font, int glyphCount)
    {
        var raw = command.EncodedBytes ?? [];
        var identity = font is { IsType0: true } || font is { IsIdentityEncoding: true };
        var step = identity ? 2 : 1;
        var advances = new float[System.Math.Max(1, glyphCount)];
        if (font is null || raw.Length < step)
            return advances;

        var x = 0f;
        var index = 0;
        for (var offset = 0; offset + step <= raw.Length && index < advances.Length; offset += step)
        {
            advances[index] = x;
            var code = identity
                ? (raw[offset] << 8) | raw[offset + 1]
                : raw[offset];
            x += (float)(font.WidthForCode(code) / 1000d);
            index++;
        }

        return advances;
    }

    private static void DrawGlyphs(
        SKCanvas canvas,
        SKPaint paint,
        SKFont font,
        ushort[] glyphs,
        float[] xs)
    {
        var positions = xs.Length == glyphs.Length ? xs : new float[glyphs.Length];
        using var builder = new SKTextBlobBuilder();
        builder.AddHorizontalRun(glyphs, font, positions, 0);
        using var blob = builder.Build();
        if (blob is not null)
            canvas.DrawText(blob, 0, 0, paint);
    }

    private SKTypeface ResolveTypeface(
        PdfParsedDocument document,
        PdfTextCommand command,
        PdfFontResource? font)
    {
        var key = TypefaceKey(command, font);
        if (_typefaces.TryGetValue(key, out var cached))
            return cached;

        SKTypeface? typeface = null;
        if (font?.ResolveEmbeddedFontProgram() is { } program)
        {
            var bytes = PdfStreamDecoder.Decode(program, _limits);
            var stream = new MemoryStream(bytes, writable: false);
            _fontPrograms.Add(stream);
            typeface = SKTypeface.FromStream(stream);
            if (typeface is null)
            {
                var data = SKData.CreateCopy(bytes);
                _fontData.Add(data);
                typeface = SKTypeface.FromData(data);
            }
        }

        typeface ??= SKTypeface.FromFamilyName(font?.FamilyName ?? "Times New Roman")
            ?? SKTypeface.Default;
        _typefaces[key] = typeface;
        return typeface;
    }

    private static string TypefaceKey(PdfTextCommand command, PdfFontResource? font)
    {
        if (font?.ResolveEmbeddedFontProgram() is { EncodedBytes.Length: > 0 } program)
        {
            var hash = SHA256.HashData(program.EncodedBytes);
            return $"emb:{program.EncodedBytes.Length}:{Convert.ToHexString(hash.AsSpan(0, 8))}";
        }

        return $"{command.FontName}:{font?.BaseFont?.Value ?? "default"}";
    }

    private static SKColor ToColor(PdfColor color)
    {
        var clamped = color.Clamp();
        return new SKColor(
            (byte)System.Math.Round(clamped.Red * 255),
            (byte)System.Math.Round(clamped.Green * 255),
            (byte)System.Math.Round(clamped.Blue * 255),
            (byte)System.Math.Round(clamped.Alpha * 255));
    }

    private static byte[] EmptyPixelPng()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
