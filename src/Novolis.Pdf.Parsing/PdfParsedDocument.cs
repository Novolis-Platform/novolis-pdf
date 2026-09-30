using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Parsing;

/// <summary>Parsed object graph and document metadata.</summary>
public sealed class PdfParsedDocument
{
    private readonly IReadOnlyDictionary<int, PdfIndirectObject> _objects;

    /// <summary>Creates a parsed document.</summary>
    public PdfParsedDocument(
        PdfDocumentInfo info,
        IReadOnlyDictionary<int, PdfIndirectObject> objects,
        PdfDictionaryObject? trailer,
        IReadOnlyList<PdfDiagnosticEntry> diagnostics)
    {
        Info = info;
        _objects = objects;
        Trailer = trailer;
        Diagnostics = diagnostics;
    }

    /// <summary>Document metadata.</summary>
    public PdfDocumentInfo Info { get; }

    /// <summary>Trailer dictionary when one was available.</summary>
    public PdfDictionaryObject? Trailer { get; }

    /// <summary>Typed trailer view when a trailer dictionary was available.</summary>
    public PdfTrailer? GetTrailer() =>
        Trailer is null ? null : new PdfTrailer(this, Trailer);

    /// <summary>Typed catalog view when the trailer root resolves to a dictionary.</summary>
    public PdfCatalog? GetCatalog() => GetTrailer()?.ResolveCatalog();

    /// <summary>Tries to obtain a typed catalog without exposing an unchecked cast.</summary>
    public bool TryGetCatalog(out PdfCatalog catalog)
    {
        catalog = GetCatalog()!;
        return catalog is not null;
    }

    /// <summary>Reader diagnostics.</summary>
    public IReadOnlyList<PdfDiagnosticEntry> Diagnostics { get; }

    /// <summary>Parsed indirect objects keyed by object number.</summary>
    public IReadOnlyDictionary<int, PdfIndirectObject> Objects => _objects;

    /// <summary>Gets an object, resolving one indirect reference.</summary>
    public PdfObject? Resolve(PdfObject? value)
    {
        return Resolve(value, maximumDepth: 32);
    }

    /// <summary>Gets an object, resolving a bounded chain of indirect references.</summary>
    public PdfObject? Resolve(PdfObject? value, int maximumDepth)
    {
        if (maximumDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));

        var current = value;
        var visited = new HashSet<(int ObjectNumber, int Generation)>();
        for (var depth = 0; depth <= maximumDepth; depth++)
        {
            if (current is not PdfIndirectReference reference)
                return current;
            if (!visited.Add((reference.ObjectNumber, reference.Generation)))
                return null;
            if (!_objects.TryGetValue(reference.ObjectNumber, out var indirect)
                || indirect.Generation != reference.Generation)
                return null;
            current = indirect.Value;
        }

        return null;
    }

    /// <summary>Resolves and casts a PDF object without exposing an unchecked cast.</summary>
    public bool TryResolve<T>(PdfObject? value, out T? resolved)
        where T : PdfObject
    {
        resolved = Resolve(value) as T;
        return resolved is not null;
    }

    /// <summary>Gets a dictionary, resolving one indirect reference.</summary>
    public PdfDictionaryObject? ResolveDictionary(PdfObject? value) =>
        Resolve(value) as PdfDictionaryObject;

    /// <summary>Gets an array, resolving one indirect reference.</summary>
    public PdfArrayObject? ResolveArray(PdfObject? value) =>
        Resolve(value) as PdfArrayObject;

    /// <summary>Gets a stream, resolving one indirect reference.</summary>
    public PdfStreamObject? ResolveStream(PdfObject? value) =>
        Resolve(value) as PdfStreamObject;
}
