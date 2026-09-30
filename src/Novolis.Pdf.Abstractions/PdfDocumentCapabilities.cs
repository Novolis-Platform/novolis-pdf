namespace Novolis.Pdf.Abstractions;

/// <summary>Features discovered while reading a PDF.</summary>
[Flags]
public enum PdfDocumentCapabilities
{
    /// <summary>No optional capability was discovered.</summary>
    None = 0,

    /// <summary>The document contains page content that can be rasterized.</summary>
    Rendering = 1 << 0,

    /// <summary>Text operators or a text map are available.</summary>
    Text = 1 << 1,

    /// <summary>At least one outline item is available.</summary>
    Outlines = 1 << 2,

    /// <summary>At least one link annotation is available.</summary>
    Links = 1 << 3,

    /// <summary>The document was recovered from malformed or incomplete structures.</summary>
    Recovered = 1 << 4,
}
