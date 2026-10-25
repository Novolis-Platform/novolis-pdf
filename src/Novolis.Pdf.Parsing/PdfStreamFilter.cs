namespace Novolis.Pdf.Parsing;

/// <summary>Supported PDF stream filters.</summary>
public enum PdfStreamFilter
{
    /// <summary>The filter name is not implemented.</summary>
    Unknown = 0,

    /// <summary>zlib / Deflate.</summary>
    FlateDecode,

    /// <summary>ASCII hexadecimal.</summary>
    AsciiHexDecode,

    /// <summary>ASCII85.</summary>
    Ascii85Decode,

    /// <summary>Run-length.</summary>
    RunLengthDecode,

    /// <summary>LZW.</summary>
    LzwDecode,

    /// <summary>PDF crypt filter already handled by the security layer.</summary>
    Crypt,

    /// <summary>JPEG image data.</summary>
    DctDecode,

    /// <summary>CCITT fax image data.</summary>
    CcittFaxDecode,

    /// <summary>JPEG 2000 image data.</summary>
    JpxDecode,

    /// <summary>JBIG2 image data.</summary>
    Jbig2Decode,
}
