using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>Polyline/path drawing command.</summary>
public sealed record PdfPathCommand(
    IReadOnlyList<PdfPoint> Points,
    bool Closed,
    bool Fill,
    bool Stroke,
    double StrokeWidth = 1,
    PdfColor FillColor = default,
    PdfColor StrokeColor = default) : PdfDrawCommand;
