namespace Novolis.Pdf.Parsing;

/// <summary>Type-safe helpers for the PDF object union.</summary>
public static class PdfObjectExtensions
{
    /// <summary>Casts a PDF object without an unchecked conversion.</summary>
    public static bool TryAs<T>(this PdfObject? value, out T typed)
        where T : PdfObject
    {
        if (value is T match)
        {
            typed = match;
            return true;
        }

        typed = null!;
        return false;
    }
}
