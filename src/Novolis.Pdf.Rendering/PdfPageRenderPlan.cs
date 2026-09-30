using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>Resolved page geometry and drawing commands.</summary>
public sealed record PdfPageRenderPlan(
    PdfPageInfo Page,
    IReadOnlyList<PdfDrawCommand> Commands,
    IReadOnlyList<PdfDiagnosticEntry> Diagnostics);
