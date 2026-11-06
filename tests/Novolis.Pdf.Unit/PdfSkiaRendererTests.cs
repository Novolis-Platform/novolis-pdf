using System.Text;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using SkiaSharp;

namespace Novolis.Pdf.Unit;

public sealed class PdfSkiaRendererTests
{
    [Test]
    public async Task ImageDoPaintsUnitSquareMappedByCtmNotPixelSize()
    {
        // 2×2 red image with CTM 40×40 pt → footprint must be 40×40 device px @ 72 DPI.
        // Drawing Width×Height under the same CTM would paint ~80×80 and fail the outside sample.
        await using var source = PdfSources.FromStream(
            new MemoryStream(BuildSolidRgbImagePdf(width: 2, height: 2, r: 220, g: 20, b: 20, ctmW: 40, ctmH: 40, tx: 10, ty: 10)),
            "image-unit-square.pdf",
            "fixture");
        var document = await PdfParser.ParseAsync(source);
        var rendered = await new PdfSkiaPageRenderer().RenderAsync(document, new PdfRenderRequest(0, 72));
        await Assert.That(rendered.Status).IsEqualTo(PdfRenderStatus.Rendered);

        using var bitmap = SKBitmap.Decode(rendered.PngBytes);
        await Assert.That(bitmap).IsNotNull();
        // PDF (30,30) → device (30, 70) inside correct 40×40 footprint.
        var inside = bitmap!.GetPixel(30, 70);
        await Assert.That(inside.Red).IsGreaterThan((byte)180);
        await Assert.That(inside.Green).IsLessThan((byte)80);
        // PDF (70,30) → device (70, 70) outside correct footprint (would be red if Width×Height blow-up).
        var outside = bitmap.GetPixel(70, 70);
        await Assert.That(outside.Green).IsGreaterThan((byte)200);
        await Assert.That(outside.Blue).IsGreaterThan((byte)200);
    }

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

    private static byte[] BuildSolidRgbImagePdf(
        int width,
        int height,
        byte r,
        byte g,
        byte b,
        int ctmW,
        int ctmH,
        int tx,
        int ty)
    {
        var samples = new byte[width * height * 3];
        for (var i = 0; i < samples.Length; i += 3)
        {
            samples[i] = r;
            samples[i + 1] = g;
            samples[i + 2] = b;
        }

        var content = $"q {ctmW} 0 0 {ctmH} {tx} {ty} cm /Im1 Do Q";
        var contentBytes = Encoding.ASCII.GetByteCount(content);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] "
                + "/Contents 4 0 R /Resources << /XObject << /Im1 5 0 R >> >> >>",
            $"<< /Length {contentBytes} >>\nstream\n{content}\nendstream",
        };

        var output = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
        var offsets = new int[objects.Length + 2];
        for (var i = 0; i < objects.Length; i++)
            AppendAsciiObject(output, offsets, i + 1, objects[i]);

        var imageDict =
            $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} "
            + $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {samples.Length} >>\nstream\n";
        offsets[objects.Length + 1] = output.Count;
        output.AddRange(Encoding.ASCII.GetBytes($"{objects.Length + 1} 0 obj\n{imageDict}"));
        output.AddRange(samples);
        output.AddRange(Encoding.ASCII.GetBytes("\nendstream\nendobj\n"));

        var xrefOffset = output.Count;
        var size = objects.Length + 2;
        var xref = new StringBuilder($"xref\n0 {size}\n0000000000 65535 f \n");
        for (var i = 1; i < size; i++)
            xref.Append($"{offsets[i]:D10} 00000 n \n");
        xref.Append($"trailer\n<< /Size {size} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        output.AddRange(Encoding.ASCII.GetBytes(xref.ToString()));
        return output.ToArray();
    }

    private static void AppendAsciiObject(List<byte> output, int[] offsets, int number, string body)
    {
        offsets[number] = output.Count;
        output.AddRange(Encoding.ASCII.GetBytes($"{number} 0 obj\n{body}\nendobj\n"));
    }
}
