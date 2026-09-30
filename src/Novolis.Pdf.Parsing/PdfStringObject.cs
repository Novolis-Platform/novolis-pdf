using System.Text;

namespace Novolis.Pdf.Parsing;

/// <summary>PDF literal or hexadecimal string.</summary>
public sealed record PdfStringObject(byte[] Bytes, bool IsHexadecimal = false) : PdfObject
{
    /// <inheritdoc />
    public override PdfObjectKind Kind => PdfObjectKind.String;

    /// <summary>Decodes the common PDF string encodings.</summary>
    public string GetText()
    {
        if (Bytes.Length >= 2 && Bytes[0] == 0xFE && Bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(Bytes, 2, Bytes.Length - 2);
        if (Bytes.Length >= 2 && Bytes[0] == 0xFF && Bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(Bytes, 2, Bytes.Length - 2);
        return Encoding.Latin1.GetString(Bytes);
    }
}
