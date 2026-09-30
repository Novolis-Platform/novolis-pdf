using Novolis.Pdf.Abstractions;
using SkiaSharp;

namespace Novolis.Pdf.Rendering.Skia;

internal static class PdfSkiaCanvas
{
    public static SKMatrix ToSkia(PdfMatrix matrix) =>
        new()
        {
            ScaleX = (float)matrix.A,
            SkewX = (float)matrix.C,
            TransX = (float)matrix.E,
            SkewY = (float)matrix.B,
            ScaleY = (float)matrix.D,
            TransY = (float)matrix.F,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1,
        };

    public static PdfMatrix FromSkia(SKMatrix matrix) =>
        new(matrix.ScaleX, matrix.SkewY, matrix.SkewX, matrix.ScaleY, matrix.TransX, matrix.TransY);
}
