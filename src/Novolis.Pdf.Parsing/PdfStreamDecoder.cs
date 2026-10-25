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

        var filters = ReadFilters(
            stream.Dictionary.Get(PdfNames.Filter),
            stream.Dictionary.Get(PdfNames.DecodeParms));
        var data = stream.EncodedBytes.ToArray();
        for (var index = 0; index < filters.Count; index++)
        {
            var filter = filters[index].Filter;
            data = filter switch
            {
                PdfStreamFilter.FlateDecode => Inflate(data, limits.MaximumStreamBytes),
                PdfStreamFilter.AsciiHexDecode => DecodeAsciiHex(data),
                PdfStreamFilter.Ascii85Decode => DecodeAscii85(data),
                PdfStreamFilter.RunLengthDecode => DecodeRunLength(data, limits.MaximumStreamBytes),
                PdfStreamFilter.LzwDecode => DecodeLzw(
                    data,
                    filters[index].Parameters,
                    limits.MaximumStreamBytes),
                PdfStreamFilter.Crypt => data,
                _ => throw new PdfReaderException(new PdfDiagnosticEntry(
                    PdfDiagnosticSeverity.Warning,
                    "PDF020",
                    $"Unsupported PDF stream filter '{filter}'."),
                    PdfReaderFailureKind.UnsupportedFilter),
            };

            if (data.LongLength > limits.MaximumStreamBytes)
                throw DecodedLimit(limits.MaximumStreamBytes);

            data = ApplyPredictor(data, filters[index].Parameters, limits.MaximumStreamBytes);
        }

        return data;
    }

    private static IReadOnlyList<PdfFilterSpec> ReadFilters(
        PdfObject? value,
        PdfObject? decodeParameters)
    {
        var names = value switch
        {
            PdfNameObject name => [name.AsName()],
            PdfArrayObject array => array.Items
                .OfType<PdfNameObject>()
                .Select(static item => item.AsName())
                .ToArray(),
            _ => [],
        };
        var filters = new PdfFilterSpec[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            filters[index] = new(
                ParseFilter(names[index]),
                ReadDecodeParameters(decodeParameters, index));
        }

        return filters;
    }

    private static PdfDictionaryObject? ReadDecodeParameters(
        PdfObject? value,
        int index) =>
        value switch
        {
            PdfDictionaryObject dictionary when index == 0 => dictionary,
            PdfArrayObject array when index < array.Items.Count
                => array.Items[index] as PdfDictionaryObject,
            _ => null,
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
                throw DecodedLimit(maximumBytes);
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

    private static byte[] DecodeRunLength(byte[] data, long maximumBytes)
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
                if ((long)output.Count + count > maximumBytes)
                    throw DecodedLimit(maximumBytes);
                output.AddRange(data.AsSpan(index, count).ToArray());
                index += count;
                continue;
            }

            var repeatCount = 257 - length;
            if (index >= data.Length)
                throw new EndOfStreamException("Truncated RunLength repeat.");
            var repeated = data[index++];
            if ((long)output.Count + repeatCount > maximumBytes)
                throw DecodedLimit(maximumBytes);
            for (var repeat = 0; repeat < repeatCount; repeat++)
                output.Add(repeated);
        }

        return output.ToArray();
    }

    private static byte[] DecodeLzw(
        byte[] data,
        PdfDictionaryObject? parameters,
        long maximumBytes)
    {
        var earlyChange = parameters?.TryGetInteger(PdfNames.EarlyChange, out var earlyValue) == true
            ? earlyValue
            : 1;
        if (earlyChange is not (0 or 1))
            throw new InvalidDataException("LZW EarlyChange must be 0 or 1.");

        const int clearCode = 256;
        const int endCode = 257;
        var dictionary = new byte[4096][];
        var codeSize = 9;
        var nextCode = 258;
        byte[]? previous = null;
        var output = new List<byte>();
        var reader = new LzwBitReader(data);
        while (reader.TryRead(codeSize, out var code))
        {
            if (code == clearCode)
            {
                codeSize = 9;
                nextCode = 258;
                previous = null;
                continue;
            }

            if (code == endCode)
                break;

            byte[] entry;
            if (code < 256)
            {
                entry = [(byte)code];
            }
            else if (code >= 258 && code < nextCode && dictionary[code] is { } known)
            {
                entry = known;
            }
            else if (code == nextCode && previous is { Length: > 0 })
            {
                entry = previous.Concat([previous[0]]).ToArray();
            }
            else
            {
                throw new InvalidDataException("LZW stream referenced an invalid code.");
            }

            if ((long)output.Count + entry.Length > maximumBytes)
                throw DecodedLimit(maximumBytes);
            output.AddRange(entry);

            if (previous is { Length: > 0 } && nextCode < dictionary.Length)
            {
                dictionary[nextCode++] = previous.Concat([entry[0]]).ToArray();
                if (codeSize < 12 && nextCode + earlyChange >= (1 << codeSize))
                    codeSize++;
            }

            previous = entry;
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
            throw DecodedLimit(maximumBytes);
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
            throw DecodedLimit(maximumBytes);
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

    private static PdfReaderException DecodedLimit(long maximumBytes) =>
        new(
            new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Error,
                "PDF021",
                $"Decoded stream exceeded the configured limit of {maximumBytes} bytes."),
            PdfReaderFailureKind.ResourceLimit);

    private readonly record struct PdfFilterSpec(
        PdfStreamFilter Filter,
        PdfDictionaryObject? Parameters);

    private sealed class LzwBitReader
    {
        private readonly byte[] _data;
        private int _bitPosition;

        public LzwBitReader(byte[] data) => _data = data;

        public bool TryRead(int bitCount, out int value)
        {
            if (_bitPosition + bitCount > _data.Length * 8)
            {
                value = 0;
                return false;
            }

            value = 0;
            for (var index = 0; index < bitCount; index++)
            {
                var byteIndex = _bitPosition / 8;
                var bitIndex = 7 - (_bitPosition % 8);
                value = (value << 1) | ((_data[byteIndex] >> bitIndex) & 1);
                _bitPosition++;
            }

            return true;
        }
    }
}
