namespace Novolis.Pdf.Parsing;

/// <summary>Typed view over a PDF trailer dictionary.</summary>
public sealed class PdfTrailer
{
    private readonly PdfParsedDocument _document;

    /// <summary>Creates a trailer view.</summary>
    public PdfTrailer(PdfParsedDocument document, PdfDictionaryObject dictionary)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dictionary);
        _document = document;
        Dictionary = dictionary;
    }

    /// <summary>Underlying trailer dictionary.</summary>
    public PdfDictionaryObject Dictionary { get; }

    /// <summary>Catalog indirect reference.</summary>
    public PdfIndirectReference? RootReference => Dictionary.GetReference(PdfNames.Root);

    /// <summary>Information-dictionary reference or value.</summary>
    public PdfObject? Info => Dictionary.Get(PdfNames.Info);

    /// <summary>Previous xref offset when present.</summary>
    public long? PreviousOffset => Dictionary.TryGetInteger(PdfNames.Prev, out var previous)
        ? previous
        : null;

    /// <summary>Declared object count.</summary>
    public int? Size => Dictionary.TryGetInteger(PdfNames.Size, out var size) ? size : null;

    /// <summary>Resolves the document catalog.</summary>
    public PdfCatalog? ResolveCatalog()
    {
        var dictionary = _document.ResolveDictionary(Dictionary.Get(PdfNames.Root));
        return dictionary is null ? null : new PdfCatalog(_document, dictionary);
    }

    /// <summary>Resolves the information dictionary.</summary>
    public PdfInfoDictionary? ResolveInfo()
    {
        var dictionary = _document.ResolveDictionary(Info);
        return dictionary is null ? null : new PdfInfoDictionary(dictionary);
    }
}
