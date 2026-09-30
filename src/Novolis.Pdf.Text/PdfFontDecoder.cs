using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Rendering;

namespace Novolis.Pdf.Text;

/// <summary>Decodes simple PDF fonts and embedded ToUnicode CMaps.</summary>
public static partial class PdfFontDecoder
{
    /// <summary>Decodes one text command using the page font resources.</summary>
    public static PdfDecodedText Decode(
        PdfParsedDocument document,
        PdfResolvedPage page,
        PdfTextCommand command,
        PdfLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(command);
        limits ??= PdfLimits.Default;
        limits.Validate();

        var raw = command.EncodedBytes ?? Encoding.Latin1.GetBytes(command.Text);
        var font = page.Resources is { } resources
            ? new PdfResourceDictionary(document, resources).ResolveFont(command.FontName)
            : null;
        var toUnicode = font?.ResolveToUnicode();
        var cmap = toUnicode is null
            ? null
            : PdfToUnicodeCMap.Parse(PdfStreamDecoder.Decode(toUnicode, limits));
        var fromToUnicode = cmap is { HasMappings: true };
        var text = cmap is { HasMappings: true }
            ? cmap.Decode(raw)
            : DecodeBuiltInEncoding(raw, font);
        return new PdfDecodedText(text, EstimateAdvance(raw, text, command.FontSize, font), fromToUnicode);
    }

    /// <summary>Reads Identity-H/V CIDs as glyph ids when CIDToGIDMap is identity.</summary>
    public static bool TryReadIdentityGlyphs(
        PdfTextCommand command,
        PdfFontResource? font,
        out ushort[] glyphs)
    {
        ArgumentNullException.ThrowIfNull(command);
        var raw = command.EncodedBytes ?? Encoding.Latin1.GetBytes(command.Text);
        if (raw.Length < 2
            || raw.Length % 2 != 0
            || font is not { IsType0: true } and not { IsIdentityEncoding: true })
        {
            glyphs = [];
            return false;
        }

        glyphs = new ushort[raw.Length / 2];
        for (var index = 0; index < glyphs.Length; index++)
            glyphs[index] = (ushort)((raw[index * 2] << 8) | raw[(index * 2) + 1]);
        return glyphs.Length > 0;
    }

    private static string DecodeBuiltInEncoding(byte[] bytes, PdfFontResource? font)
    {
        if (font is { IsType0: true } || font is { IsIdentityEncoding: true })
            return string.Empty;

        return Encoding.Latin1.GetString(bytes);
    }

    private static double EstimateAdvance(
        byte[] bytes,
        string text,
        double fontSize,
        PdfFontResource? font)
    {
        if (fontSize <= 0 || !double.IsFinite(fontSize))
            return 0;
        var simpleFont = font is { IsType0: true }
            ? font.ResolveDescendant()
            : font;

        var widths = simpleFont?.Widths;
        var firstChar = simpleFont?.FirstChar ?? 0;
        var missingWidth = simpleFont?.MissingWidth ?? 500;
        var total = 0d;
        foreach (var value in bytes)
        {
            var width = missingWidth;
            var index = value - firstChar;
            if (widths?.Get<PdfNumberObject>(index) is { } widthValue)
                width = widthValue.Value;
            total += width;
        }

        if (bytes.Length == 0)
            total = text.Length * 500;
        return System.Math.Max(fontSize * 0.1, fontSize * total / 1000);
    }

    [GeneratedRegex(
        @"(?is)beginbfchar(?<body>.*?)endbfchar",
        RegexOptions.CultureInvariant)]
    private static partial Regex BfCharBlock();

    [GeneratedRegex(
        @"(?is)beginbfrange(?<body>.*?)endbfrange",
        RegexOptions.CultureInvariant)]
    private static partial Regex BfRangeBlock();

    private sealed partial class PdfToUnicodeCMap
    {
        private readonly IReadOnlyDictionary<string, string> _mappings;
        private readonly int[] _codeLengths;

        public bool HasMappings => _mappings.Count > 0;

        private PdfToUnicodeCMap(IReadOnlyDictionary<string, string> mappings)
        {
            _mappings = mappings;
            _codeLengths = mappings.Keys
                .Select(static key => key.Length / 2)
                .Distinct()
                .OrderByDescending(static length => length)
                .ToArray();
        }

        public static PdfToUnicodeCMap Parse(byte[] data)
        {
            var text = Encoding.ASCII.GetString(data);
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match block in BfCharBlock().Matches(text))
            {
                foreach (Match match in HexPair().Matches(block.Groups["body"].Value))
                {
                    var source = match.Groups["source"].Value;
                    var destination = match.Groups["destination"].Value;
                    mappings[source] = DecodeDestination(destination);
                }
            }

