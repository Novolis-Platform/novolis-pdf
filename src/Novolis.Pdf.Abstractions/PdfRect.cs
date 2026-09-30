namespace Novolis.Pdf.Abstractions;

/// <summary>A rectangle in PDF user-space coordinates.</summary>
public readonly record struct PdfRect(double Left, double Bottom, double Right, double Top)
{
    /// <summary>Width of the rectangle.</summary>
    public double Width => Math.Max(0, Right - Left);

    /// <summary>Height of the rectangle.</summary>
    public double Height => Math.Max(0, Top - Bottom);

    /// <summary>Creates a normalized rectangle from two corners.</summary>
    public static PdfRect FromCorners(PdfPoint first, PdfPoint second) =>
        new(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Max(first.X, second.X),
            Math.Max(first.Y, second.Y));

    /// <summary>Returns whether a point is inside the rectangle.</summary>
    public bool Contains(PdfPoint point) =>
        point.X >= Left && point.X <= Right
        && point.Y >= Bottom && point.Y <= Top;

    /// <summary>Returns the union of two rectangles.</summary>
    public PdfRect Union(PdfRect other) =>
        new(
            Math.Min(Left, other.Left),
            Math.Min(Bottom, other.Bottom),
            Math.Max(Right, other.Right),
            Math.Max(Top, other.Top));
}
