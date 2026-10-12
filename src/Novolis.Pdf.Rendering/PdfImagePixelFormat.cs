namespace Novolis.Pdf.Rendering;

/// <summary>Supported sample layouts for an image XObject.</summary>
public enum PdfImagePixelFormat
{
    /// <summary>One gray sample per pixel.</summary>
    Gray,
    /// <summary>Red, green, and blue samples per pixel.</summary>
    Rgb,
    /// <summary>Cyan, magenta, yellow, and black samples per pixel.</summary>
    Cmyk,
}
