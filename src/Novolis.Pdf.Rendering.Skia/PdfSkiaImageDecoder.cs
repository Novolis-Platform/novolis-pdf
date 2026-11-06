using System.Runtime.InteropServices;
using Novolis.Pdf.Rendering;
using SkiaSharp;

namespace Novolis.Pdf.Rendering.Skia;

/// <summary>Decodes interpreted PDF image commands into Skia bitmaps.</summary>
public static class PdfSkiaImageDecoder
{
    /// <summary>Builds an RGBA bitmap from decoded image samples or embedded DCT.</summary>
    public static SKBitmap Decode(PdfImageCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.EncodedImage is { Length: > 0 })
        {
            var encoded = SKBitmap.Decode(command.EncodedImage);
            return encoded
                ?? throw new InvalidDataException("The embedded PDF image could not be decoded.");
        }

        var byteCount = checked(command.Width * command.Height * 4);
        var rgba = new byte[byteCount];
        var components = command.Palette is not null
            ? 1
            : command.PixelFormat switch
            {
                PdfImagePixelFormat.Gray => 1,
                PdfImagePixelFormat.Rgb => 3,
                PdfImagePixelFormat.Cmyk => 4,
                _ => throw new InvalidOperationException("Unsupported PDF image pixel format."),
            };

        for (var sourceY = 0; sourceY < command.Height; sourceY++)
        {
            var targetY = command.Height - 1 - sourceY;
            for (var x = 0; x < command.Width; x++)
            {
                var sample = ((sourceY * command.Width) + x) * components;
                var destination = ((targetY * command.Width) + x) * 4;
                var (red, green, blue) = command.Palette is { Length: > 0 } palette
                    ? IndexedToRgb(
                        ReadSample(command.Samples, sample, command.BitsPerComponent, command.InvertSamples),
                        palette)
                    : command.PixelFormat switch
                    {
                        PdfImagePixelFormat.Gray =>
                            (ReadSample(command.Samples, sample, command.BitsPerComponent, command.InvertSamples),
                             ReadSample(command.Samples, sample, command.BitsPerComponent, command.InvertSamples),
                             ReadSample(command.Samples, sample, command.BitsPerComponent, command.InvertSamples)),
                        PdfImagePixelFormat.Rgb =>
                            (ReadSample(command.Samples, sample, command.BitsPerComponent, command.InvertSamples),
                             ReadSample(command.Samples, sample + 1, command.BitsPerComponent, command.InvertSamples),
                             ReadSample(command.Samples, sample + 2, command.BitsPerComponent, command.InvertSamples)),
                        PdfImagePixelFormat.Cmyk => CmykToRgb(
                            ReadSample(command.Samples, sample, command.BitsPerComponent, command.InvertSamples),
                            ReadSample(command.Samples, sample + 1, command.BitsPerComponent, command.InvertSamples),
                            ReadSample(command.Samples, sample + 2, command.BitsPerComponent, command.InvertSamples),
                            ReadSample(command.Samples, sample + 3, command.BitsPerComponent, command.InvertSamples)),
                        _ => throw new InvalidOperationException("Unsupported PDF image pixel format."),
                    };
                rgba[destination] = red;
                rgba[destination + 1] = green;
                rgba[destination + 2] = blue;
                rgba[destination + 3] = byte.MaxValue;
            }
        }

        var bitmap = new SKBitmap(
            new SKImageInfo(
                command.Width,
                command.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul));
        Marshal.Copy(rgba, 0, bitmap.GetPixels(), rgba.Length);
        return bitmap;
    }

    private static (byte Red, byte Green, byte Blue) IndexedToRgb(byte index, byte[] palette)
    {
        var offset = System.Math.Min(index * 3, System.Math.Max(0, palette.Length - 3));
        return (palette[offset], palette[offset + 1], palette[offset + 2]);
    }

    private static byte ReadSample(byte[] samples, int componentIndex, int bitsPerComponent, bool invert)
    {
        if (bitsPerComponent == 8)
        {
            var sample = componentIndex < samples.Length ? samples[componentIndex] : (byte)0;
            return invert ? (byte)(byte.MaxValue - sample) : sample;
        }

        var bitIndex = checked(componentIndex * bitsPerComponent);
        var byteIndex = bitIndex / 8;
        var shift = 8 - bitsPerComponent - (bitIndex % 8);
        if (byteIndex >= samples.Length || shift < 0)
            return 0;
        var mask = (1 << bitsPerComponent) - 1;
        var value = (samples[byteIndex] >> shift) & mask;
        var result = (byte)(value * byte.MaxValue / mask);
        return invert ? (byte)(byte.MaxValue - result) : result;
    }

    private static (byte Red, byte Green, byte Blue) CmykToRgb(byte cyan, byte magenta, byte yellow, byte black)
    {
        var c = cyan / 255d;
        var m = magenta / 255d;
        var y = yellow / 255d;
        var k = black / 255d;
        return (
            (byte)System.Math.Round(255 * (1 - System.Math.Min(1, c + k))),
            (byte)System.Math.Round(255 * (1 - System.Math.Min(1, m + k))),
            (byte)System.Math.Round(255 * (1 - System.Math.Min(1, y + k))));
    }
}
