namespace Novolis.Pdf.Abstractions;

/// <summary>Severity of a reader diagnostic.</summary>
public enum PdfDiagnosticSeverity
{
    /// <summary>Informational detail that does not affect rendering.</summary>
    Information,

    /// <summary>Recoverable condition or unsupported optional feature.</summary>
    Warning,

    /// <summary>Condition that prevents a document or page from being read.</summary>
    Error,
}
