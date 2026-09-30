using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Text;

namespace Novolis.Pdf.Unit;

public sealed class PdfTextExtractorTests
{
    [Test]
    public async Task ExtractsPageTextFromTypedContentStream()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(PdfParserTests.BuildClassicHello()),
            "fixture.pdf",
            "fixture");
        var document = await PdfParser.ParseAsync(source);
        var extractor = new PdfTextExtractor();

        var spans = extractor.ExtractDocument(document);

        await Assert.That(spans.Count).IsEqualTo(1);
        await Assert.That(spans[0].Text).IsEqualTo("Hello PDF");
        await Assert.That(spans[0].PageIndex).IsEqualTo(0);
    }

    [Test]
    public async Task SearchFindsTypedTextSpans()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(PdfParserTests.BuildClassicHello()),
            "fixture.pdf",
            "fixture");
        var document = await PdfParser.ParseAsync(source);
        var spans = new PdfTextExtractor().ExtractDocument(document);
        var matches = new PdfTextSearch().Search(spans, "hello");

        await Assert.That(matches.Count).IsEqualTo(1);
        await Assert.That(matches[0].PageIndex).IsEqualTo(0);
    }
}
