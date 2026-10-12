using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using SkiaSharp;

namespace Novolis.Pdf.Unit;

public sealed class PdfStressFixtureTests
{
    [Test]
    public async Task Pathological_fixtures_parse_and_render_without_crashing()
    {
        var root = Environment.GetEnvironmentVariable("NOVOLIS_PDF_STRESS_ROOT");
        Skip.Unless(
            !string.IsNullOrWhiteSpace(root) && Directory.Exists(root),
            "Set NOVOLIS_PDF_STRESS_ROOT to run the generated PDF stress fixtures.");

        var renderer = new PdfSkiaPageRenderer();
        foreach (var name in new[]
                 {
                     "many-pages-1000.pdf",
                     "font-heavy.pdf",
                     "image-heavy-2400.pdf",
                     "oversized-page.pdf",
                 })
        {
            var path = Path.Combine(root!, name);
            Skip.Unless(File.Exists(path), $"Missing stress fixture: {path}");
            await using var source = PdfSources.OpenFile(path);
            var document = await PdfParser.ParseAsync(source);
            var pages = PdfPageTree.Resolve(document);

            await Assert.That(pages.Count).IsGreaterThan(0).Because(name);
            foreach (var pageIndex in new[] { 0, pages.Count - 1 }.Distinct())
            {
                var rendered = await renderer.RenderAsync(
                    document,
                    new PdfRenderRequest(pageIndex, 72));

                await Assert.That(
                        rendered.Status is PdfRenderStatus.Rendered or PdfRenderStatus.Partial)
                    .IsTrue()
                    .Because($"{name} page {pageIndex + 1}");
                await Assert.That(rendered.PngBytes.Length)
                    .IsGreaterThan(100)
                    .Because($"{name} page {pageIndex + 1}");
                await Assert.That(ContainsVisibleInk(rendered.PngBytes))
                    .IsTrue()
                    .Because($"{name} page {pageIndex + 1}");
                if (name == "image-heavy-2400.pdf")
                {
                    var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[pageIndex]);
                    await Assert.That(plan.Commands.OfType<PdfImageCommand>()).Count().IsEqualTo(1);
                }
            }
        }
    }

    [Test]
    public async Task Truncated_fixture_is_tolerated_without_throwing()
    {
        var root = Environment.GetEnvironmentVariable("NOVOLIS_PDF_STRESS_ROOT");
        Skip.Unless(
            !string.IsNullOrWhiteSpace(root) && Directory.Exists(root),
            "Set NOVOLIS_PDF_STRESS_ROOT to run the generated PDF stress fixtures.");
        var path = Path.Combine(root!, "malformed-truncated-xref.pdf");
        Skip.Unless(File.Exists(path), $"Missing stress fixture: {path}");

        await using var source = PdfSources.OpenFile(path);
        var document = await PdfParser.ParseAsync(source);
        var pages = PdfPageTree.Resolve(document);

        await Assert.That(pages.Count).IsEqualTo(0);
    }

    private static bool ContainsVisibleInk(byte[] png)
    {
        using var image = SKImage.FromEncodedData(png);
        using var bitmap = SKBitmap.FromImage(image);
        var step = System.Math.Max(1, System.Math.Max(bitmap.Width, bitmap.Height) / 2048);
        for (var y = 0; y < bitmap.Height; y += step)
        for (var x = 0; x < bitmap.Width; x += step)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.Alpha > 0
                && (pixel.Red < 240 || pixel.Green < 240 || pixel.Blue < 240))
                return true;
        }

        return false;
    }
}
