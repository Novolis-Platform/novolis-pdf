namespace Novolis.Pdf.Abstractions;

/// <summary>Stable page navigation state for a reader host.</summary>
public sealed record PdfPageNavigation(
    int PageIndex,
    double Zoom = 1,
    int Rotation = 0,
    double HorizontalOffset = 0,
    double VerticalOffset = 0)
{
    /// <summary>Returns a validated navigation state.</summary>
    public PdfPageNavigation Validate(int pageCount)
    {
        if (pageCount < 0)
            throw new ArgumentOutOfRangeException(nameof(pageCount));
        if (PageIndex < 0 || (pageCount > 0 && PageIndex >= pageCount))
            throw new ArgumentOutOfRangeException(nameof(PageIndex));
        if (!double.IsFinite(Zoom) || Zoom <= 0)
            throw new ArgumentOutOfRangeException(nameof(Zoom));
        if (!double.IsFinite(HorizontalOffset) || !double.IsFinite(VerticalOffset))
            throw new ArgumentOutOfRangeException(nameof(HorizontalOffset));

        return this with { Rotation = ((Rotation % 360) + 360) % 360 };
    }
}
