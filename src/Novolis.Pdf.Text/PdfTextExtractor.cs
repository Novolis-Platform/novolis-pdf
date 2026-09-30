using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Rendering;

namespace Novolis.Pdf.Text;

/// <summary>Extracts portable text commands and maps them to page geometry.</summary>
public sealed class PdfTextExtractor
{
    private readonly PdfLimits _limits;

    /// <summary>Creates a text extractor with bounded parsing limits.</summary>
    public PdfTextExtractor(PdfLimits? limits = null)
    {
        _limits = limits ?? PdfLimits.Default;
        _limits.Validate();
    }

    /// <summary>Extracts text spans from one page.</summary>
    public IReadOnlyList<PdfTextSpan> ExtractPage(
        Novolis.Pdf.Parsing.PdfParsedDocument document,
        int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));

        var pages = PdfPageTree.Resolve(document, _limits);
        if (pageIndex >= pages.Count)
            return [];

        var plan = PdfContentStreamInterpreter.BuildPlan(document, pages[pageIndex], _limits);
        var spans = new List<PdfTextSpan>();
        var sequence = 0;
        foreach (var text in plan.Commands.OfType<PdfTextCommand>())
        {
            var decoded = PdfFontDecoder.Decode(document, pages[pageIndex], text, _limits);
            var width = System.Math.Max(text.FontSize * 0.25, decoded.Advance);
            var bounds = new PdfRect(
                text.Origin.X,
                text.Origin.Y - System.Math.Max(1, text.FontSize * 0.2),
                text.Origin.X + width,
                text.Origin.Y + System.Math.Max(1, text.FontSize * 0.8));
            spans.Add(new PdfTextSpan(
                pageIndex,
                decoded.Text,
                bounds,
                Sequence: sequence++,
                FontFamily: text.FontName,
                FontSize: text.FontSize));
        }

        return spans;
    }

    /// <summary>Extracts text spans from every resolved page.</summary>
    public IReadOnlyList<PdfTextSpan> ExtractDocument(
        Novolis.Pdf.Parsing.PdfParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var pages = PdfPageTree.Resolve(document, _limits);
        var spans = new List<PdfTextSpan>();
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            spans.AddRange(ExtractPage(document, pageIndex));
        return spans;
    }
}
