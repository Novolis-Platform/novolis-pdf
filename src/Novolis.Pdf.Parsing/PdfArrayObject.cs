namespace Novolis.Pdf.Parsing;

/// <summary>PDF array object.</summary>
public sealed record PdfArrayObject(IReadOnlyList<PdfObject> Items) : PdfObject
{
    /// <inheritdoc />
    public override PdfObjectKind Kind => PdfObjectKind.Array;

    /// <summary>Returns an item when it has the requested index and type.</summary>
    public T? Get<T>(int index)
        where T : PdfObject =>
        index >= 0 && index < Items.Count ? Items[index] as T : null;
}
