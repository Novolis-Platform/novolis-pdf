using System.IO.Compression;
using System.Text;
using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Parsing;

/// <summary>Decodes the initial set of common PDF stream filters.</summary>
public static class PdfStreamDecoder
{
    /// <summary>Decodes a stream with the configured size limit.</summary>
    public static byte[] Decode(
        PdfStreamObject stream,
        PdfLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        limits ??= PdfLimits.Default;
        limits.Validate();

        var filters = ReadFilters(stream.Dictionary.Get(PdfNames.Filter));
        var data = stream.EncodedBytes;
        for (var index = 0; index < filters.Count; index++)
        {
            var filter = filters[index];
            data = filter switch
            {
                PdfStreamFilter.FlateDecode => Inflate(data, limits.MaximumStreamBytes),
                PdfStreamFilter.AsciiHexDecode => DecodeAsciiHex(data),
                PdfStreamFilter.Ascii85Decode => DecodeAscii85(data),
                PdfStreamFilter.RunLengthDecode => DecodeRunLength(data),
                _ => throw new PdfReaderException(new PdfDiagnosticEntry(
                    PdfDiagnosticSeverity.Warning,
                    "PDF020",
                    $"Unsupported PDF stream filter '{filter}'.")),
            };

            if (data.LongLength > limits.MaximumStreamBytes)
                throw new PdfReaderException(new PdfDiagnosticEntry(
                    PdfDiagnosticSeverity.Error,
                    "PDF021",
                    $"Decoded stream exceeded the configured limit of {limits.MaximumStreamBytes} bytes."));

            data = ApplyPredictor(
                data,
                index < filters.Count - 1
                    ? null
                    : ReadDecodeParameters(stream.Dictionary.Get(PdfNames.DecodeParms)),
                limits.MaximumStreamBytes);
        }

        return data;
    }

    private static IReadOnlyList<PdfStreamFilter> ReadFilters(PdfObject? value) =>
        value switch
        {
            PdfNameObject name => [ParseFilter(name.AsName())],
            PdfArrayObject array => array.Items
                .OfType<PdfNameObject>()
                .Select(static item => ParseFilter(item.AsName()))
                .ToArray(),
            _ => [],
        };

    private static PdfStreamFilter ParseFilter(PdfName name)
    {
        if (PdfStreamFilters.TryParse(name, out var filter))
            return filter;
        return PdfStreamFilter.Unknown;
    }

    private static byte[] Inflate(byte[] data, long maximumBytes)
    {
        using var input = new MemoryStream(data, writable: false);
        using var output = new MemoryStream();
        try
        {
            using var inflater = new ZLibStream(input, CompressionMode.Decompress, leaveOpen: true);
            CopyBounded(inflater, output, maximumBytes);
        }
        catch (InvalidDataException)
        {
            input.Position = 0;
            output.SetLength(0);
            using var inflater = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
            CopyBounded(inflater, output, maximumBytes);
        }

        return output.ToArray();
    }

