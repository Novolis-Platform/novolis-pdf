namespace Novolis.Pdf.Parsing;

/// <summary>Reference to an indirect PDF object.</summary>
public sealed record PdfIndirectReference(int ObjectNumber, int Generation = 0) : PdfObject
{
    /// <inheritdoc />
    public override PdfObjectKind Kind => PdfObjectKind.IndirectReference;
}
