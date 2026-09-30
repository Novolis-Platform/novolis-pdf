namespace Novolis.Pdf.Documents;

/// <summary>Local recent-document entry.</summary>
public sealed record PdfRecentDocument(
    string StableId,
    string DisplayName,
    string Locator,
    DateTimeOffset LastOpenedUtc,
    int? LastPageIndex = null);
