using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Rendering;

/// <summary>Decoded image samples and the PDF image-space transform.</summary>
public sealed record PdfImageCommand(
    byte[] Samples,
    int Width,
    int Height,
    int BitsPerComponent,
    PdfImagePixelFormat PixelFormat,
    PdfMatrix Matrix) : PdfDrawCommand;
