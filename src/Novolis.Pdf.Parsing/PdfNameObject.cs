namespace Novolis.Pdf.Parsing;

/// <summary>PDF name object without the leading slash.</summary>
public sealed record PdfNameObject(string Value) : PdfObject
{
    /// <inheritdoc />
    public override PdfObjectKind Kind => PdfObjectKind.Name;

    /// <summary>Converts this object into a typed name token.</summary>
    public PdfName AsName() => new(Value);

    /// <inheritdoc />
    public override string ToString() => $"/{Value}";
}
