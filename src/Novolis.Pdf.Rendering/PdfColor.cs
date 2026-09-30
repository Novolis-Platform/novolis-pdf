namespace Novolis.Pdf.Rendering;

/// <summary>Linear-independent RGB color used by the page command model.</summary>
public readonly record struct PdfColor(double Red, double Green, double Blue, double Alpha = 1)
{
    /// <summary>Opaque black.</summary>
    public static PdfColor Black { get; } = new(0, 0, 0);

    /// <summary>Opaque white.</summary>
    public static PdfColor White { get; } = new(1, 1, 1);

    /// <summary>Clamps all channels to the renderable range.</summary>
    public PdfColor Clamp() =>
        new(
            System.Math.Clamp(Red, 0, 1),
            System.Math.Clamp(Green, 0, 1),
            System.Math.Clamp(Blue, 0, 1),
            System.Math.Clamp(Alpha, 0, 1));
}
