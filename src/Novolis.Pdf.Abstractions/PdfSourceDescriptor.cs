namespace Novolis.Pdf.Abstractions;

/// <summary>Stable display and identity information for a PDF source.</summary>
public sealed record PdfSourceDescriptor(
    string DisplayName,
    string? StableId = null,
    long? Length = null,
    DateTimeOffset? LastModifiedUtc = null)
{
    /// <summary>Returns a normalized descriptor.</summary>
    public PdfSourceDescriptor Normalize()
    {
        var name = string.IsNullOrWhiteSpace(DisplayName) ? "document.pdf" : DisplayName.Trim();
        var stableId = string.IsNullOrWhiteSpace(StableId) ? null : StableId.Trim();
        var length = Length is >= 0 ? Length : null;
        return this with
        {
            DisplayName = name,
            StableId = stableId,
            Length = length,
        };
    }
}
