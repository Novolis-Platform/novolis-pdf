using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Platform;

/// <summary>Platform-neutral request to open a PDF source.</summary>
public sealed record PdfOpenRequest(
    PdfSourceDescriptor Descriptor,
    Func<CancellationToken, ValueTask<Stream>> OpenReadAsync);
