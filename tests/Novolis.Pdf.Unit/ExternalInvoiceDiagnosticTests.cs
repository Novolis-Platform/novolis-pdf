using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using Novolis.Pdf.Rendering.Skia;
using Novolis.Pdf.Text;

namespace Novolis.Pdf.Unit;

public sealed class ExternalInvoiceDiagnosticTests
{
    [Test]
    public async Task Configured_external_invoice_parses_and_renders_every_page()
    {
        var path = Environment.GetEnvironmentVariable("NOVOLIS_PDF_INVOICE_PATH");
        Skip.Unless(
            !string.IsNullOrWhiteSpace(path) && File.Exists(path),
            "Set NOVOLIS_PDF_INVOICE_PATH to run the external invoice diagnostic.");

        await using var source = PdfSources.OpenFile(path!);
        var document = await PdfParser.ParseAsync(source);
        var pages = PdfPageTree.Resolve(document);
        var text = string.Concat(new PdfTextExtractor().ExtractDocument(document)
            .Select(static span => span.Text));

        Console.WriteLine($"invoice={path}");
        Console.WriteLine($"bytes={new FileInfo(path!).Length} pages={pages.Count}");
        Console.WriteLine($"objects={document.Objects.Count} diagnostics={document.Diagnostics.Count}");
        foreach (var diagnostic in document.Diagnostics)
            Console.WriteLine($"document-diagnostic={diagnostic.Code} {diagnostic.Message}");
        Console.WriteLine($"text-chars={text.Length} has-invoice={text.Contains("Invoice", StringComparison.OrdinalIgnoreCase)}");

        await Assert.That(pages.Count).IsGreaterThan(0);
        await Assert.That(text).Contains("Invoice");

        var renderer = new PdfSkiaPageRenderer();
        for (var index = 0; index < pages.Count; index++)
        {
            var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[index]);
            var rendered = await renderer.RenderAsync(
                document,
                new PdfRenderRequest(index, 72));

            Console.WriteLine(
                $"page={index + 1} commands={plan.Commands.Count} plan-diagnostics={plan.Diagnostics.Count} " +
                $"status={rendered.Status} pixels={rendered.PixelWidth}x{rendered.PixelHeight} png={rendered.PngBytes.Length}");
            foreach (var diagnostic in plan.Diagnostics)
                Console.WriteLine($"page-diagnostic={diagnostic.Code} {diagnostic.Message}");

            await Assert.That(plan.Diagnostics.Any(static diagnostic => diagnostic.Code == "PDF030"))
                .IsFalse()
                .Because($"page {index + 1}");
            await Assert.That(rendered.Status is PdfRenderStatus.Rendered or PdfRenderStatus.Partial)
                .IsTrue()
                .Because($"page {index + 1}");
            await Assert.That(rendered.PngBytes.Length)
                .IsGreaterThan(1000)
                .Because($"page {index + 1}");
        }
    }
}
