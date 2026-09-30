using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;

namespace Novolis.Pdf.Rendering;

/// <summary>Resolved page dictionary and stream content.</summary>
public sealed record PdfResolvedPage(
    PdfPageInfo Info,
    PdfDictionaryObject Dictionary,
    IReadOnlyList<PdfStreamObject> Contents,
    PdfDictionaryObject? Resources = null,
    int? ObjectNumber = null);
