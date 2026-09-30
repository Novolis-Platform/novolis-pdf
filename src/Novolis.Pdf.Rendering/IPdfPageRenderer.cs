using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;

namespace Novolis.Pdf.Rendering;

/// <summary>Renders a parsed PDF page without exposing a UI framework.</summary>
public interface IPdfPageRenderer
{
    /// <summary>Renders one page to a host-neutral encoded frame.</summary>
    ValueTask<PdfRenderedPage> RenderAsync(
        PdfParsedDocument document,
        PdfRenderRequest request,
        CancellationToken cancellationToken = default);
}
