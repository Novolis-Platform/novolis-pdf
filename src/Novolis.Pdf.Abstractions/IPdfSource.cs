namespace Novolis.Pdf.Abstractions;

/// <summary>Random-access source used by the PDF parser.</summary>
public interface IPdfSource : IAsyncDisposable
{
    /// <summary>Human-readable source metadata.</summary>
    PdfSourceDescriptor Descriptor { get; }

    /// <summary>Reads up to <paramref name="buffer"/> length at an absolute byte offset.</summary>
    ValueTask<int> ReadAtAsync(
        long offset,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default);
}
