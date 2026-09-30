namespace Novolis.Pdf.Abstractions;

/// <summary>Selection geometry for one contiguous page-space text selection.</summary>
public sealed record PdfTextSelection(
    int PageIndex,
    PdfRect Bounds,
    int StartSpanSequence,
    int EndSpanSequence,
    string Text)
{
    /// <summary>Returns whether the selection belongs to the supplied page.</summary>
    public bool IsOnPage(int pageIndex) => PageIndex == pageIndex;
}
