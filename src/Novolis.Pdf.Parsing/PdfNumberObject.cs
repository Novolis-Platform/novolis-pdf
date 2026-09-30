namespace Novolis.Pdf.Parsing;

/// <summary>PDF numeric object.</summary>
public sealed record PdfNumberObject(double Value, string RawValue) : PdfObject
{
    /// <inheritdoc />
    public override PdfObjectKind Kind => PdfObjectKind.Number;
}
