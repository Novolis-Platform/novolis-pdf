using Novolis.Pdf.Rendering;

namespace Novolis.Pdf.Rendering.Skia;

/// <summary>Cropped north-up map raster from a PDF page.</summary>
public sealed record PdfMapRasterResult(
    byte[] PngBytes,
    int PixelWidth,
    int PixelHeight,
    PdfMapContentFrame FramePdf,
    double Dpi,
    bool PixelOriginNorthWest);
