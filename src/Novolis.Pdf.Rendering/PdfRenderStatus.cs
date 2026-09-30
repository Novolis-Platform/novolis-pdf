namespace Novolis.Pdf.Rendering;

/// <summary>Outcome of a page render request.</summary>
public enum PdfRenderStatus
{
    /// <summary>Page was rendered.</summary>
    Rendered,

    /// <summary>Page exists but contains unsupported content.</summary>
    Partial,

    /// <summary>Page could not be rendered.</summary>
    Failed,
}
