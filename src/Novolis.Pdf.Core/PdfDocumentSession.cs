using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;

namespace Novolis.Pdf.Core;

/// <summary>Owns a source and its parsed PDF lifetime.</summary>
public sealed class PdfDocumentSession : IAsyncDisposable
{
    private bool _disposed;

    private PdfDocumentSession(IPdfSource source, PdfParsedDocument document)
    {
        Source = source;
        Document = document;
    }

    /// <summary>Source owned by the session.</summary>
    public IPdfSource Source { get; }

    /// <summary>Parsed object graph.</summary>
    public PdfParsedDocument Document { get; }

    /// <summary>Typed catalog when the trailer root resolves.</summary>
    public PdfCatalog? Catalog => Document.GetCatalog();

    /// <summary>Typed trailer when one was parsed.</summary>
    public PdfTrailer? Trailer => Document.GetTrailer();

    /// <summary>Opens and parses a source.</summary>
    public static async ValueTask<PdfDocumentSession> OpenAsync(
        IPdfSource source,
        PdfLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var parsed = await PdfParser.ParseAsync(source, limits, cancellationToken).ConfigureAwait(false);
        return new PdfDocumentSession(source, parsed);
    }

    /// <summary>Opens a source using strongly typed bounded reader options.</summary>
    public static ValueTask<PdfDocumentSession> OpenAsync(
        IPdfSource source,
        PdfOpenOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return OpenAsync(source, options.Limits, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        await Source.DisposeAsync().ConfigureAwait(false);
    }
}
