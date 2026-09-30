namespace Novolis.Pdf.Abstractions;

/// <summary>Text with page-space geometry and source provenance.</summary>
public sealed record PdfTextSpan(
    int PageIndex,
    string Text,
    PdfRect Bounds,
    int? ObjectNumber = null,
    int Sequence = 0,
    string? FontFamily = null,
    double FontSize = 0);
