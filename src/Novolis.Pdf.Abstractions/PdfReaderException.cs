namespace Novolis.Pdf.Abstractions;

/// <summary>Exception carrying a structured PDF diagnostic.</summary>
public sealed class PdfReaderException : Exception
{
    /// <summary>Creates a reader exception.</summary>
    public PdfReaderException(
        PdfDiagnosticEntry diagnostic,
        PdfReaderFailureKind failureKind = PdfReaderFailureKind.Unknown)
        : base(diagnostic.Message, diagnostic.Exception)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
        FailureKind = failureKind;
    }

    /// <summary>Diagnostic that caused the exception.</summary>
    public PdfDiagnosticEntry Diagnostic { get; }

    /// <summary>Structured category for the failure.</summary>
    public PdfReaderFailureKind FailureKind { get; }
}
