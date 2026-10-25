using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>Clips subsequent drawing to a transformed PDF path.</summary>
public sealed record PdfClipCommand(
    IReadOnlyList<PdfPoint> Points,
    bool EvenOdd) : PdfDrawCommand;
