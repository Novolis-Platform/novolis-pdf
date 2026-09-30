namespace Novolis.Pdf.Abstractions;

/// <summary>Document-level metadata exposed by a parsed PDF.</summary>
public sealed record PdfDocumentInfo(
    PdfSourceDescriptor Source,
    string? Title = null,
    string? Author = null,
    string? Subject = null,
    string? Creator = null,
    string? Producer = null,
    string? Language = null,
    string? Version = null,
    PdfDocumentCapabilities Capabilities = PdfDocumentCapabilities.None);
