namespace Novolis.Pdf.Parsing;

/// <summary>PDF null object.</summary>
public sealed record PdfNullObject : PdfObject
{
    /// <inheritdoc />
    public override PdfObjectKind Kind => PdfObjectKind.Null;

    /// <summary>Shared null instance.</summary>
    public static PdfNullObject Instance { get; } = new();
}
