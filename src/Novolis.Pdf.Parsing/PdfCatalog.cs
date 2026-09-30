namespace Novolis.Pdf.Parsing;

/// <summary>Typed view over the PDF catalog dictionary.</summary>
public sealed class PdfCatalog
{
    private readonly PdfParsedDocument _document;

    /// <summary>Creates a catalog view.</summary>
    public PdfCatalog(PdfParsedDocument document, PdfDictionaryObject dictionary)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dictionary);
        _document = document;
        Dictionary = dictionary;
    }

    /// <summary>Underlying catalog dictionary.</summary>
    public PdfDictionaryObject Dictionary { get; }

    /// <summary>Catalog type name.</summary>
    public PdfName? Type => Dictionary.GetName(PdfNames.Type);

    /// <summary>Whether the dictionary declares /Type /Catalog.</summary>
    public bool IsCatalog => Type == PdfNames.Catalog;

    /// <summary>Pages-tree indirect reference.</summary>
    public PdfIndirectReference? PagesReference => Dictionary.GetReference(PdfNames.Pages);

    /// <summary>Resolves the page tree root.</summary>
    public PdfPageNode? ResolvePages()
    {
        var dictionary = _document.ResolveDictionary(Dictionary.Get(PdfNames.Pages));
        return dictionary is null
            ? null
            : new PdfPageNode(_document, dictionary, PagesReference?.ObjectNumber);
    }

    /// <summary>Resolves the outline root.</summary>
    public PdfOutlineNode? ResolveOutlines()
    {
        var dictionary = _document.ResolveDictionary(Dictionary.Get(PdfNames.Outlines));
        return dictionary is null ? null : new PdfOutlineNode(_document, dictionary);
    }

    /// <summary>Resolves a named destination through /Dests or the name tree.</summary>
    public PdfObject? ResolveNamedDestination(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var direct = _document.ResolveDictionary(Dictionary.Get(PdfNames.Dests))?.Get(name);
        if (direct is not null)
            return _document.Resolve(direct);

        var names = _document.ResolveDictionary(Dictionary.Get(PdfNames.Names));
        var destinationTree = _document.ResolveDictionary(names?.Get(PdfNames.Dests));
        return PdfNameTree.Find(_document, destinationTree, name);
    }
}
