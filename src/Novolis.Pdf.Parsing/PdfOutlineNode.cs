namespace Novolis.Pdf.Parsing;

/// <summary>Typed view over an outline item or outline root.</summary>
public sealed class PdfOutlineNode
{
    private readonly PdfParsedDocument _document;

    /// <summary>Creates an outline node view.</summary>
    public PdfOutlineNode(PdfParsedDocument document, PdfDictionaryObject dictionary)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dictionary);
        _document = document;
        Dictionary = dictionary;
    }

    /// <summary>Underlying outline dictionary.</summary>
    public PdfDictionaryObject Dictionary { get; }

    /// <summary>Outline title.</summary>
    public string? Title => Dictionary.GetString(PdfNames.Title)?.GetText();

    /// <summary>Explicit destination value.</summary>
    public PdfObject? Destination => Dictionary.Get(PdfNames.Dest);

    /// <summary>Signed child count; positive means the item starts open.</summary>
    public int Count => Dictionary.TryGetInteger(PdfNames.Count, out var count) ? count : 0;

    /// <summary>Resolves the first child outline item.</summary>
    public PdfOutlineNode? ResolveFirst()
    {
        var dictionary = _document.ResolveDictionary(Dictionary.Get(PdfNames.First));
        return dictionary is null ? null : new PdfOutlineNode(_document, dictionary);
    }

    /// <summary>Resolves the next sibling outline item.</summary>
    public PdfOutlineNode? ResolveNext()
    {
        var dictionary = _document.ResolveDictionary(Dictionary.Get(PdfNames.Next));
        return dictionary is null ? null : new PdfOutlineNode(_document, dictionary);
    }
}
