namespace Novolis.Pdf.Parsing;

/// <summary>Typed view over a page resource dictionary.</summary>
public sealed class PdfResourceDictionary
{
    private readonly PdfParsedDocument _document;

    /// <summary>Creates a resource-dictionary view.</summary>
    public PdfResourceDictionary(PdfParsedDocument document, PdfDictionaryObject dictionary)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dictionary);
        _document = document;
        Dictionary = dictionary;
    }

    /// <summary>Underlying resource dictionary.</summary>
    public PdfDictionaryObject Dictionary { get; }

    /// <summary>Resolves a font by its resource name.</summary>
    public PdfFontResource? ResolveFont(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
            return null;

        var fonts = _document.ResolveDictionary(Dictionary.Get(PdfNames.Font))
            ?? Dictionary.GetDictionary(PdfNames.Font);
        var font = _document.ResolveDictionary(fonts?.Get(fontName));
        return font is null ? null : new PdfFontResource(_document, font);
    }

    /// <summary>Resolves an XObject stream by resource name.</summary>
    public PdfStreamObject? ResolveXObject(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var xobjects = _document.ResolveDictionary(Dictionary.Get(PdfNames.XObject))
            ?? Dictionary.GetDictionary(PdfNames.XObject);
        return _document.ResolveStream(xobjects?.Get(name));
    }
}
