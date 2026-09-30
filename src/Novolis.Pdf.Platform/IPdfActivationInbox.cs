namespace Novolis.Pdf.Platform;

/// <summary>Receives file activation requests before or after the UI is ready.</summary>
public interface IPdfActivationInbox
{
    /// <summary>Publishes an activation request.</summary>
    bool Publish(PdfOpenRequest request);

    /// <summary>Reads activation requests in arrival order.</summary>
    IAsyncEnumerable<PdfOpenRequest> ReadAllAsync(CancellationToken cancellationToken = default);
}
