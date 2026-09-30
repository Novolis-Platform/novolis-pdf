namespace Novolis.Pdf.Abstractions;

/// <summary>Affine transform used by PDF graphics and text operators.</summary>
public readonly record struct PdfMatrix(
    double A,
    double B,
    double C,
    double D,
    double E,
    double F)
{
    /// <summary>Identity matrix.</summary>
    public static PdfMatrix Identity { get; } = new(1, 0, 0, 1, 0, 0);

    /// <summary>Transforms a point.</summary>
    public PdfPoint Transform(PdfPoint point) =>
        new(
            A * point.X + C * point.Y + E,
            B * point.X + D * point.Y + F);

    /// <summary>Concatenates this matrix with another matrix.</summary>
    public PdfMatrix Multiply(PdfMatrix other) =>
        new(
            A * other.A + C * other.B,
            B * other.A + D * other.B,
            A * other.C + C * other.D,
            B * other.C + D * other.D,
            A * other.E + C * other.F + E,
            B * other.E + D * other.F + F);
}
