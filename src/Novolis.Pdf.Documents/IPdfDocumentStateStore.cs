namespace Novolis.Pdf.Documents;

/// <summary>Local-only persistence for reader history and positions.</summary>
public interface IPdfDocumentStateStore
{
    /// <summary>Loads recent documents.</summary>
    ValueTask<IReadOnlyList<PdfRecentDocument>> LoadRecentAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Adds or refreshes a recent document.</summary>
    ValueTask<IReadOnlyList<PdfRecentDocument>> RememberAsync(
        PdfRecentDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a reading position.</summary>
    ValueTask<PdfReadingPosition?> LoadPositionAsync(
        string stableId,
        CancellationToken cancellationToken = default);

    /// <summary>Stores a reading position.</summary>
    ValueTask SavePositionAsync(
        PdfReadingPosition position,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a recent document and its position.</summary>
    ValueTask RemoveAsync(
        string stableId,
        CancellationToken cancellationToken = default);
}
