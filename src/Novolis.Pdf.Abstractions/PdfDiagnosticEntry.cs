namespace Novolis.Pdf.Abstractions;

/// <summary>Structured provenance for a PDF reader condition.</summary>
public sealed record PdfDiagnosticEntry(
    PdfDiagnosticSeverity Severity,
    string Code,
    string Message,
    long? ByteOffset = null,
    int? ObjectNumber = null,
    int? PageIndex = null,
    Exception? Exception = null);
