using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;

namespace Novolis.Pdf.Unit;

public sealed class PdfSkiaRendererTests
{
    [Test]
    public async Task RendersDeterministicPngFromTypedPageTree()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(PdfParserTests.BuildClassicHello()),
            "fixture.pdf",
            "fixture");
        var document = await PdfParser.ParseAsync(source);
        var renderer = new PdfSkiaPageRenderer();
        var request = new PdfRenderRequest(0, 72);

        var first = await renderer.RenderAsync(document, request);
        var second = await renderer.RenderAsync(document, request);

        await Assert.That(first.Status).IsEqualTo(PdfRenderStatus.Rendered);
        await Assert.That(first.PixelWidth).IsGreaterThan(0);
        await Assert.That(first.PixelHeight).IsGreaterThan(0);
        await Assert.That(first.PngBytes[0]).IsEqualTo((byte)0x89);
        await Assert.That(first.PngBytes.SequenceEqual(second.PngBytes)).IsTrue();
    }

    [Test]
    public async Task ViewportFitKeepsPageInsideThePane()
    {
        var view = PdfViewportTransform.FitPage(432, 648, 800, 600, 1);
        var topLeft = view.Transform(new PdfPoint(0, 648));
        var bottomRight = view.Transform(new PdfPoint(432, 0));

        await Assert.That(topLeft.Y).IsEqualTo(0).Within(0.01);
        await Assert.That(bottomRight.X - topLeft.X).IsLessThanOrEqualTo(800);
        await Assert.That(bottomRight.Y - topLeft.Y).IsLessThanOrEqualTo(600);
    }

    [Test]
    public async Task FitWidthScalesToTheViewportWidth()
    {
        var view = PdfViewportTransform.FitWidth(432, 648, 800, 1);
        var right = view.Transform(new PdfPoint(432, 648));

        await Assert.That(right.X).IsEqualTo(800).Within(0.5);
    }

    [Test]
    public async Task RasterTransformHonorsCropOriginAndNativeRotation()
    {
        var page = new PdfPageInfo(
            0,
            new PdfRect(0, 0, 400, 500),
            new PdfRect(10, 20, 210, 320),
            Rotation: 90);
        var view = PdfViewportTransform.Raster(page, 72);
        var cropBottomLeft = view.Transform(new PdfPoint(10, 20));
        var cropTopRight = view.Transform(new PdfPoint(210, 320));

        await Assert.That(page.DisplayWidth).IsEqualTo(300);
        await Assert.That(page.DisplayHeight).IsEqualTo(200);
        await Assert.That(cropBottomLeft.X).IsEqualTo(0).Within(0.01);
        await Assert.That(cropBottomLeft.Y).IsEqualTo(0).Within(0.01);
        await Assert.That(cropTopRight.X).IsEqualTo(300).Within(0.01);
        await Assert.That(cropTopRight.Y).IsEqualTo(200).Within(0.01);
    }
}
