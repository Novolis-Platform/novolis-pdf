using System.Runtime.InteropServices;
using Novolis.Pdf.Rendering;
using SkiaSharp;

namespace Novolis.Pdf.Rendering.Skia;

/// <summary>
/// Decodes interpreted PDF image commands into Skia bitmaps.
/// Sample row 0 stays at bitmap row 0 (PDF / Acrobat top-row-first). The PDF unit-square
/// Y convention is applied in <see cref="PdfSkiaPageRenderer"/> when painting, not here.
/// </summary>
public static class PdfSkiaImageDecoder
{
    /// <summary>Builds an RGBA bitmap from decoded image samples or embedded DCT, applying soft masks.</summary>
    public static SKBitmap Decode(PdfImageCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        SKBitmap bitmap;
        if (command.EncodedImage is { Length: > 0 })
        {
            bitmap = SKBitmap.Decode(command.EncodedImage)
                ?? throw new InvalidDataException("The embedded PDF image could not be decoded.");
            if (bitmap.Width != command.Width || bitmap.Height != command.Height)
            {
                using var wrong = bitmap;
                throw new InvalidDataException(
                    $"Embedded image decoded to {bitmap.Width}×{bitmap.Height}, expected {command.Width}×{command.Height}.");
            }

            if (command.SoftMaskSamples is null)
                return EnsureRgba(bitmap);

            using (bitmap)
                return ApplySoftMask(EnsureRgba(bitmap), command.SoftMaskSamples, command.Width, command.Height);
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

        for (var y = 0; y < command.Height; y++)
        {
            for (var x = 0; x < command.Width; x++)
            {
                var sample = ((y * command.Width) + x) * components;
                var destination = ((y * command.Width) + x) * 4;
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
                var alpha = SoftMaskAlpha(command.SoftMaskSamples, x, y, command.Width);
                rgba[destination] = red;
                rgba[destination + 1] = green;
                rgba[destination + 2] = blue;
                rgba[destination + 3] = alpha;
            }
        }

        bitmap = new SKBitmap(
            new SKImageInfo(
                command.Width,
                command.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Unpremul));
        Marshal.Copy(rgba, 0, bitmap.GetPixels(), rgba.Length);
        return bitmap;
    }

    private static SKBitmap EnsureRgba(SKBitmap source)
    {
        if (source.ColorType == SKColorType.Rgba8888 && source.AlphaType != SKAlphaType.Opaque)
            return source;

        var copy = new SKBitmap(
            new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(copy);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        source.Dispose();
        return copy;
    }

    private static SKBitmap ApplySoftMask(SKBitmap rgba, byte[] softMask, int width, int height)
    {
        var info = rgba.Info;
        var pixels = new byte[info.BytesSize];
        Marshal.Copy(rgba.GetPixels(), pixels, 0, pixels.Length);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                pixels[(i * 4) + 3] = i < softMask.Length ? softMask[i] : byte.MaxValue;
            }
        }

        var result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        Marshal.Copy(pixels, 0, result.GetPixels(), pixels.Length);
        rgba.Dispose();
        return result;
    }

    private static byte SoftMaskAlpha(byte[]? softMask, int x, int y, int width)
    {
        if (softMask is null)
            return byte.MaxValue;
        var index = (y * width) + x;
        return index < softMask.Length ? softMask[index] : byte.MaxValue;
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
