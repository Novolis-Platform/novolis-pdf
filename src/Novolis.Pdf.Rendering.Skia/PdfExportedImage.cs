namespace Novolis.Pdf.Rendering.Skia;

/// <summary>One embedded image written to disk with PDF CTM placement (points, y-up).</summary>
public sealed record PdfExportedImage(
    string Path,
    int PixelWidth,
    int PixelHeight,
    double PdfLeft,
    double PdfBottom,
    double PdfWidth,
    double PdfHeight,
    string PixelFormat);
