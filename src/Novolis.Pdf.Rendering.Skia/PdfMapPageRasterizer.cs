using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using SkiaSharp;

namespace Novolis.Pdf.Rendering.Skia;

/// <summary>
/// Rasters a map print page and crops to the image-tile content frame.
/// Default paint is <see cref="PdfPaintFlags.ImagesOnly"/> so vector chrome/labels are not baked in.
/// Output row 0 = north (PDF y-up → device via viewport raster transform).
/// </summary>
public static class PdfMapPageRasterizer
{
    /// <summary>Rasters and crops the page map frame to a north-up PNG.</summary>
    public static PdfMapRasterResult RasterCropped(
        PdfParsedDocument document,
        PdfResolvedPage page,
        double dpi,
        PdfPaintFlags flags = PdfPaintFlags.ImagesOnly) =>
        RasterCropped(document, page, dpi, frame: null, flags);

    /// <summary>
    /// Rasters the page and crops to <paramref name="frame"/> when supplied (shared stack alignment).
    /// When null, the frame is inferred from non-mask image commands on this page only.
    /// </summary>
    public static PdfMapRasterResult RasterCropped(
        PdfParsedDocument document,
        PdfResolvedPage page,
        double dpi,
        PdfMapContentFrame? frame,
        PdfPaintFlags flags = PdfPaintFlags.ImagesOnly)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        if (dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(dpi));

        var plan = PdfContentStreamInterpreter.BuildPlan(document, page);
        var cropFrame = frame ?? PdfMapContentFrame.FromImageCommands(plan);
        var scale = dpi / 72d;
        var info = page.Info;
        var fullWidth = System.Math.Clamp((int)System.Math.Ceiling(info.DisplayWidth * scale), 1, 16_384);
        var fullHeight = System.Math.Clamp((int)System.Math.Ceiling(info.DisplayHeight * scale), 1, 16_384);
        var view = PdfViewportTransform.Raster(info, dpi);

        using var full = new SKBitmap(fullWidth, fullHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(full))
            new PdfSkiaPageRenderer().Paint(canvas, document, page, plan, view, flags);

        var cropLeft = cropFrame.Left * scale;
        var cropTop = (info.DisplayHeight - cropFrame.Top) * scale;
        var cropWidth = cropFrame.Width * scale;
        var cropHeight = cropFrame.Height * scale;
        var pixelWidth = System.Math.Clamp((int)System.Math.Round(cropWidth), 1, 16_384);
        var pixelHeight = System.Math.Clamp((int)System.Math.Round(cropHeight), 1, 16_384);

        using var cropped = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.Clear(SKColors.White);
            var srcLeft = (float)cropLeft;
            var srcTop = (float)cropTop;
            var src = new SKRect(srcLeft, srcTop, srcLeft + (float)cropWidth, srcTop + (float)cropHeight);
            canvas.DrawBitmap(full, src, new SKRect(0, 0, pixelWidth, pixelHeight), new SKSamplingOptions(SKFilterMode.Linear));
        }

        using var encoded = SKImage.FromBitmap(cropped).Encode(SKEncodedImageFormat.Png, 92);
        return new PdfMapRasterResult(
            encoded.ToArray(),
            pixelWidth,
            pixelHeight,
            cropFrame,
            dpi,
            PixelOriginNorthWest: true);
    }
}
