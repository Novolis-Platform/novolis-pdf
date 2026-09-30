namespace Novolis.Pdf.Documents;

/// <summary>Persisted local reading position.</summary>
public sealed record PdfReadingPosition(
    string StableId,
    int PageIndex,
    double Zoom = 1,
    int Rotation = 0,
    DateTimeOffset UpdatedUtc = default);
