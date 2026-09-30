namespace Novolis.Pdf.Parsing;

/// <summary>PDF stream with its dictionary and encoded bytes.</summary>
public sealed record PdfStreamObject(
    PdfDictionaryObject Dictionary,
    byte[] EncodedBytes) : PdfObject
{
    /// <inheritdoc />
    public override PdfObjectKind Kind => PdfObjectKind.Stream;
}
