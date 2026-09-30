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
}
