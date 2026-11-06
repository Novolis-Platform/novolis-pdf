using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>
/// Axis-aligned union of image placements on a page (PDF user space, y-up).
/// Used to crop print chrome from Norgeskart-style tiled map PDFs.
/// </summary>
public readonly record struct PdfMapContentFrame(
    double Left,
    double Bottom,
    double Right,
    double Top)
{
    /// <summary>Frame width in PDF points.</summary>
    public double Width => Right - Left;

    /// <summary>Frame height in PDF points.</summary>
    public double Height => Top - Bottom;

    /// <summary>Infers the content frame from every <see cref="PdfImageCommand"/> placement.</summary>
    public static PdfMapContentFrame FromImageCommands(PdfPageRenderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        foreach (var command in plan.Commands)
        {
            if (command is not PdfImageCommand image || image.IsImageMask)
                continue;

            var m = image.Matrix;
            minX = System.Math.Min(minX, m.E);
            minY = System.Math.Min(minY, m.F);
            maxX = System.Math.Max(maxX, m.E + m.A);
            maxY = System.Math.Max(maxY, m.F + m.D);
        }

        if (minX >= maxX || minY >= maxY)
            throw new InvalidDataException("Could not infer map content frame from PDF image tiles.");

        return new PdfMapContentFrame(minX, minY, maxX, maxY);
    }
}