    private static void CopyBounded(Stream input, Stream output, long maximumBytes)
    {
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = input.Read(buffer, 0, buffer.Length);
            if (read == 0)
                return;
            if (output.Length + read > maximumBytes)
                throw new PdfReaderException(new PdfDiagnosticEntry(
                    PdfDiagnosticSeverity.Error,
                    "PDF021",
                    $"Decoded stream exceeded the configured limit of {maximumBytes} bytes."));
            output.Write(buffer, 0, read);
        }
    }

    private static byte[] DecodeAsciiHex(byte[] data)
    {
        var output = new List<byte>(data.Length / 2);
        var high = -1;
        foreach (var value in data)
        {
            if (value is (byte)'>' )
                break;
            if (value is 0 or 9 or 10 or 12 or 13 or 32)
                continue;
            var nibble = HexValue(value);
            if (nibble < 0)
                throw new InvalidDataException("Invalid ASCII hexadecimal stream.");
            if (high < 0)
                high = nibble;
            else
            {
                output.Add((byte)((high << 4) | nibble));
                high = -1;
            }
        }

        if (high >= 0)
            output.Add((byte)(high << 4));
        return output.ToArray();
    }

    private static byte[] DecodeAscii85(byte[] data)
    {
        var output = new List<byte>();
        var group = new List<int>(5);
        for (var index = 0; index < data.Length; index++)
        {
            var value = data[index];
            if (value is (byte)'~')
                break;
            if (value is 0 or 9 or 10 or 12 or 13 or 32)
                continue;
            if (value == (byte)'z' && group.Count == 0)
            {
                output.AddRange([0, 0, 0, 0]);
                continue;
            }
            if (value is < (byte)'!' or > (byte)'u')
                throw new InvalidDataException("Invalid ASCII85 stream.");
            group.Add(value - '!');
            if (group.Count == 5)
            {
                AppendAscii85Group(output, group, 4);
                group.Clear();
            }
        }

        if (group.Count > 0)
        {
            var count = group.Count;
            while (group.Count < 5)
                group.Add(84);
            AppendAscii85Group(output, group, count - 1);
        }

        return output.ToArray();
    }

    private static void AppendAscii85Group(List<byte> output, IReadOnlyList<int> group, int byteCount)
    {
        uint value = 0;
        foreach (var digit in group)
            value = checked(value * 85 + (uint)digit);
        var bytes = new byte[4];
        for (var index = 3; index >= 0; index--)
        {
            bytes[index] = (byte)(value & 0xFF);
            value >>= 8;
        }
        output.AddRange(bytes.AsSpan(0, byteCount).ToArray());
    }

    private static byte[] DecodeRunLength(byte[] data)
    {
        var output = new List<byte>();
        var index = 0;
        while (index < data.Length)
        {
            var length = data[index++];
            if (length == 128)
                break;
            if (length <= 127)
            {
                var count = length + 1;
                if (index + count > data.Length)
                    throw new EndOfStreamException("Truncated RunLength stream.");
                output.AddRange(data.AsSpan(index, count).ToArray());
                index += count;
                continue;
            }

            var repeatCount = 257 - length;
            if (index >= data.Length)
                throw new EndOfStreamException("Truncated RunLength repeat.");
            var repeated = data[index++];
            for (var repeat = 0; repeat < repeatCount; repeat++)
                output.Add(repeated);
        }

        return output.ToArray();
    }

    private static int HexValue(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - '0',
        >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
        >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
        _ => -1,
    };

    private static PdfDictionaryObject? ReadDecodeParameters(PdfObject? value) =>
        value switch
        {
            PdfDictionaryObject dictionary => dictionary,
            PdfArrayObject array => array.Items.OfType<PdfDictionaryObject>().FirstOrDefault(),
            _ => null,
        };

    private static byte[] ApplyPredictor(
        byte[] data,
        PdfDictionaryObject? parameters,
        long maximumBytes)
    {
        if (parameters?.GetNumber(PdfNames.Predictor) is not { } predictor || predictor <= 1)
            return data;

        var columns = parameters.TryGetInteger(PdfNames.Columns, out var columnValue)
            ? columnValue
            : 1;
        var colors = parameters.TryGetInteger(PdfNames.Colors, out var colorValue)
            ? colorValue
            : 1;
        var bits = parameters.TryGetInteger(PdfNames.BitsPerComponent, out var bitsValue)
            ? bitsValue
            : 8;
        if (columns <= 0 || colors <= 0 || bits is not (1 or 2 or 4 or 8 or 16))
            throw new InvalidDataException("Unsupported PDF stream predictor parameters.");

        var rowBytes = checked((columns * colors * bits + 7) / 8);
        if (predictor is >= 10 and <= 15)
            return DecodePngPredictor(data, rowBytes, Math.Max(1, (colors * bits + 7) / 8), maximumBytes);
        if (predictor == 2 && bits == 8)
            return DecodeTiffPredictor(data, rowBytes, Math.Max(1, colors), maximumBytes);
        return data;
    }

    private static byte[] DecodePngPredictor(
        byte[] data,
        int rowBytes,
        int bytesPerPixel,
        long maximumBytes)
    {
        var encodedRowBytes = checked(rowBytes + 1);
        if (data.Length % encodedRowBytes != 0)
            throw new InvalidDataException("PNG predictor data does not contain complete rows.");
        var output = new byte[data.Length - data.Length / encodedRowBytes];
        var previous = new byte[rowBytes];
        var source = 0;
        var target = 0;
        while (source < data.Length)
        {
            var filter = data[source++];
            var row = data.AsSpan(source, rowBytes);
            source += rowBytes;
            var decoded = output.AsSpan(target, rowBytes);
            for (var index = 0; index < rowBytes; index++)
            {
                var left = index >= bytesPerPixel ? decoded[index - bytesPerPixel] : (byte)0;
                var up = previous[index];
                var upLeft = index >= bytesPerPixel ? previous[index - bytesPerPixel] : (byte)0;
                decoded[index] = filter switch
                {
                    0 => row[index],
                    1 => unchecked((byte)(row[index] + left)),
                    2 => unchecked((byte)(row[index] + up)),
                    3 => unchecked((byte)(row[index] + ((left + up) / 2))),
                    4 => unchecked((byte)(row[index] + Paeth(left, up, upLeft))),
                    _ => throw new InvalidDataException($"Unsupported PNG predictor row {filter}."),
                };
            }
            decoded.CopyTo(previous);
            target += rowBytes;
        }

        if (output.LongLength > maximumBytes)
            throw new PdfReaderException(new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Error,
                "PDF021",
                $"Decoded stream exceeded the configured limit of {maximumBytes} bytes."));
        return output;
    }

    private static byte[] DecodeTiffPredictor(
        byte[] data,
        int rowBytes,
        int bytesPerPixel,
        long maximumBytes)
    {
        if (data.Length % rowBytes != 0)
            throw new InvalidDataException("TIFF predictor data does not contain complete rows.");
        var output = data.ToArray();
        for (var rowStart = 0; rowStart < output.Length; rowStart += rowBytes)
        {
            for (var index = bytesPerPixel; index < rowBytes; index++)
                output[rowStart + index] = unchecked(
                    (byte)(output[rowStart + index] + output[rowStart + index - bytesPerPixel]));
        }
        if (output.LongLength > maximumBytes)
            throw new PdfReaderException(new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Error,
                "PDF021",
                $"Decoded stream exceeded the configured limit of {maximumBytes} bytes."));
        return output;
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upLeftDistance = Math.Abs(estimate - upLeft);
        return leftDistance <= upDistance && leftDistance <= upLeftDistance
            ? left
            : upDistance <= upLeftDistance ? up : upLeft;
    }
}
