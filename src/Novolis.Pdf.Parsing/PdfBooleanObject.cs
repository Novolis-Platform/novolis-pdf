namespace Novolis.Pdf.Parsing;

/// <summary>PDF boolean object.</summary>
public sealed record PdfBooleanObject(bool Value) : PdfObject
{
    /// <inheritdoc />
    public override PdfObjectKind Kind => PdfObjectKind.Boolean;
}