            foreach (Match block in BfRangeBlock().Matches(text))
                ReadRangeBlock(block.Groups["body"].Value, mappings);
            return new PdfToUnicodeCMap(mappings);
        }

        public string Decode(ReadOnlySpan<byte> bytes)
        {
            if (_mappings.Count == 0)
                return Encoding.Latin1.GetString(bytes);

            var output = new StringBuilder();
            for (var position = 0; position < bytes.Length;)
            {
                string? mapped = null;
                var consumed = 1;
                foreach (var length in _codeLengths)
                {
                    if (position + length > bytes.Length)
                        continue;
                    var key = Convert.ToHexString(bytes.Slice(position, length));
                    if (_mappings.TryGetValue(key, out mapped))
                    {
                        consumed = length;
                        break;
                    }
                }

                output.Append(mapped ?? "\uFFFD");
                position += consumed;
            }

            return output.ToString();
        }

        private static void ReadRangeBlock(
            string body,
            IDictionary<string, string> mappings)
        {
            var tokens = TokenizeHexAndArray(body);
            for (var index = 0; index + 2 < tokens.Count;)
            {
                var start = tokens[index++];
                var end = tokens[index++];
                var destination = tokens[index++];
                if (!TryHexInteger(start, out var startValue)
                    || !TryHexInteger(end, out var endValue))
                {
                    continue;
                }

                if (destination.StartsWith('['))
                {
                    var values = destination
                        .Trim('[', ']')
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var hexDigits = start.Trim('<', '>').Length;
                    for (var offset = 0; offset <= endValue - startValue && offset < values.Length; offset++)
                    {
                        var source = ToSourceKey(startValue + offset, hexDigits);
                        mappings[source] = DecodeDestination(values[offset]);
                    }
                }
                else if (TryHexInteger(destination, out var destinationValue))
                {
                    var hexDigits = start.Trim('<', '>').Length;
                    for (var offset = 0; offset <= endValue - startValue; offset++)
                    {
                        var source = ToSourceKey(startValue + offset, hexDigits);
                        mappings[source] = DecodeCodePoint(destinationValue + offset);
                    }
                }
            }
        }

        private static IReadOnlyList<string> TokenizeHexAndArray(string body)
        {
            var tokens = new List<string>();
            var builder = new StringBuilder();
            var inArray = false;
            foreach (var value in body)
            {
                if (value == '<' && !inArray)
                {
                    builder.Clear();
                    builder.Append(value);
                    continue;
                }

                if (value == '>' && !inArray)
                {
                    builder.Append(value);
                    tokens.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }

                if (value == '[')
                {
                    builder.Clear();
                    builder.Append(value);
                    inArray = true;
                    continue;
                }

                if (value == ']')
                {
                    builder.Append(value);
                    tokens.Add(builder.ToString());
                    builder.Clear();
                    inArray = false;
                    continue;
                }

                if (inArray || builder.Length > 0)
                {
                    if (inArray && char.IsWhiteSpace(value))
                        continue;
                    builder.Append(value);
                }
            }

            if (builder.Length > 0)
                tokens.Add(builder.ToString());
            return tokens;
        }

        private static string DecodeDestination(string token)
        {
            var hex = token.Trim('<', '>');
            if (hex.Length % 2 != 0)
                hex += "0";
            var bytes = new byte[hex.Length / 2];
            for (var index = 0; index < bytes.Length; index++)
                bytes[index] = byte.Parse(
                    hex.AsSpan(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            return bytes.Length >= 2 && bytes.Length % 2 == 0
                ? Encoding.BigEndianUnicode.GetString(bytes).TrimEnd('\0')
                : Encoding.Latin1.GetString(bytes);
        }

        private static string DecodeCodePoint(int value) =>
            value <= char.MaxValue ? ((char)value).ToString() : char.ConvertFromUtf32(value);

        private static bool TryHexInteger(string value, out int result)
        {
            var hex = value.Trim('<', '>');
            return int.TryParse(
                hex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static string ToSourceKey(int value, int hexDigitCount) =>
            value.ToString(
                $"X{System.Math.Max(2, hexDigitCount)}",
                CultureInfo.InvariantCulture);

        [GeneratedRegex(
            @"<(?<source>[0-9A-Fa-f]+)>\s*<(?<destination>[0-9A-Fa-f]+)>",
            RegexOptions.CultureInvariant)]
        private static partial Regex HexPair();
    }
}
