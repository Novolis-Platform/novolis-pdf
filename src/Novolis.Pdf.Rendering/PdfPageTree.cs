using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;

namespace Novolis.Pdf.Rendering;

/// <summary>Resolves the catalog and inherited page-tree attributes.</summary>
public static class PdfPageTree
{
    /// <summary>Resolves all page dictionaries in display order.</summary>
    public static IReadOnlyList<PdfResolvedPage> Resolve(
        PdfParsedDocument document,
        PdfLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        limits ??= PdfLimits.Default;
        limits.Validate();

        var result = new List<PdfResolvedPage>();
        var visited = new HashSet<int>();
        var pages = document.GetCatalog()?.ResolvePages();
        if (pages is null)
            return result;

        Visit(
            document,
            pages,
            new PageInheritance(),
            result,
            visited,
            limits.MaximumPages);
        return result;
    }

    private static void Visit(
        PdfParsedDocument document,
        PdfPageNode node,
        PageInheritance inherited,
        ICollection<PdfResolvedPage> result,
        ISet<int> visited,
        int maximumPages)
    {
        if (result.Count >= maximumPages)
            return;

        var next = inherited.Merge(node);
        if (node.IsPage)
        {
            var mediaBox = ReadBox(next.MediaBox) ?? new PdfRect(0, 0, 612, 792);
            var cropBox = ReadBox(next.CropBox) ?? mediaBox;
            var pageIndex = result.Count;
            var info = new PdfPageInfo(
                pageIndex,
                mediaBox,
                cropBox,
                next.Rotation,
                next.UserUnit);
            result.Add(new PdfResolvedPage(
                info,
                node.Dictionary,
                ResolveContents(document, next.Contents),
                document.ResolveDictionary(next.Resources),
                node.ObjectNumber));
            return;
        }

        foreach (var child in node.ResolveKids())
        {
            if (child.ObjectNumber is { } objectNumber && !visited.Add(objectNumber))
                continue;
            Visit(
                document,
                child,
                next,
                result,
                visited,
                maximumPages);
            if (result.Count >= maximumPages)
                return;
        }
    }

    private static IReadOnlyList<PdfStreamObject> ResolveContents(
        PdfParsedDocument document,
        PdfObject? contents)
    {
        var streams = new List<PdfStreamObject>();
        switch (document.Resolve(contents))
        {
            case PdfStreamObject stream:
                streams.Add(stream);
                break;
            case PdfArrayObject array:
                foreach (var item in array.Items)
                {
                    if (document.ResolveStream(item) is { } child)
                        streams.Add(child);
                }
                break;
        }

        return streams;
    }

    private static PdfRect? ReadBox(PdfObject? value) =>
        PdfRectangleReader.TryRead(value, out var rectangle) ? rectangle : null;

    private sealed record PageInheritance(
        PdfObject? MediaBox = null,
        PdfObject? CropBox = null,
        PdfObject? Contents = null,
        PdfObject? Resources = null,
        int Rotation = 0,
        double UserUnit = 1)
    {
        public PageInheritance Merge(PdfPageNode node) =>
            new(
                node.MediaBox ?? MediaBox,
                node.CropBox ?? CropBox,
                node.Contents ?? Contents,
                node.Resources ?? Resources,
                Rotation + node.Rotation,
                node.Dictionary.GetNumber(PdfNames.UserUnit) ?? UserUnit);
    }
}
