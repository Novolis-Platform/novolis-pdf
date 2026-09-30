namespace Novolis.Pdf.Abstractions;

/// <summary>A search result with page and text geometry.</summary>
public sealed record PdfSearchMatch(
    int PageIndex,
    string Query,
    string Text,
    PdfRect Bounds,
    int StartIndex);
