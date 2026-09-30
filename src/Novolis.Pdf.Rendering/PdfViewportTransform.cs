using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>View matrices that map PDF user space onto a device canvas.</summary>
public static class PdfViewportTransform
{
    /// <summary>Y-up PDF page to Y-down canvas, then scale and center.</summary>
    public static PdfMatrix FitPage(
        double pageWidth,
        double pageHeight,
        double viewportWidth,
        double viewportHeight,
        double zoom = 1)
    {
        var mediaWidth = pageWidth > 0 ? pageWidth : 612;
        var mediaHeight = pageHeight > 0 ? pageHeight : 792;
        var paneWidth = System.Math.Max(1, viewportWidth);
        var paneHeight = System.Math.Max(1, viewportHeight);
        var fit = System.Math.Min(paneWidth / mediaWidth, paneHeight / mediaHeight);
        var scale = fit * System.Math.Clamp(zoom, 0.5, 4);
        var drawnWidth = mediaWidth * scale;
        var drawnHeight = mediaHeight * scale;
        var originX = (paneWidth - drawnWidth) / 2;
        var originY = (paneHeight - drawnHeight) / 2;
        var flip = new PdfMatrix(1, 0, 0, -1, 0, mediaHeight);
        var view = new PdfMatrix(scale, 0, 0, scale, originX, originY);
        return view.Multiply(flip);
    }

    /// <summary>Scale a page to a raster DPI without letterboxing.</summary>
    public static PdfMatrix Raster(double pageHeight, double dotsPerInch)
    {
        var scale = System.Math.Max(0.01, dotsPerInch / 72d);
        var flip = new PdfMatrix(1, 0, 0, -1, 0, pageHeight);
        return new PdfMatrix(scale, 0, 0, scale, 0, 0).Multiply(flip);
    }

    /// <summary>Fit the page width to the viewport; height may extend past the pane.</summary>
    public static PdfMatrix FitWidth(
        double pageWidth,
        double pageHeight,
        double viewportWidth,
        double zoom = 1)
    {
        var mediaWidth = pageWidth > 0 ? pageWidth : 612;
        var mediaHeight = pageHeight > 0 ? pageHeight : 792;
        var scale = System.Math.Max(1, viewportWidth) / mediaWidth * System.Math.Clamp(zoom, 0.5, 4);
        var flip = new PdfMatrix(1, 0, 0, -1, 0, mediaHeight);
        return new PdfMatrix(scale, 0, 0, scale, 0, 0).Multiply(flip);
    }

    /// <summary>Map the page onto an exact device rectangle (continuous-stack cells).</summary>
    public static PdfMatrix Stretch(
        double pageWidth,
        double pageHeight,
        double deviceWidth,
        double deviceHeight)
    {
        var mediaWidth = pageWidth > 0 ? pageWidth : 612;
        var mediaHeight = pageHeight > 0 ? pageHeight : 792;
        var scaleX = System.Math.Max(1, deviceWidth) / mediaWidth;
        var scaleY = System.Math.Max(1, deviceHeight) / mediaHeight;
        var flip = new PdfMatrix(1, 0, 0, -1, 0, mediaHeight);
        return new PdfMatrix(scaleX, 0, 0, scaleY, 0, 0).Multiply(flip);
    }
}
