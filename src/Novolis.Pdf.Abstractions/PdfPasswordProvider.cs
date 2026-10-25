namespace Novolis.Pdf.Abstractions;

/// <summary>Asynchronously obtains a password without exposing it to diagnostics.</summary>
public delegate ValueTask<string?> PdfPasswordProvider(
    PdfPasswordRequest request,
    CancellationToken cancellationToken);
