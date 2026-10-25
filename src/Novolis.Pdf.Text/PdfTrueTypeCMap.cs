using System.Text;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;

namespace Novolis.Pdf.Text;

/// <summary>Reads Unicode mappings from an embedded TrueType or OpenType font.</summary>
internal static class PdfTrueTypeCMap
{
    /// <summary>
    /// Decodes identity CIDs through the embedded font cmap when a ToUnicode map is absent or empty.
    /// </summary>
    public static string? TryDecode(
        PdfParsedDocument document,
        PdfFontResource font,
        ReadOnlySpan<byte> bytes,
        PdfLimits limits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(limits);
        if (bytes.Length < 2 || bytes.Length % 2 != 0 || !CanUseGlyphIds(font))
            return null;

        try
        {
            var program = font.ResolveEmbeddedFontProgram();
            if (program is null)
                return null;

            var fontBytes = PdfStreamDecoder.Decode(program, limits);
            if (!TryReadGlyphMap(fontBytes, out var glyphMap))
                return null;

            var output = new StringBuilder(bytes.Length / 2);
            var mapped = false;
            for (var index = 0; index < bytes.Length; index += 2)
            {
                var glyph = (bytes[index] << 8) | bytes[index + 1];
                if (glyphMap.TryGetValue(glyph, out var codePoint))
                {
                    output.Append(char.ConvertFromUtf32(codePoint));
                    mapped = true;
                }
                else
                {
                    output.Append('\uFFFD');
                }
            }

            return mapped ? output.ToString() : null;
        }
        catch (Exception exception) when (exception is InvalidDataException
                                               or PdfReaderException
                                               or ArgumentException
                                               or OverflowException)
        {
            return null;
        }
    }

    private static bool CanUseGlyphIds(PdfFontResource font)
    {
        if (font.IsIdentityEncoding)
            return true;

        var simple = font.IsType0 ? font.ResolveDescendant() ?? font : font;
        return simple.Dictionary.GetName(PdfNames.CidToGidMap)?.Value == "Identity";
    }

    private static bool TryReadGlyphMap(byte[] font, out Dictionary<int, int> glyphMap)
    {
        glyphMap = [];
        if (font.Length < 12)
            return false;

        var tableCount = ReadUInt16(font, 4);
        var directory = 12;
        int cmapOffset = -1;
        int cmapLength = 0;
        for (var index = 0; index < tableCount; index++)
        {
            var entry = directory + (index * 16);
            if (entry + 16 > font.Length)
                return false;
            if (font[entry] == (byte)'c'
                && font[entry + 1] == (byte)'m'
                && font[entry + 2] == (byte)'a'
                && font[entry + 3] == (byte)'p')
            {
                cmapOffset = checked((int)ReadUInt32(font, entry + 8));
                cmapLength = checked((int)ReadUInt32(font, entry + 12));
                break;
            }
        }

        if (cmapOffset < 0
            || cmapLength < 4
            || cmapOffset > font.Length
            || cmapLength > font.Length - cmapOffset)
        {
            return false;
        }

        var encodingCount = ReadUInt16(font, cmapOffset + 2);
        int? format4 = null;
        int? format12 = null;
        for (var index = 0; index < encodingCount; index++)
        {
            var record = cmapOffset + 4 + (index * 8);
            if (record + 8 > cmapOffset + cmapLength)
                return false;
            var platform = ReadUInt16(font, record);
            var encoding = ReadUInt16(font, record + 2);
            var offset = cmapOffset + checked((int)ReadUInt32(font, record + 4));
            if (offset < cmapOffset || offset >= cmapOffset + cmapLength)
                continue;
            var format = ReadUInt16(font, offset);
            var preferred = platform is 0 or 3 && encoding is 1 or 3 or 4 or 10;
            if (format == 12 && (format12 is null || preferred))
                format12 = offset;
            else if (format == 4 && (format4 is null || preferred))
                format4 = offset;
        }

        if (format12 is { } fullOffset)
            ReadFormat12(font, fullOffset, cmapOffset + cmapLength, glyphMap);
        if (glyphMap.Count == 0 && format4 is { } bmpOffset)
            ReadFormat4(font, bmpOffset, cmapOffset + cmapLength, glyphMap);
        return glyphMap.Count > 0;
    }

    private static void ReadFormat4(
        byte[] font,
        int offset,
        int limit,
        IDictionary<int, int> glyphMap)
    {
        if (offset + 14 > limit)
            return;
        var segmentCount = ReadUInt16(font, offset + 6) / 2;
        if (segmentCount <= 0)
            return;
        var endCount = offset + 14;
        var startCount = endCount + (segmentCount * 2) + 2;
        var idDelta = startCount + (segmentCount * 2);
        var idRangeOffset = idDelta + (segmentCount * 2);
        var glyphArray = idRangeOffset + (segmentCount * 2);
        if (glyphArray > limit)
            return;

        for (var index = 0; index < segmentCount; index++)
        {
            var start = ReadUInt16(font, startCount + (index * 2));
            var end = ReadUInt16(font, endCount + (index * 2));
            if (start > end)
                continue;
            var delta = ReadUInt16(font, idDelta + (index * 2));
            var rangeOffset = ReadUInt16(font, idRangeOffset + (index * 2));
            for (var code = start; code <= end; code++)
            {
                int glyph;
                if (rangeOffset == 0)
                {
                    glyph = (code + delta) & 0xFFFF;
                }
                else
                {
                    var glyphOffset = idRangeOffset
                        + (index * 2)
                        + rangeOffset
                        + ((code - start) * 2);
                    if (glyphOffset < 0 || glyphOffset + 2 > limit)
                        continue;
                    glyph = ReadUInt16(font, glyphOffset);
                    if (glyph != 0)
                        glyph = (glyph + delta) & 0xFFFF;
                }

                if (glyph != 0 && !glyphMap.ContainsKey(glyph))
                    glyphMap[glyph] = code;
                if (code == 0xFFFF)
                    break;
            }
        }
    }

    private static void ReadFormat12(
        byte[] font,
        int offset,
        int limit,
        IDictionary<int, int> glyphMap)
    {
        if (offset + 16 > limit)
            return;
        var groupCount = ReadUInt32(font, offset + 12);
        if (groupCount > 1_000_000)
            return;
        var group = offset + 16;
        for (var index = 0; index < groupCount; index++)
        {
            if (group + 12 > limit)
                return;
            var start = ReadUInt32(font, group);
            var end = ReadUInt32(font, group + 4);
            var glyph = ReadUInt32(font, group + 8);
            group += 12;
            if (start > end || end - start > 0x10FFFF)
                continue;
            for (var code = start; code <= end; code++)
            {
                var glyphId = glyph + (code - start);
                if (glyphId is > 0 and <= int.MaxValue
                    && code <= 0x10FFFF
                    && !glyphMap.ContainsKey((int)glyphId))
                {
                    glyphMap[(int)glyphId] = (int)code;
                }

                if (code == end)
                    break;
            }
        }
    }

    private static int ReadUInt16(byte[] data, int offset)
    {
        if (offset < 0 || offset + 2 > data.Length)
            throw new InvalidDataException("TrueType cmap read is outside the font.");
        return (data[offset] << 8) | data[offset + 1];
    }

    private static uint ReadUInt32(byte[] data, int offset)
    {
        if (offset < 0 || offset + 4 > data.Length)
            throw new InvalidDataException("TrueType cmap read is outside the font.");
        return ((uint)data[offset] << 24)
            | ((uint)data[offset + 1] << 16)
            | ((uint)data[offset + 2] << 8)
            | data[offset + 3];
    }
}
