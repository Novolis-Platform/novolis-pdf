namespace Novolis.Pdf.Abstractions;

/// <summary>Exception carrying a structured PDF diagnostic.</summary>
public sealed class PdfReaderException : Exception
{
    /// <summary>Creates a reader exception.</summary>
    public PdfReaderException(PdfDiagnosticEntry diagnostic)
        : base(diagnostic.Message, diagnostic.Exception)
    {
        Diagnostic = diagnostic;
    }

    /// <summary>Diagnostic that caused the exception.</summary>
    public PdfDiagnosticEntry Diagnostic { get; }
}
