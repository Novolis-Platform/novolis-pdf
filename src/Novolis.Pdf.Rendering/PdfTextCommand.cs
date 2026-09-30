using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>Text drawing command in page user space, with PDF text-rendering matrices.</summary>
public sealed record PdfTextCommand(
    string Text,
    PdfPoint Origin,
    double FontSize,
    PdfColor Color,
    string? FontName = null,
    byte[]? EncodedBytes = null,
    PdfMatrix Ctm = default,
    PdfMatrix TextMatrix = default) : PdfDrawCommand
{
    /// <summary>Trm = Tfs × Tm × CTM, matching PDFium / ISO 32000.</summary>
    public PdfMatrix TextRenderingMatrix
    {
        get
        {
            var ctm = Ctm == default ? PdfMatrix.Identity : Ctm;
            var text = TextMatrix == default ? PdfMatrix.Identity : TextMatrix;
            var size = FontSize > 0 ? FontSize : 1;
            return ctm.Multiply(text.Multiply(new PdfMatrix(size, 0, 0, size, 0, 0)));
        }
    }
}
