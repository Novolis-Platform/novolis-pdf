using Novolis.Pdf.Core;
using Novolis.Pdf.Parsing;

namespace Novolis.Pdf.Unit;

public sealed class PdfTypedDocumentTests
{
    [Test]
    public async Task SessionExposesTypedCatalogAndTrailer()
    {
        await using var source = PdfSources.FromStream(
            new MemoryStream(PdfParserTests.BuildClassicHello()),
            "fixture.pdf",
            "fixture");
        await using var session = await PdfDocumentSession.OpenAsync(source);

        await Assert.That(session.Trailer?.RootReference?.ObjectNumber).IsEqualTo(1);
        await Assert.That(session.Catalog?.Type).IsEqualTo(PdfNames.Catalog);
        await Assert.That(session.Catalog?.IsCatalog).IsTrue();
        await Assert.That(session.Catalog?.PagesReference?.ObjectNumber).IsEqualTo(2);
        await Assert.That(session.Document.TryResolve<PdfDictionaryObject>(
                session.Catalog?.PagesReference,
                out var pages))
            .IsTrue();
        await Assert.That(pages?.GetName(PdfNames.Type)).IsEqualTo(PdfNames.Pages);
    }

    [Test]
    public async Task PdfNameEqualityIgnoresLeadingSlash()
    {
        var catalog = new PdfName("/Catalog");

        await Assert.That(catalog == PdfNames.Catalog).IsTrue();
        await Assert.That(catalog.Equals("Catalog")).IsTrue();
        await Assert.That(catalog.Equals("/Catalog")).IsTrue();
        await Assert.That(new PdfName("Page") != PdfNames.Catalog).IsTrue();
    }
}
