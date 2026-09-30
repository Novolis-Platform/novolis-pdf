namespace Novolis.Pdf.Abstractions;

/// <summary>Requested page output scale and quality.</summary>
public sealed record PdfRenderRequest(
    int PageIndex,
    double DotsPerInch = 144,
    PdfRect? Clip = null,
    bool IncludeAnnotations = true,
    int Rotation = 0)
{
    /// <summary>Validates a render request.</summary>
    public void Validate()
    {
        if (PageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(PageIndex));
        if (!double.IsFinite(DotsPerInch) || DotsPerInch is <= 0 or > 1200)
            throw new ArgumentOutOfRangeException(nameof(DotsPerInch));
        if (Clip is { } clip
            && (!double.IsFinite(clip.Left)
                || !double.IsFinite(clip.Bottom)
                || !double.IsFinite(clip.Right)
                || !double.IsFinite(clip.Top)
                || clip.Width <= 0
                || clip.Height <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(Clip));
        }
        if (Rotation % 90 != 0)
            throw new ArgumentOutOfRangeException(nameof(Rotation), "PDF rotation must be a multiple of 90 degrees.");
    }
}
