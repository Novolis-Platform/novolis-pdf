namespace Novolis.Pdf.Abstractions;

/// <summary>One outline/bookmark item.</summary>
public sealed record PdfOutlineItem(
    string Title,
    int? PageIndex = null,
    IReadOnlyList<PdfOutlineItem>? Children = null,
    bool IsOpen = false,
    string? NamedDestination = null);
