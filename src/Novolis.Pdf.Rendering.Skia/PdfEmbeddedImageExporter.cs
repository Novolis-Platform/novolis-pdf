using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using SkiaSharp;

namespace Novolis.Pdf.Rendering.Skia;

/// <summary>Writes each embedded page image as a PNG with placement metadata (north-up page space).</summary>
public static class PdfEmbeddedImageExporter
{
    /// <summary>Decodes every image command on the page and writes PNGs under <paramref name="outputDirectory"/>.</summary>
    public static async Task<IReadOnlyList<PdfExportedImage>> ExportPageAsync(
        PdfParsedDocument document,
        PdfResolvedPage page,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var plan = PdfContentStreamInterpreter.BuildPlan(document, page);
        var results = new List<PdfExportedImage>();
        var index = 0;
        foreach (var command in plan.Commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (command is not PdfImageCommand image || image.IsImageMask)
                continue;

            using var bitmap = PdfSkiaImageDecoder.Decode(image);
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            var kind = image.IsImageMask ? "mask" : "img";
            var fileName = $"p{page.Info.Index:D2}-{kind}{index:D2}-{image.Width}x{image.Height}.png";
            var path = Path.Combine(outputDirectory, fileName);
            await File.WriteAllBytesAsync(path, encoded.ToArray(), cancellationToken).ConfigureAwait(false);
            var m = image.Matrix;
            results.Add(new PdfExportedImage(
                path,
                image.Width,
                image.Height,
                m.E,
                m.F,
                m.A,
                m.D,
                image.IsImageMask ? "ImageMask" : image.PixelFormat.ToString()));
            index++;
        }

        return results;
    }
}
