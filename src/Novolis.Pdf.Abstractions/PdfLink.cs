namespace Novolis.Pdf.Abstractions;

/// <summary>A link annotation resolved from a page.</summary>
public sealed record PdfLink(
    int PageIndex,
    PdfRect Bounds,
    string? Uri = null,
    int? TargetPageIndex = null,
    string? NamedDestination = null);
