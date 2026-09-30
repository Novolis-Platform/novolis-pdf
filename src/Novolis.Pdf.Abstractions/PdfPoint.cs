namespace Novolis.Pdf.Abstractions;

/// <summary>A point in PDF user-space coordinates.</summary>
public readonly record struct PdfPoint(double X, double Y)
{
    /// <summary>Returns the translated point.</summary>
    public PdfPoint Translate(double x, double y) => new(X + x, Y + y);
}
