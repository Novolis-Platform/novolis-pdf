using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Parsing;

/// <summary>Reads a PDF rectangle array into a typed rectangle.</summary>
public static class PdfRectangleReader
{
    /// <summary>Reads four numeric corners as a normalized rectangle.</summary>
    public static bool TryRead(PdfObject? value, out PdfRect rectangle)
    {
        rectangle = default;
        if (value is not PdfArrayObject array)
            return false;
        if (array.Get<PdfNumberObject>(0) is not { } x0
            || array.Get<PdfNumberObject>(1) is not { } y0
            || array.Get<PdfNumberObject>(2) is not { } x1
            || array.Get<PdfNumberObject>(3) is not { } y1)
        {
            return false;
        }

        rectangle = PdfRect.FromCorners(
            new PdfPoint(x0.Value, y0.Value),
            new PdfPoint(x1.Value, y1.Value));
        return true;
    }
}
