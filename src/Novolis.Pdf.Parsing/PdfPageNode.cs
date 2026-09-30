namespace Novolis.Pdf.Parsing;

/// <summary>Typed view over a /Pages or /Page dictionary.</summary>
public sealed class PdfPageNode
{
    private readonly PdfParsedDocument _document;

    /// <summary>Creates a page-tree node view.</summary>
    public PdfPageNode(
        PdfParsedDocument document,
        PdfDictionaryObject dictionary,
        int? objectNumber = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dictionary);
        _document = document;
        Dictionary = dictionary;
        ObjectNumber = objectNumber;
    }

    /// <summary>Underlying page or pages dictionary.</summary>
    public PdfDictionaryObject Dictionary { get; }

    /// <summary>Indirect object number when the node was reached through a reference.</summary>
    public int? ObjectNumber { get; }

    /// <summary>Declared node type.</summary>
    public PdfName? Type => Dictionary.GetName(PdfNames.Type);

    /// <summary>Whether the node is a leaf page.</summary>
    public bool IsPage => Type == PdfNames.Page;

    /// <summary>Whether the node is an intermediate pages node.</summary>
    public bool IsPages => Type == PdfNames.Pages;

    /// <summary>Inherited or local media box.</summary>
    public PdfObject? MediaBox => Dictionary.Get(PdfNames.MediaBox);

    /// <summary>Inherited or local crop box.</summary>
    public PdfObject? CropBox => Dictionary.Get(PdfNames.CropBox);

    /// <summary>Page contents.</summary>
    public PdfObject? Contents => Dictionary.Get(PdfNames.Contents);

    /// <summary>Page or node resources.</summary>
    public PdfObject? Resources => Dictionary.Get(PdfNames.Resources);

    /// <summary>Declared rotation in degrees.</summary>
    public int Rotation => Dictionary.TryGetInteger(PdfNames.Rotate, out var rotation)
        ? rotation
        : 0;

    /// <summary>Declared user unit, defaulting to 1.</summary>
    public double UserUnit => Dictionary.GetNumber(PdfNames.UserUnit) ?? 1;

    /// <summary>Resolves page-tree children in document order.</summary>
    public IReadOnlyList<PdfPageNode> ResolveKids()
    {
        var kids = _document.ResolveArray(Dictionary.Get(PdfNames.Kids));
        if (kids is null)
            return [];

        var nodes = new List<PdfPageNode>(kids.Items.Count);
        foreach (var child in kids.Items)
        {
            var dictionary = _document.ResolveDictionary(child);
            if (dictionary is null)
                continue;
            var objectNumber = child is PdfIndirectReference reference
                ? reference.ObjectNumber
                : (int?)null;
            nodes.Add(new PdfPageNode(_document, dictionary, objectNumber));
        }

        return nodes;
    }

    /// <summary>Resolves annotation dictionaries on a leaf page.</summary>
    public IReadOnlyList<PdfAnnotation> ResolveAnnotations()
    {
        var annotations = _document.ResolveArray(Dictionary.Get(PdfNames.Annots));
        if (annotations is null)
            return [];

        var result = new List<PdfAnnotation>(annotations.Items.Count);
        foreach (var item in annotations.Items)
        {
            var dictionary = _document.ResolveDictionary(item);
            if (dictionary is not null)
                result.Add(new PdfAnnotation(_document, dictionary));
        }

        return result;
    }
}
