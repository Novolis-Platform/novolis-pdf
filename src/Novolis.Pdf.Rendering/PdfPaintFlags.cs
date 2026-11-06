namespace Novolis.Pdf.Rendering;

/// <summary>Selects which display-list command kinds a Skia painter applies.</summary>
[Flags]
public enum PdfPaintFlags
{
    /// <summary>No commands.</summary>
    None = 0,

    /// <summary>Path fill/stroke commands.</summary>
    Paths = 1,

    /// <summary>Clip path commands.</summary>
    Clips = 2,

    /// <summary>Text drawing commands.</summary>
    Text = 4,

    /// <summary>Image XObject / inline image commands.</summary>
    Images = 8,

    /// <summary>Images with clip operators retained (typical map raster body).</summary>
    ImagesOnly = Images | Clips,

    /// <summary>All supported command kinds.</summary>
    All = Paths | Clips | Text | Images,
}
