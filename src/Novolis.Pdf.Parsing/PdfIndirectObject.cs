namespace Novolis.Pdf.Parsing;

/// <summary>An indirect PDF object and its source location.</summary>
public sealed record PdfIndirectObject(
    int ObjectNumber,
    int Generation,
    PdfObject Value,
    long Offset);
