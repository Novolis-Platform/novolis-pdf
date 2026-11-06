using System.Security.Cryptography;
using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>
/// Builds a map content frame from image tiles that are byte-identical across two page plans
/// (same pixel size + sample hash). Use this when two Norgeskart prints share the same map rasters.
/// </summary>
public static class PdfSharedImageFrame
{
    /// <summary>
    /// Builds a content frame from placements of images whose sample payloads match across both plans.
    /// Throws if no shared raster tiles exist.
    /// </summary>
    public static PdfMapContentFrame FromMatchingImages(PdfPageRenderPlan first, PdfPageRenderPlan second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var secondByHash = new Dictionary<string, List<PdfImageCommand>>(StringComparer.Ordinal);
        foreach (var command in second.Commands)
        {
            if (command is not PdfImageCommand image || image.IsImageMask)
                continue;
            var key = ContentKey(image);
            if (!secondByHash.TryGetValue(key, out var list))
                secondByHash[key] = list = [];
            list.Add(image);
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var matched = 0;
        foreach (var command in first.Commands)
        {
            if (command is not PdfImageCommand image || image.IsImageMask)
                continue;
            var key = ContentKey(image);
            if (!secondByHash.ContainsKey(key))
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
                "The two PDFs do not share any identical embedded image payloads to define a common map frame.");

        return new PdfMapContentFrame(minX, minY, maxX, maxY);
    }

    /// <summary>Builds a stable key from pixel size plus SHA-256 of samples or encoded bytes.</summary>
    public static string ContentKey(PdfImageCommand image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var payload = image.EncodedImage is { Length: > 0 } encoded
            ? encoded
            : image.Samples;
        var hash = Convert.ToHexString(SHA256.HashData(payload));
        return $"{image.Width}x{image.Height}:{hash}";
    }
}
