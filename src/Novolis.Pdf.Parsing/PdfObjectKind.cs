namespace Novolis.Pdf.Parsing;

/// <summary>Discriminator for the typed PDF object union.</summary>
public enum PdfObjectKind
{
    /// <summary>PDF null.</summary>
    Null,

    /// <summary>PDF boolean.</summary>
    Boolean,

    /// <summary>PDF number.</summary>
    Number,

    /// <summary>PDF name.</summary>
    Name,

    /// <summary>PDF string.</summary>
    String,

    /// <summary>PDF array.</summary>
    Array,

    /// <summary>PDF dictionary.</summary>
    Dictionary,

    /// <summary>PDF stream.</summary>
    Stream,

    /// <summary>Indirect object reference.</summary>
    IndirectReference,
}
