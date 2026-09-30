namespace Novolis.Pdf.Text;

/// <summary>Decoded PDF text and its estimated page-space advance.</summary>
public sealed record PdfDecodedText(string Text, double Advance, bool FromToUnicode = false);
