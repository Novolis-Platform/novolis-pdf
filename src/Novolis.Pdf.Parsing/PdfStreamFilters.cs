namespace Novolis.Pdf.Parsing;

/// <summary>Maps PDF filter names onto the typed filter union.</summary>
public static class PdfStreamFilters
{
    /// <summary>Parses a filter name into a known filter.</summary>
    public static bool TryParse(PdfName name, out PdfStreamFilter filter)
    {
        if (name == PdfNames.FlateDecode || name == PdfNames.Fl)
        {
            filter = PdfStreamFilter.FlateDecode;
            return true;
        }

        if (name == PdfNames.AsciiHexDecode || name == PdfNames.AHx)
        {
            filter = PdfStreamFilter.AsciiHexDecode;
            return true;
        }

        if (name == PdfNames.Ascii85Decode || name == PdfNames.A85)
        {
            filter = PdfStreamFilter.Ascii85Decode;
            return true;
        }

        if (name == PdfNames.RunLengthDecode || name == PdfNames.RL)
        {
            filter = PdfStreamFilter.RunLengthDecode;
            return true;
        }

        filter = PdfStreamFilter.Unknown;
        return false;
    }
}
