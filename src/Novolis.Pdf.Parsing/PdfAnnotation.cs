using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Parsing;

/// <summary>Typed view over a page annotation dictionary.</summary>
public sealed class PdfAnnotation
{
    private readonly PdfParsedDocument _document;

    /// <summary>Creates an annotation view.</summary>
    public PdfAnnotation(PdfParsedDocument document, PdfDictionaryObject dictionary)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dictionary);
        _document = document;
        Dictionary = dictionary;
    }

    /// <summary>Underlying annotation dictionary.</summary>
    public PdfDictionaryObject Dictionary { get; }

    /// <summary>Annotation subtype.</summary>
    public PdfName? Subtype => Dictionary.GetName(PdfNames.Subtype);

    /// <summary>Whether the annotation is a link.</summary>
    public bool IsLink => Subtype == PdfNames.Link;

    /// <summary>Annotation rectangle when four numbers are present.</summary>
    public PdfRect? Rectangle =>
        PdfRectangleReader.TryRead(Dictionary.Get(PdfNames.Rect), out var rectangle)
            ? rectangle
            : null;

    /// <summary>Explicit destination on the annotation.</summary>
    public PdfObject? Destination => Dictionary.Get(PdfNames.Dest);

    /// <summary>Resolves the annotation action dictionary.</summary>
    public PdfDictionaryObject? ResolveAction() =>
        _document.ResolveDictionary(Dictionary.Get(PdfNames.Action));

    /// <summary>Resolves a URI action target.</summary>
    public string? ResolveUri()
    {
        var action = ResolveAction();
        return _document.Resolve(action?.Get(PdfNames.Uri)) is PdfStringObject uri
            ? uri.GetText()
            : null;
    }

    /// <summary>Resolves the most specific destination object.</summary>
    public PdfObject? ResolveDestination()
    {
        var action = ResolveAction();
        return _document.Resolve(Destination)
            ?? _document.Resolve(action?.Get(PdfNames.Destination));
    }
}
