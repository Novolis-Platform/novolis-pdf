using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;
using System.Runtime.CompilerServices;

namespace Novolis.Pdf.Text;

/// <summary>Extracts outlines and link annotations from a parsed PDF.</summary>
public sealed class PdfNavigationExtractor
{
    /// <summary>Extracts top-level and nested outline entries.</summary>
    public IReadOnlyList<PdfOutlineItem> ExtractOutlines(PdfParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var outlines = document.GetCatalog()?.ResolveOutlines();
        if (outlines is null)
            return [];

        var pages = PdfPageTree.Resolve(document);
        var result = new List<PdfOutlineItem>();
        var current = outlines.ResolveFirst();
        var visited = new HashSet<int>();
        while (current is not null && result.Count < 10_000)
        {
            var item = ReadOutlineItem(document, pages, current, visited);
            if (item is not null)
                result.Add(item);
            current = current.ResolveNext();
        }

        return result;
    }

    /// <summary>Extracts link annotations from every page.</summary>
    public IReadOnlyList<PdfLink> ExtractLinks(PdfParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var pages = PdfPageTree.Resolve(document);
        var catalog = document.GetCatalog();
        var links = new List<PdfLink>();
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var page = new PdfPageNode(document, pages[pageIndex].Dictionary, pages[pageIndex].ObjectNumber);
            foreach (var annotation in page.ResolveAnnotations())
            {
                if (!annotation.IsLink || annotation.Rectangle is not { } bounds)
                    continue;

                var destination = annotation.ResolveDestination();
                var namedDestination = destination is PdfStringObject named
                    ? named.GetText()
                    : null;
                if (namedDestination is not null)
                    destination = catalog?.ResolveNamedDestination(namedDestination);
                var targetPageIndex = ResolveTargetPageIndex(document, pages, catalog, destination);
                links.Add(new PdfLink(
                    pageIndex,
                    bounds,
                    annotation.ResolveUri(),
                    targetPageIndex,
                    namedDestination));
            }
        }

        return links;
    }

    private static PdfOutlineItem? ReadOutlineItem(
        PdfParsedDocument document,
        IReadOnlyList<PdfResolvedPage> pages,
        PdfOutlineNode item,
        ISet<int> visited)
    {
        var title = item.Title;
        if (string.IsNullOrWhiteSpace(title))
            return null;
        if (!visited.Add(RuntimeHelpers.GetHashCode(item.Dictionary)))
            return null;

        var catalog = document.GetCatalog();
        var destination = document.Resolve(item.Destination);
        var namedDestination = destination is PdfStringObject named
            ? named.GetText()
            : null;
        if (namedDestination is not null)
            destination = catalog?.ResolveNamedDestination(namedDestination);
        var pageIndex = ResolveTargetPageIndex(document, pages, catalog, destination);
        var children = new List<PdfOutlineItem>();
        var child = item.ResolveFirst();
        while (child is not null && children.Count < 10_000)
        {
            var childItem = ReadOutlineItem(document, pages, child, visited);
            if (childItem is not null)
                children.Add(childItem);
            child = child.ResolveNext();
        }

        return new PdfOutlineItem(
            title,
            pageIndex,
            children,
            IsOpen: item.Count > 0,
            NamedDestination: namedDestination);
    }

    private static int? ResolveTargetPageIndex(
        PdfParsedDocument document,
        IReadOnlyList<PdfResolvedPage> pages,
        PdfCatalog? catalog,
        PdfObject? destination)
    {
        var resolvedDestination = document.Resolve(destination);
        if (resolvedDestination is PdfStringObject named)
            resolvedDestination = catalog?.ResolveNamedDestination(named.GetText());
        if (resolvedDestination is not PdfArrayObject array || array.Items.Count == 0)
            return null;
        var targetReference = array.Items[0] as PdfIndirectReference;
        var target = document.ResolveDictionary(array.Items[0]);
        if (target is null)
            return null;
        for (var index = 0; index < pages.Count; index++)
        {
            if (targetReference?.ObjectNumber == pages[index].ObjectNumber
                || Equals(pages[index].Dictionary, target))
                return index;
        }

        return null;
    }
}
