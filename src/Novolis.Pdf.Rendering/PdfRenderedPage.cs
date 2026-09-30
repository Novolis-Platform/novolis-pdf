using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>Host-neutral encoded page output.</summary>
public sealed record PdfRenderedPage(
    int PageIndex,
    int PixelWidth,
    int PixelHeight,
    byte[] PngBytes,
    PdfRenderStatus Status,
    IReadOnlyList<PdfDiagnosticEntry> Diagnostics);
