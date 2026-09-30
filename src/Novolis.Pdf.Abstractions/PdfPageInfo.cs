namespace Novolis.Pdf.Abstractions;

/// <summary>Resolved page metadata.</summary>
public sealed record PdfPageInfo(
    int Index,
    PdfRect MediaBox,
    PdfRect CropBox,
    int Rotation = 0,
    double UserUnit = 1)
{
    /// <summary>Returns the normalized clockwise rotation.</summary>
    public int NormalizedRotation => ((Rotation % 360) + 360) % 360;

    /// <summary>Returns the displayed width after page rotation.</summary>
    public double DisplayWidth =>
        NormalizedRotation is 90 or 270 ? CropBox.Height : CropBox.Width;

    /// <summary>Returns the displayed height after page rotation.</summary>
    public double DisplayHeight =>
        NormalizedRotation is 90 or 270 ? CropBox.Width : CropBox.Height;
}
