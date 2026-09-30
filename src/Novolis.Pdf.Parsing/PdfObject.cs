namespace Novolis.Pdf.Parsing;

/// <summary>Base type for PDF primitive and composite values.</summary>
public abstract record PdfObject
{
    /// <summary>Runtime-safe discriminator for the PDF value union.</summary>
    public abstract PdfObjectKind Kind { get; }
}
