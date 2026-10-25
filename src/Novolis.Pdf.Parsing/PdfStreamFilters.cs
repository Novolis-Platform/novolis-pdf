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

        if (name == PdfNames.LzwDecode || name == PdfNames.Lzw)
        {
            filter = PdfStreamFilter.LzwDecode;
            return true;
        }

        if (name == PdfNames.Crypt)
        {
            filter = PdfStreamFilter.Crypt;
            return true;
        }

        if (name == PdfNames.DctDecode)
        {
            filter = PdfStreamFilter.DctDecode;
            return true;
        }

        if (name == PdfNames.CcittFaxDecode)
        {
            filter = PdfStreamFilter.CcittFaxDecode;
            return true;
        }

        if (name == PdfNames.JpxDecode)
        {
            filter = PdfStreamFilter.JpxDecode;
            return true;
        }

        if (name == PdfNames.Jbig2Decode)
        {
            filter = PdfStreamFilter.Jbig2Decode;
            return true;
        }

        filter = PdfStreamFilter.Unknown;
        return false;
    }
}
