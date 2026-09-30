using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;

namespace Novolis.Pdf.Rendering;

/// <summary>Retains per-page display lists so zoom and pan only change the view matrix.</summary>
public sealed class PdfPagePlanCache
{
    private readonly Dictionary<int, PdfPageRenderPlan> _plans = [];
    private PdfParsedDocument? _document;

    /// <summary>Returns a cached plan, building it once per page of the current document.</summary>
    public PdfPageRenderPlan Get(
        PdfParsedDocument document,
        PdfResolvedPage page,
        PdfLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        if (!ReferenceEquals(_document, document))
        {
            _plans.Clear();
            _document = document;
        }

        if (_plans.TryGetValue(page.Info.Index, out var plan))
            return plan;

        plan = PdfContentStreamInterpreter.BuildPlan(document, page, limits);
        _plans[page.Info.Index] = plan;
        return plan;
    }

    /// <summary>Drops retained plans.</summary>
    public void Clear()
    {
        _plans.Clear();
        _document = null;
    }
}
