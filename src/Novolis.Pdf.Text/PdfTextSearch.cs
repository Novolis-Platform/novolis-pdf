using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Text;

/// <summary>Searches extracted page text locally.</summary>
public sealed class PdfTextSearch
{
    /// <summary>Searches spans using ordinal or case-insensitive matching.</summary>
    public IReadOnlyList<PdfSearchMatch> Search(
        IEnumerable<PdfTextSpan> spans,
        string query,
        bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var matches = new List<PdfSearchMatch>();
        foreach (var span in spans)
        {
            for (var start = span.Text.IndexOf(query, comparison);
                 start >= 0;
                 start = span.Text.IndexOf(query, start + query.Length, comparison))
            {
                matches.Add(new PdfSearchMatch(
                    span.PageIndex,
                    query,
                    span.Text,
                    span.Bounds,
                    start));
            }
        }

        return matches;
    }
}
