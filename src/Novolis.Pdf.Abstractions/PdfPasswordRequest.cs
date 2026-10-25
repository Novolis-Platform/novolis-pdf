namespace Novolis.Pdf.Abstractions;

/// <summary>Context supplied to a password provider.</summary>
public sealed record PdfPasswordRequest(
    string DocumentName,
    int Attempt,
    PdfPasswordFailureReason PreviousFailure = PdfPasswordFailureReason.None);
