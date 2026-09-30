using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using Novolis.Pdf.Text;
using SkiaSharp;

namespace Novolis.Pdf.Unit;

public sealed class NovolisGeneratedPdfTests
{
    private const string CalypsoExportPath =
        @"C:\Users\frank\Downloads\the-calypso-cycle-calypso (9).pdf";

    [Test]
    public async Task ExtractsUnicodeFromSkiaIdentityHPdf()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildSkiaIdentityPdf("Calypso")),
            "skia-calypso.pdf",
            "skia-calypso");
        var document = await PdfParser.ParseAsync(source);
        var text = string.Concat(new PdfTextExtractor().ExtractDocument(document).Select(static span => span.Text));

        await Assert.That(text).Contains("Calypso");
    }

    [Test]
    public async Task CalypsoPageTwoGlyphOrientationDump()
    {
        if (!File.Exists(CalypsoExportPath))
            return;

        await using var source = PdfSources.FromStream(
            File.OpenRead(CalypsoExportPath),
            Path.GetFileName(CalypsoExportPath),
            CalypsoExportPath);
        var document = await PdfParser.ParseAsync(source);
        var pages = PdfPageTree.Resolve(document);
        if (pages.Count <= 2)
            return;

        var page = pages[2];
        var plan = PdfContentStreamInterpreter.BuildPlan(document, page);
        var commands = plan.Commands.OfType<PdfTextCommand>().Take(40).ToArray();
        var dump = new System.Text.StringBuilder();
        var typefaces = new Dictionary<string, SKTypeface>(StringComparer.Ordinal);
        foreach (var command in commands)
        {
            var fontResource = page.Resources is { } resources
                ? new PdfResourceDictionary(document, resources).ResolveFont(command.FontName)
                : null;
            var decoded = PdfFontDecoder.Decode(document, page, command);
            var typeface = ResolveDumpTypeface(document, command, fontResource, typefaces);
            using var font = new SKFont(typeface, 1);
            PdfFontDecoder.TryReadIdentityGlyphs(command, fontResource, out var identity);
            var mapped = font.GetGlyphs(decoded.Text);
            dump.AppendLine(
                $"uni={decoded.Text} cid=[{string.Join(',', identity)}] get=[{string.Join(',', mapped)}] glyphs={typeface.GlyphCount} font={fontResource?.BaseFont} tm=({command.TextMatrix.A:0.##},{command.TextMatrix.D:0.##},{command.TextMatrix.F:0.##}) trmD={command.TextRenderingMatrix.D:0.##}");
        }

        var dumpPath = Path.Combine(Path.GetTempPath(), "novolis-pdf-glyph-dump.txt");
        File.WriteAllText(dumpPath, dump.ToString());

        var current = await new PdfSkiaPageRenderer().RenderAsync(document, new PdfRenderRequest(2, 96));
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "novolis-pdf-page2-current.png"), current.PngBytes);

        RenderGlyphStrip(
            document,
            page,
            commands,
            typefaces,
            unflip: false,
            identityFirst: false,
            Path.Combine(Path.GetTempPath(), "novolis-pdf-strip-get.png"));
        RenderGlyphStrip(
            document,
            page,
            commands,
            typefaces,
            unflip: true,
            identityFirst: false,
            Path.Combine(Path.GetTempPath(), "novolis-pdf-strip-get-unflip.png"));
        RenderGlyphStrip(
            document,
            page,
            commands,
            typefaces,
            unflip: false,
            identityFirst: true,
            Path.Combine(Path.GetTempPath(), "novolis-pdf-strip-cid.png"));
        RenderGlyphStrip(
            document,
            page,
            commands,
            typefaces,
            unflip: true,
            identityFirst: true,
            Path.Combine(Path.GetTempPath(), "novolis-pdf-strip-cid-unflip.png"));

        await Assert.That(File.Exists(dumpPath)).IsTrue();
    }

    private static SKTypeface ResolveDumpTypeface(
        PdfParsedDocument document,
        PdfTextCommand command,
        PdfFontResource? font,
        Dictionary<string, SKTypeface> cache)
    {
        var key = font?.BaseFont?.Value ?? command.FontName ?? "default";
        if (cache.TryGetValue(key, out var cached))
            return cached;
        SKTypeface? typeface = null;
        if (font?.ResolveEmbeddedFontProgram() is { } program)
        {
            var bytes = PdfStreamDecoder.Decode(program, PdfLimits.Default);
            typeface = SKTypeface.FromStream(new MemoryStream(bytes, writable: false));
        }

        typeface ??= SKTypeface.Default;
        cache[key] = typeface;
        return typeface;
    }

    private static void RenderGlyphStrip(
        PdfParsedDocument document,
        PdfResolvedPage page,
        IReadOnlyList<PdfTextCommand> commands,
        Dictionary<string, SKTypeface> typefaces,
        bool unflip,
        bool identityFirst,
        string path)
    {
        using var bitmap = new SKBitmap(900, 160, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Fill };
        var x = 16f;
        foreach (var command in commands.Take(28))
        {
            var fontResource = page.Resources is { } resources
                ? new PdfResourceDictionary(document, resources).ResolveFont(command.FontName)
                : null;
            var decoded = PdfFontDecoder.Decode(document, page, command);
            var typeface = ResolveDumpTypeface(document, command, fontResource, typefaces);
            using var font = new SKFont(typeface, 36);
            PdfFontDecoder.TryReadIdentityGlyphs(command, fontResource, out var identity);
            var glyphs = identityFirst
                && identity.Length > 0
                && identity.All(glyph => glyph < typeface.GlyphCount)
                ? identity
                : font.GetGlyphs(decoded.Text);
            if (glyphs.Length == 0)
                continue;
            canvas.Save();
            canvas.Translate(x, unflip ? 50 : 110);
            if (unflip)
                canvas.Scale(1, -1);
            using var builder = new SKTextBlobBuilder();
            builder.AddHorizontalRun(glyphs, font, [0f], 0);
            using var blob = builder.Build();
            if (blob is not null)
                canvas.DrawText(blob, 0, 0, paint);
            canvas.Restore();
            x += 28;
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    [Test]
    public async Task CalypsoPageTwoAuthorsNoteMatchesUnicode()
    {
        if (!File.Exists(CalypsoExportPath))
            return;

        await using var source = PdfSources.FromStream(
            File.OpenRead(CalypsoExportPath),
            Path.GetFileName(CalypsoExportPath),
            CalypsoExportPath);
        var document = await PdfParser.ParseAsync(source);
        var pages = PdfPageTree.Resolve(document);
        if (pages.Count <= 2)
            return;

        var pageTwo = string.Concat(
            new PdfTextExtractor().ExtractDocument(document)
                .Where(static span => span.PageIndex == 2)
                .Select(static span => span.Text));
        var rendered = await new PdfSkiaPageRenderer().RenderAsync(
            document,
            new PdfRenderRequest(2, 72));
        var decoded = PdfContentStreamInterpreter.BuildPlan(document, pages[2])
            .Commands
            .OfType<PdfTextCommand>()
            .Select(command => PdfFontDecoder.Decode(document, pages[2], command))
            .Where(static item => item.FromToUnicode)
            .ToArray();

        await Assert.That(pageTwo).Contains("Author's Note");
        await Assert.That(pageTwo.Contains("Author's Notee", StringComparison.Ordinal)).IsFalse();
        await Assert.That(rendered.Status is PdfRenderStatus.Rendered or PdfRenderStatus.Partial).IsTrue();
        await Assert.That(rendered.PngBytes.Length).IsGreaterThan(1000);
        await Assert.That(decoded.Length).IsGreaterThan(0);
        await Assert.That(string.Concat(decoded.Select(static item => item.Text))).Contains("Author");
    }

    [Test]
    public async Task ReadsNovolisExportedCalypsoPdf()
    {
        if (!File.Exists(CalypsoExportPath))
            return;

        await using var source = PdfSources.FromStream(
            File.OpenRead(CalypsoExportPath),
            Path.GetFileName(CalypsoExportPath),
            CalypsoExportPath);
        var document = await PdfParser.ParseAsync(source);
        var extractor = new PdfTextExtractor();
        var documentText = string.Concat(
            extractor.ExtractDocument(document).Select(static span => span.Text));
        var pages = PdfPageTree.Resolve(document);
        var dumpPath = Path.Combine(Path.GetTempPath(), "novolis-pdf-calypso-dump.txt");
        var dump = new System.Text.StringBuilder();
        dump.AppendLine(
            $"catalog-count={document.GetCatalog()?.ResolvePages()?.Dictionary.GetNumber(PdfNames.Count)}");
        dump.AppendLine($"resolved={pages.Count}");
        dump.AppendLine($"extract={documentText[..System.Math.Min(400, documentText.Length)]}");
        var inspectCount = System.Math.Min(5, pages.Count);
        for (var index = 0; index < inspectCount; index++)
        {
            var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[index]);
            var pageRender = await new PdfSkiaPageRenderer().RenderAsync(
                document,
                new PdfRenderRequest(index, 72));
            File.WriteAllBytes(
                Path.Combine(Path.GetTempPath(), $"novolis-pdf-calypso-page{index}.png"),
                pageRender.PngBytes);
            dump.AppendLine($"==== page {index} streams={pages[index].Contents.Count} commands={plan.Commands.Count} ====");
            dump.Append(BuildCalypsoDump(document, pages[index], plan, pageRender, string.Empty));
        }

        File.WriteAllText(dumpPath, dump.ToString());
        var rendered = await new PdfSkiaPageRenderer().RenderAsync(
            document,
            new PdfRenderRequest(0, 72));

        await Assert.That(document.GetCatalog()).IsNotNull();
        await Assert.That(
                documentText.Contains("Calypso", StringComparison.OrdinalIgnoreCase)
                || documentText.Contains("cycle", StringComparison.OrdinalIgnoreCase))
            .IsTrue();
        await Assert.That(rendered.Status is PdfRenderStatus.Rendered or PdfRenderStatus.Partial).IsTrue();
        await Assert.That(rendered.PngBytes.Length).IsGreaterThan(1000);
        if (pages.Count > 2)
        {
            var pageTwo = string.Concat(
                extractor.ExtractDocument(document)
                    .Where(static span => span.PageIndex == 2)
                    .Select(static span => span.Text));
            await Assert.That(pageTwo).Contains("Author");
            var note = await new PdfSkiaPageRenderer().RenderAsync(document, new PdfRenderRequest(2, 72));
            await Assert.That(note.PngBytes.Length).IsGreaterThan(1000);
            var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[2]);
            var decoded = plan.Commands.OfType<PdfTextCommand>()
                .Select(command => PdfFontDecoder.Decode(document, pages[2], command))
                .First(static item => item.FromToUnicode);
            await Assert.That(decoded.FromToUnicode).IsTrue();
            await Assert.That(decoded.Text.Length).IsGreaterThan(0);
        }
    }

    private static byte[] BuildSkiaIdentityPdf(string text)
    {
        using var stream = new MemoryStream();
        using (var pdf = SKDocument.CreatePdf(stream))
        {
            using var canvas = pdf.BeginPage(400, 200);
            using var font = new SKFont(SKTypeface.Default, 28);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText(text, 36, 80, SKTextAlign.Left, font, paint);
            pdf.EndPage();
            pdf.Close();
        }

        return stream.ToArray();
    }

    private static string BuildCalypsoDump(
        PdfParsedDocument document,
        PdfResolvedPage page,
        PdfPageRenderPlan plan,
        PdfRenderedPage rendered,
        string documentText)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"pages={PdfPageTree.Resolve(document).Count}");
        builder.AppendLine($"box={page.Info.CropBox} rotate={page.Info.Rotation} userUnit={page.Info.UserUnit}");
        builder.AppendLine($"png={rendered.PixelWidth}x{rendered.PixelHeight} status={rendered.Status} bytes={rendered.PngBytes.Length}");
        builder.AppendLine($"commands={plan.Commands.Count} diagnostics={plan.Diagnostics.Count}");
        foreach (var diagnostic in plan.Diagnostics.Take(20))
            builder.AppendLine($"diag {diagnostic.Code} {diagnostic.Message}");

        if (page.Resources is { } resources)
        {
            builder.AppendLine("resource-keys=" + string.Join(",", resources.Values.Keys));
            var fonts = document.ResolveDictionary(resources.Get(PdfNames.Font));
            if (fonts is not null)
            {
                foreach (var (name, value) in fonts.Values)
                {
                    var font = document.ResolveDictionary(value);
                    if (font is null)
                        continue;
                    var view = new PdfFontResource(document, font);
                    var program = view.ResolveEmbeddedFontProgram();
                    builder.AppendLine(
                        $"font {name} subtype={view.Subtype} encoding={view.Encoding} base={view.BaseFont} file2={program is not null} descW={view.ResolveDescendant()?.Widths is not null}");
                }
            }

            var xobjects = document.ResolveDictionary(resources.Get("XObject"));
            builder.AppendLine("xobjects=" + (xobjects is null ? "none" : string.Join(",", xobjects.Values.Keys)));
        }

        foreach (var command in plan.Commands.OfType<PdfTextCommand>().Take(80))
        {
            var hex = command.EncodedBytes is { Length: > 0 }
                ? Convert.ToHexString(command.EncodedBytes)
                : "";
            var decoded = PdfFontDecoder.Decode(document, page, command, PdfLimits.Default).Text;
            builder.AppendLine(
                $"text size={command.FontSize:0.###} origin={command.Origin.X:0.##},{command.Origin.Y:0.##} font={command.FontName} hex={hex} raw={command.Text.Replace('\0', '·')} uni={decoded}");
        }

        builder.AppendLine("--- content ---");
        foreach (var stream in page.Contents)
        {
            var content = PdfStreamDecoder.Decode(stream, PdfLimits.Default);
            builder.AppendLine(System.Text.Encoding.ASCII.GetString(content.AsSpan(0, System.Math.Min(content.Length, 12000))));
        }
        builder.AppendLine("--- extract ---");
        builder.AppendLine(documentText.Length > 1500 ? documentText[..1500] : documentText);
        return builder.ToString();
    }
}
