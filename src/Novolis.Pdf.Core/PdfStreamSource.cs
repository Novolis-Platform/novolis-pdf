using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Core;

/// <summary>Thread-safe random-access adapter over a seekable stream.</summary>
public sealed class PdfStreamSource : IPdfSource
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly bool _leaveOpen;
    private bool _disposed;

    /// <summary>Creates a source over a seekable stream.</summary>
    public PdfStreamSource(
        Stream stream,
        PdfSourceDescriptor descriptor,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!stream.CanSeek || !stream.CanRead)
            throw new ArgumentException("The PDF source stream must be readable and seekable.", nameof(stream));

        _stream = stream;
        Descriptor = descriptor.Normalize() with
        {
            Length = descriptor.Length ?? stream.Length,
        };
        _leaveOpen = leaveOpen;
    }

    /// <inheritdoc />
    public PdfSourceDescriptor Descriptor { get; }

    /// <inheritdoc />
    public async ValueTask<int> ReadAtAsync(
        long offset,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (buffer.Length == 0)
            return 0;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stream.Seek(offset, SeekOrigin.Begin);
            return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _gate.Dispose();
        if (!_leaveOpen)
            _stream.Dispose();
        return ValueTask.CompletedTask;
    }
}
