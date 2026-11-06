using System.Globalization;
using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>
/// Builds a map content frame from image tiles that share the same page placement across two plans.
/// Norgeskart exports often reuse the same strip layout even when sample bytes differ slightly.
/// </summary>
public static class PdfSharedImageFrame
{
    /// <summary>
    /// Builds a content frame from placements that exist in both plans.
    /// Throws if no shared placements exist.
    /// </summary>
    public static PdfMapContentFrame FromMatchingImages(PdfPageRenderPlan first, PdfPageRenderPlan second)
    {
        var keys = MatchingPlacementKeys(first, second);
        if (keys.Count == 0)
            throw new InvalidDataException(
                "The two PDFs do not share any image placements to define a common map frame.");

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var matched = 0;
        foreach (var command in first.Commands)
        {
            if (command is not PdfImageCommand image || image.IsImageMask)
                continue;
            if (!keys.Contains(PlacementKey(image)))
                continue;

            matched++;
            var m = image.Matrix;
            minX = System.Math.Min(minX, m.E);
            minY = System.Math.Min(minY, m.F);
            maxX = System.Math.Max(maxX, m.E + m.A);
            maxY = System.Math.Max(maxY, m.F + m.D);
        }

        if (matched == 0 || minX >= maxX || minY >= maxY)
            throw new InvalidDataException(
                "Shared placements were found, but no usable image rects define a map frame.");

        return new PdfMapContentFrame(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Placement keys of non-mask images that occupy the same page rectangle in both plans
    /// (shared Norgeskart strip layout). Relief-only portrait tiles are excluded.
    /// </summary>
    public static HashSet<string> MatchingPlacementKeys(PdfPageRenderPlan first, PdfPageRenderPlan second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var secondKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in second.Commands)
        {
            if (command is not PdfImageCommand image || image.IsImageMask)
                continue;
            secondKeys.Add(PlacementKey(image));
        }

        var shared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in first.Commands)
        {
            if (command is not PdfImageCommand image || image.IsImageMask)
                continue;
            var key = PlacementKey(image);
            if (secondKeys.Contains(key))
                shared.Add(key);
        }

        return shared;
    }

    /// <summary>Stable key from pixel size plus CTM placement (points, 2 decimal places).</summary>
    public static string PlacementKey(PdfImageCommand image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var m = image.Matrix;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{image.Width}x{image.Height}@{m.E:F2},{m.F:F2},{m.A:F2},{m.D:F2}");
    }

    /// <summary>Legacy alias — placement keys are what the map pipeline filters on.</summary>
    public static string ContentKey(PdfImageCommand image) => PlacementKey(image);

    /// <summary>Legacy alias for <see cref="MatchingPlacementKeys"/>.</summary>
    public static HashSet<string> MatchingContentKeys(PdfPageRenderPlan first, PdfPageRenderPlan second) =>
        MatchingPlacementKeys(first, second);
}
