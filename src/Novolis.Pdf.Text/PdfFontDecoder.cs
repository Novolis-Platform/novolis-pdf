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
        if (!HasReadableText(text) && font is not null)
        {
            var embedded = PdfTrueTypeCMap.TryDecode(document, font, raw, limits);
            if (HasReadableText(embedded))
            {
                text = embedded!;
                fromToUnicode = false;
            }
        }

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

    private static bool HasReadableText(string? text) =>
        text?.Any(static value => !char.IsControl(value) && value != '\uFFFD') == true;

    private static string DecodeBuiltInEncoding(byte[] bytes, PdfFontResource? font)
    {
        if (font is { IsType0: true } || font is { IsIdentityEncoding: true })
            return string.Empty;

        var differences = ReadDifferences(font);
        var encodingName = font?.Encoding?.Value
            ?? font?.BaseEncoding?.Value
            ?? "StandardEncoding";
        var output = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            if (differences.TryGetValue(value, out var difference))
            {
                output.Append(GlyphNameToText(difference));
                continue;
            }

            output.Append(DecodeBaseByte(value, encodingName));
        }

        return output.ToString();
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

    private static IReadOnlyDictionary<int, string> ReadDifferences(PdfFontResource? font)
    {
        var differences = font?.EncodingDifferences;
        if (differences is null)
            return new Dictionary<int, string>();

        var result = new Dictionary<int, string>();
        var code = -1;
        foreach (var item in differences.Items)
        {
            if (item is PdfNumberObject number
                && double.IsFinite(number.Value)
                && number.Value == System.Math.Truncate(number.Value)
                && number.Value is >= 0 and <= 255)
            {
                code = (int)number.Value;
                continue;
            }

            if (code >= 0 && item is PdfNameObject name)
            {
                result[code] = name.Value;
                code++;
            }
        }

        return result;
    }

    private static char DecodeBaseByte(byte value, string encodingName)
    {
        if (value is >= 0x20 and <= 0x7E)
            return (char)value;
        if (encodingName is "WinAnsiEncoding" && WinAnsi[value] is { } winAnsi)
            return winAnsi;
        if (encodingName is "MacRomanEncoding" && MacRoman[value] is { } macRoman)
            return macRoman;
        if (value >= 0xA0)
            return (char)value;
        return '\uFFFD';
    }

    private static string GlyphNameToText(string name)
    {
        if (GlyphNames.TryGetValue(name, out var text))
            return text;
        if (name.Length == 1)
            return name;
        if (name.StartsWith("uni", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                name.AsSpan(3),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var unicode)
            && unicode <= 0x10FFFF)
        {
            return char.ConvertFromUtf32(unicode);
        }
        if (name.StartsWith('u')
            && int.TryParse(
                name.AsSpan(1),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out unicode)
            && unicode <= 0x10FFFF)
        {
            return char.ConvertFromUtf32(unicode);
        }

        return "\uFFFD";
    }

    private static readonly IReadOnlyDictionary<string, string> GlyphNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["space"] = " ",
            ["exclam"] = "!",
            ["quotedbl"] = "\"",
            ["numbersign"] = "#",
            ["dollar"] = "$",
            ["percent"] = "%",
            ["ampersand"] = "&",
            ["quotesingle"] = "'",
            ["parenleft"] = "(",
            ["parenright"] = ")",
            ["asterisk"] = "*",
            ["plus"] = "+",
            ["comma"] = ",",
            ["hyphen"] = "-",
            ["period"] = ".",
            ["slash"] = "/",
            ["colon"] = ":",
            ["semicolon"] = ";",
            ["less"] = "<",
            ["equal"] = "=",
            ["greater"] = ">",
            ["question"] = "?",
            ["at"] = "@",
            ["bracketleft"] = "[",
            ["backslash"] = "\\",
            ["bracketright"] = "]",
            ["asciicircum"] = "^",
            ["underscore"] = "_",
            ["grave"] = "`",
            ["braceleft"] = "{",
            ["bar"] = "|",
            ["braceright"] = "}",
            ["asciitilde"] = "~",
            ["bullet"] = "\u2022",
            ["endash"] = "\u2013",
            ["emdash"] = "\u2014",
            ["ellipsis"] = "\u2026",
            ["Euro"] = "\u20AC",
            ["fi"] = "\uFB01",
            ["fl"] = "\uFB02",
            ["Lslash"] = "\u0141",
            ["lslash"] = "\u0142",
            ["Agrave"] = "\u00C0",
            ["Aacute"] = "\u00C1",
            ["Acircumflex"] = "\u00C2",
            ["Atilde"] = "\u00C3",
            ["Adieresis"] = "\u00C4",
            ["Aring"] = "\u00C5",
            ["Ccedilla"] = "\u00C7",
            ["Egrave"] = "\u00C8",
            ["Eacute"] = "\u00C9",
            ["Edieresis"] = "\u00CB",
            ["Igrave"] = "\u00CC",
            ["Iacute"] = "\u00CD",
            ["Idieresis"] = "\u00CF",
            ["Ntilde"] = "\u00D1",
            ["Ograve"] = "\u00D2",
            ["Oacute"] = "\u00D3",
            ["Otilde"] = "\u00D5",
            ["Odieresis"] = "\u00D6",
            ["Ugrave"] = "\u00D9",
            ["Uacute"] = "\u00DA",
            ["Udieresis"] = "\u00DC",
            ["agrave"] = "\u00E0",
            ["aacute"] = "\u00E1",
            ["acircumflex"] = "\u00E2",
            ["atilde"] = "\u00E3",
            ["adieresis"] = "\u00E4",
            ["aring"] = "\u00E5",
            ["ccedilla"] = "\u00E7",
            ["egrave"] = "\u00E8",
            ["eacute"] = "\u00E9",
            ["edieresis"] = "\u00EB",
            ["igrave"] = "\u00EC",
            ["iacute"] = "\u00ED",
            ["idieresis"] = "\u00EF",
            ["ntilde"] = "\u00F1",
            ["ograve"] = "\u00F2",
            ["oacute"] = "\u00F3",
            ["otilde"] = "\u00F5",
            ["odieresis"] = "\u00F6",
            ["ugrave"] = "\u00F9",
            ["uacute"] = "\u00FA",
            ["udieresis"] = "\u00FC",
        };

    private static readonly IReadOnlyDictionary<byte, char> WinAnsi =
        new Dictionary<byte, char>
        {
            [0x80] = '\u20AC',
            [0x82] = '\u201A',
            [0x83] = '\u0192',
            [0x84] = '\u201E',
            [0x85] = '\u2026',
            [0x86] = '\u2020',
            [0x87] = '\u2021',
            [0x88] = '\u02C6',
            [0x89] = '\u2030',
            [0x8A] = '\u0160',
            [0x8B] = '\u2039',
            [0x8C] = '\u0152',
            [0x8E] = '\u017D',
            [0x91] = '\u2018',
            [0x92] = '\u2019',
            [0x93] = '\u201C',
            [0x94] = '\u201D',
            [0x95] = '\u2022',
            [0x96] = '\u2013',
            [0x97] = '\u2014',
            [0x98] = '\u02DC',
            [0x99] = '\u2122',
            [0x9A] = '\u0161',
            [0x9B] = '\u203A',
            [0x9C] = '\u0153',
            [0x9E] = '\u017E',
            [0x9F] = '\u0178',
        };

    private static readonly IReadOnlyDictionary<byte, char> MacRoman =
        new Dictionary<byte, char>
        {
            [0x80] = '\u00C4',
            [0x81] = '\u00C5',
            [0x82] = '\u00C7',
            [0x83] = '\u00C9',
            [0x84] = '\u00D1',
            [0x85] = '\u00D6',
            [0x86] = '\u00DC',
            [0x87] = '\u00E1',
            [0x88] = '\u00E0',
            [0x89] = '\u00E2',
            [0x8A] = '\u00E4',
            [0x8B] = '\u00E3',
            [0x8C] = '\u00E5',
            [0x8D] = '\u00E7',
            [0x8E] = '\u00E9',
            [0x8F] = '\u00E8',
            [0x90] = '\u00EA',
            [0x91] = '\u00EB',
            [0x92] = '\u00ED',
            [0x93] = '\u00EC',
            [0x94] = '\u00EE',
            [0x95] = '\u00EF',
            [0x96] = '\u00F1',
            [0x97] = '\u00F3',
            [0x98] = '\u00F2',
            [0x99] = '\u00F4',
            [0x9A] = '\u00F6',
            [0x9B] = '\u00F5',
            [0x9C] = '\u00FA',
            [0x9D] = '\u00F9',
            [0x9E] = '\u00FB',
            [0x9F] = '\u00FC',
        };

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

        private PdfToUnicodeCMap(
            IReadOnlyDictionary<string, string> mappings,
            IReadOnlyCollection<int> codeLengths)
        {
            _mappings = mappings;
            _codeLengths = codeLengths
                .Concat(mappings.Keys.Select(static key => key.Length / 2))
                .Distinct()
                .OrderByDescending(static length => length)
                .ToArray();
        }

        public static PdfToUnicodeCMap Parse(byte[] data)
        {
            var text = Encoding.ASCII.GetString(data);
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var codeLengths = new HashSet<int>();
            foreach (Match block in CodeSpaceBlock().Matches(text))
            {
                var tokens = HexToken().Matches(block.Groups["body"].Value);
                for (var index = 0; index + 1 < tokens.Count; index += 2)
                {
                    var start = tokens[index].Value;
                    var end = tokens[index + 1].Value;
                    if (start.Length == end.Length)
                        codeLengths.Add(System.Math.Max(1, (start.Length - 2) / 2));
                }
            }

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
            return new PdfToUnicodeCMap(mappings, codeLengths);
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
            if (bytes.Length >= 4 && bytes.Length % 4 == 0)
            {
                var output = new StringBuilder();
                for (var index = 0; index < bytes.Length; index += 4)
                {
                    var value = (uint)(bytes[index] << 24
                        | bytes[index + 1] << 16
                        | bytes[index + 2] << 8
                        | bytes[index + 3]);
                    if (value <= 0x10FFFF)
                        output.Append(char.ConvertFromUtf32((int)value));
                }

                return output.ToString();
            }

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

        [GeneratedRegex(
            @"(?is)begincodespacerange(?<body>.*?)endcodespacerange",
            RegexOptions.CultureInvariant)]
        private static partial Regex CodeSpaceBlock();

        [GeneratedRegex(
            @"<(?<value>[0-9A-Fa-f]+)>",
            RegexOptions.CultureInvariant)]
        private static partial Regex HexToken();
    }
}
