using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Parsing;

/// <summary>Reads a PDF source into a bounded parsed object graph.</summary>
public static partial class PdfParser
{
    private static readonly Regex ObjectHeaderRegex = ObjectHeader();

    /// <summary>Parses a PDF source.</summary>
    public static async ValueTask<PdfParsedDocument> ParseAsync(
        IPdfSource source,
        PdfLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        limits ??= PdfLimits.Default;
        limits.Validate();

        var data = await ReadAllAsync(source, limits, cancellationToken).ConfigureAwait(false);
        if (data.Length < 5 || !Encoding.ASCII.GetBytes("%PDF-").AsSpan().SequenceEqual(data.AsSpan(0, 5)))
        {
            throw new PdfReaderException(new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Error,
                "PDF001",
                "The source does not begin with a PDF header."));
        }

        var diagnostics = new List<PdfDiagnosticEntry>();
        var objects = new Dictionary<int, PdfIndirectObject>();
        var objectOffsets = ReadObjectOffsetsFromXref(data, limits, diagnostics, out var xrefTrailer);
        if (objectOffsets.Count == 0)
        {
            foreach (Match match in ObjectHeaderRegex.Matches(Encoding.ASCII.GetString(data)))
            {
                if (int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                    && int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var generation))
                {
                    objectOffsets[number] = new XrefEntry(
                        XrefEntryKind.Offset,
                        match.Index,
                        generation);
                }
            }
        }

        foreach (var (number, entry) in objectOffsets
                     .Where(static item => item.Value.Kind == XrefEntryKind.Offset)
                     .OrderBy(static item => item.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (objects.Count >= limits.MaximumObjects)
            {
                diagnostics.Add(new PdfDiagnosticEntry(
                    PdfDiagnosticSeverity.Error,
                    "PDF002",
                    $"The object limit of {limits.MaximumObjects} was reached.",
                    ObjectNumber: number));
                break;
            }

            try
            {
                var parsed = ParseIndirectObject(data, number, entry.Generation, entry.Offset, limits);
                if (parsed is not null)
                    objects[number] = parsed;
            }
            catch (Exception exception) when (exception is InvalidDataException
                                                   or EndOfStreamException
                                                   or FormatException)
            {
                diagnostics.Add(new PdfDiagnosticEntry(
                    PdfDiagnosticSeverity.Warning,
                    "PDF003",
                    $"Object {number} could not be fully parsed: {exception.Message}",
                    entry.Offset,
                    number,
                    Exception: exception));
            }
        }

        ParseObjectStreams(data, objectOffsets, objects, limits, diagnostics, cancellationToken);

        var trailer = ReadTrailer(data, limits, diagnostics) ?? xrefTrailer;
        var info = ReadDocumentInfo(source.Descriptor, objects, trailer);
        return new PdfParsedDocument(info, objects, trailer, diagnostics);
    }

    private static async Task<byte[]> ReadAllAsync(
        IPdfSource source,
        PdfLimits limits,
        CancellationToken cancellationToken)
    {
        var length = source.Descriptor.Length;
        if (length is > 0 and > int.MaxValue || length > limits.MaximumDocumentBytes)
        {
            throw new PdfReaderException(new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Error,
                "PDF004",
                $"The document exceeds the configured size limit of {limits.MaximumDocumentBytes} bytes."));
        }

        if (length is >= 0)
        {
            var buffer = new byte[(int)length.Value];
            var read = 0;
            while (read < buffer.Length)
            {
                var count = await source.ReadAtAsync(read, buffer.AsMemory(read), cancellationToken)
                    .ConfigureAwait(false);
                if (count == 0)
                    break;
                read += count;
            }

            return read == buffer.Length ? buffer : buffer[..read];
        }

        using var output = new MemoryStream();
        var chunk = new byte[64 * 1024];
        long offset = 0;
        while (output.Length < limits.MaximumDocumentBytes)
        {
            var remaining = limits.MaximumDocumentBytes - output.Length;
            var requested = (int)Math.Min(chunk.Length, remaining);
            var read = await source.ReadAtAsync(offset, chunk.AsMemory(0, requested), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                break;
            await output.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            offset += read;
        }

        if (output.Length >= limits.MaximumDocumentBytes)
        {
            throw new PdfReaderException(new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Error,
                "PDF005",
                $"The document exceeded the configured size limit of {limits.MaximumDocumentBytes} bytes."));
        }

        return output.ToArray();
    }

    private static PdfIndirectObject? ParseIndirectObject(
        byte[] data,
        int expectedNumber,
        int expectedGeneration,
        long offset,
        PdfLimits limits)
    {
        var parsed = ParseIndirectObjectAtOffset(data, offset, limits);
        if (parsed.ObjectNumber != expectedNumber || parsed.Generation != expectedGeneration)
            throw new InvalidDataException("Cross-reference entry points to a different object.");
        return parsed;
    }

    private static PdfIndirectObject ParseIndirectObjectAtOffset(
        byte[] data,
        long offset,
        PdfLimits limits)
    {
        if (offset < 0 || offset >= data.Length)
            throw new InvalidDataException("Object offset is outside the source.");

        var reader = new PdfSyntaxReader(data, limits.MaximumNestingDepth);
        reader.SetPosition((int)offset);
        var number = ReadInteger(reader);
        var generation = ReadInteger(reader);
        if (!reader.TryReadKeyword("obj"))
            throw new InvalidDataException("Indirect object header is missing 'obj'.");

        var value = reader.ReadObject();
        reader.SkipWhitespaceAndComments();
        if (value is PdfDictionaryObject dictionary && reader.TryReadKeyword("stream"))
        {
            SkipStreamLineEnding(data, reader);
            var streamStart = reader.Position;
            var streamLength = ResolveLength(dictionary.Get(PdfNames.Length));
            int length;
            if (streamLength is >= 0 and <= int.MaxValue
                && streamStart + streamLength.Value <= data.Length)
            {
                length = streamLength.Value;
            }
            else
            {
                var endStream = reader.FindNext("endstream", streamStart);
                if (endStream < 0)
                    throw new EndOfStreamException("PDF stream has no endstream marker.");
                length = endStream - streamStart;
            }

            if (length > limits.MaximumStreamBytes)
                throw new InvalidDataException("PDF stream exceeds the configured stream limit.");

            value = new PdfStreamObject(dictionary, data.AsSpan(streamStart, length).ToArray());
        }

        return new PdfIndirectObject(number, generation, value, offset);
    }

    private static int ReadInteger(PdfSyntaxReader reader)
    {
        var value = reader.ReadObject();
        return value is PdfNumberObject number
            && double.IsFinite(number.Value)
            && number.Value == Math.Truncate(number.Value)
            ? checked((int)number.Value)
            : throw new InvalidDataException("Expected an integer PDF object header.");
    }

    private static int? ResolveLength(PdfObject? value) =>
        value is PdfNumberObject number
        && number.Value >= 0
        && number.Value <= int.MaxValue
        && number.Value == Math.Truncate(number.Value)
            ? (int)number.Value
            : null;

    private static void SkipStreamLineEnding(byte[] data, PdfSyntaxReader reader)
    {
        if (reader.Position < data.Length && data[reader.Position] == (byte)'\r')
        {
            reader.SetPosition(reader.Position + 1);
            if (reader.Position < data.Length && data[reader.Position] == (byte)'\n')
                reader.SetPosition(reader.Position + 1);
        }
        else if (reader.Position < data.Length && data[reader.Position] == (byte)'\n')
        {
            reader.SetPosition(reader.Position + 1);
        }
    }

    private static Dictionary<int, XrefEntry> ReadObjectOffsetsFromXref(
        byte[] data,
        PdfLimits limits,
        ICollection<PdfDiagnosticEntry> diagnostics,
        out PdfDictionaryObject? xrefTrailer)
    {
        var offsets = new Dictionary<int, XrefEntry>();
        xrefTrailer = null;
        var text = Encoding.ASCII.GetString(data);
        var startXref = text.LastIndexOf("startxref", StringComparison.Ordinal);
        if (startXref < 0)
            return offsets;

        var position = startXref + "startxref".Length;
        while (position < data.Length && IsWhitespace(data[position]))
            position++;
        var numberStart = position;
        while (position < data.Length && data[position] is >= (byte)'0' and <= (byte)'9')
            position++;
        if (!long.TryParse(
                text[numberStart..position],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var xrefOffset)
            || xrefOffset < 0
            || xrefOffset >= data.Length)
        {
            return offsets;
        }

        if (!ReadXrefAt(
                data,
                xrefOffset,
                limits,
                offsets,
                diagnostics,
                new HashSet<long>(),
                out xrefTrailer))
        {
            diagnostics.Add(new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Information,
                "PDF006",
                "The cross-reference section was not readable; object scanning was used.",
                xrefOffset));
            offsets.Clear();
        }

        return offsets;
    }

    private static bool ReadXrefAt(
        byte[] data,
        long xrefOffset,
        PdfLimits limits,
        IDictionary<int, XrefEntry> offsets,
        ICollection<PdfDiagnosticEntry> diagnostics,
        ISet<long> visited,
        out PdfDictionaryObject? trailer)
    {
        trailer = null;
        if (xrefOffset < 0 || xrefOffset >= data.Length || !visited.Add(xrefOffset))
            return false;

        var reader = new PdfSyntaxReader(data, limits.MaximumNestingDepth);
        reader.SetPosition((int)xrefOffset);
        if (string.Equals(reader.ReadKeyword(), "xref", StringComparison.Ordinal))
        {
            while (true)
            {
                reader.SkipWhitespaceAndComments();
                if (reader.Position >= data.Length)
                    return false;
                var section = reader.ReadKeyword();
                if (string.Equals(section, "trailer", StringComparison.Ordinal))
                {
                    trailer = reader.ReadObject() as PdfDictionaryObject;
                    break;
                }

                if (!int.TryParse(section, NumberStyles.None, CultureInfo.InvariantCulture, out var first))
                    return false;
                var countToken = reader.ReadKeyword();
                if (!int.TryParse(countToken, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                    || first < 0
                    || count < 0)
                {
                    return false;
                }

                for (var index = 0; index < count; index++)
                {
                    reader.SkipWhitespaceAndComments();
                    var line = ReadLine(data, reader);
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3
                        || !long.TryParse(
                            parts[0],
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var offset)
                        || !int.TryParse(
                            parts[1],
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var generation)
                        || !parts[2].StartsWith('n'))
                    {
                        continue;
                    }

                    var objectNumber = first + index;
                    if (!offsets.ContainsKey(objectNumber))
                    {
                        offsets[objectNumber] = new XrefEntry(
                            XrefEntryKind.Offset,
                            offset,
                            generation);
                    }
                }
            }

            if (trailer?.GetNumber(PdfNames.Prev) is { } previous
                && previous >= 0
                && previous <= long.MaxValue
                && ReadXrefAt(data, (long)previous, limits, offsets, diagnostics, visited, out var previousTrailer)
                && previousTrailer is not null)
            {
                trailer = MergeTrailer(trailer, previousTrailer);
            }

            return true;
        }

        try
        {
            var xrefObject = ParseIndirectObjectAtOffset(data, xrefOffset, limits);
            if (xrefObject.Value is not PdfStreamObject stream
                || stream.Dictionary.GetName(PdfNames.Type) != PdfNames.XRef)
            {
                return false;
            }

            trailer = stream.Dictionary;
            offsets[xrefObject.ObjectNumber] = new XrefEntry(
                XrefEntryKind.Offset,
                xrefOffset,
                xrefObject.Generation);
            var decoded = PdfStreamDecoder.Decode(stream, limits);
            ReadXrefStreamEntries(stream.Dictionary, decoded, offsets);

            if (stream.Dictionary.GetNumber(PdfNames.Prev) is { } previous
                && previous >= 0
                && previous <= long.MaxValue)
            {
                ReadXrefAt(data, (long)previous, limits, offsets, diagnostics, visited, out var previousTrailer);
                if (previousTrailer is not null)
                    trailer = MergeTrailer(trailer, previousTrailer);
            }

            return true;
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or EndOfStreamException
                or PdfReaderException)
        {
            diagnostics.Add(new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Warning,
                "PDF008",
                $"The xref stream could not be parsed: {exception.Message}",
                xrefOffset,
                Exception: exception));
            return false;
        }
    }

    private static void ReadXrefStreamEntries(
        PdfDictionaryObject dictionary,
        byte[] data,
        IDictionary<int, XrefEntry> offsets)
    {
        var widths = dictionary.GetArray(PdfNames.W);
        if (widths is null || widths.Items.Count < 3)
            throw new InvalidDataException("An xref stream must define three field widths.");

        var fieldWidths = widths.Items
            .Take(3)
            .OfType<PdfNumberObject>()
            .Select(static item => checked((int)item.Value))
            .ToArray();
        if (fieldWidths.Length != 3 || fieldWidths.Any(static width => width < 0 || width > 8))
            throw new InvalidDataException("An xref stream has invalid field widths.");

        var ranges = ReadXrefRanges(dictionary);
        var recordWidth = fieldWidths.Sum();
        if (recordWidth <= 0)
            throw new InvalidDataException("An xref stream has an empty record width.");

        var position = 0;
        foreach (var (first, count) in ranges)
        {
            for (var index = 0; index < count; index++)
            {
                if (position + recordWidth > data.Length)
                    throw new EndOfStreamException("An xref stream ended before all records were read.");

                var type = ReadBigEndian(data, ref position, fieldWidths[0], defaultValue: 1);
                var field2 = ReadBigEndian(data, ref position, fieldWidths[1]);
                var field3 = ReadBigEndian(data, ref position, fieldWidths[2]);
                var objectNumber = checked(first + index);
                if (offsets.ContainsKey(objectNumber))
                    continue;

                if (type == 1)
                {
                    offsets[objectNumber] = new XrefEntry(
                        XrefEntryKind.Offset,
                        checked((long)field2),
                        checked((int)field3));
                }
                else if (type == 2)
                {
                    offsets[objectNumber] = new XrefEntry(
                        XrefEntryKind.ObjectStream,
                        0,
                        0,
                        checked((int)field2),
                        checked((int)field3));
                }
            }
        }
    }

    private static IReadOnlyList<(int First, int Count)> ReadXrefRanges(
        PdfDictionaryObject dictionary)
    {
        var index = dictionary.GetArray(PdfNames.Index);
        if (index is null)
        {
            var size = dictionary.GetNumber(PdfNames.Size)
                ?? throw new InvalidDataException("An xref stream must define /Size.");
            return [(0, checked((int)size))];
        }

        var values = index.Items
            .OfType<PdfNumberObject>()
            .Select(static item => checked((int)item.Value))
            .ToArray();
        if (values.Length % 2 != 0)
            throw new InvalidDataException("An xref stream /Index must contain pairs.");
        var ranges = new List<(int First, int Count)>(values.Length / 2);
        for (var position = 0; position < values.Length; position += 2)
            ranges.Add((values[position], values[position + 1]));
        return ranges;
    }

    private static ulong ReadBigEndian(
        byte[] data,
        ref int position,
        int width,
        ulong defaultValue = 0)
    {
        if (width == 0)
            return defaultValue;
        ulong value = 0;
        for (var index = 0; index < width; index++)
            value = (value << 8) | data[position++];
        return value;
    }

    private static PdfDictionaryObject MergeTrailer(
        PdfDictionaryObject current,
        PdfDictionaryObject previous)
    {
        var values = new Dictionary<string, PdfObject>(previous.Values, StringComparer.Ordinal);
        foreach (var (name, value) in current.Values)
            values[name] = value;
        return new PdfDictionaryObject(values);
    }

    private static void ParseObjectStreams(
        byte[] data,
        IReadOnlyDictionary<int, XrefEntry> offsets,
        IDictionary<int, PdfIndirectObject> objects,
        PdfLimits limits,
        ICollection<PdfDiagnosticEntry> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var group in offsets
                     .Where(static item => item.Value.Kind == XrefEntryKind.ObjectStream)
                     .GroupBy(static item => item.Value.ObjectStreamNumber)
                     .OrderBy(static group => group.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!objects.TryGetValue(group.Key, out var objectStreamObject)
                || objectStreamObject.Value is not PdfStreamObject objectStream)
            {
                diagnostics.Add(new PdfDiagnosticEntry(
                    PdfDiagnosticSeverity.Warning,
                    "PDF009",
                    $"Object stream {group.Key} was referenced but could not be loaded.",
                    ObjectNumber: group.Key));
                continue;
            }

            try
            {
                var objectCount = ReadRequiredInteger(objectStream.Dictionary, PdfNames.N);
                var firstOffset = ReadRequiredInteger(objectStream.Dictionary, PdfNames.First);
                if (objectCount < 0 || objectCount > limits.MaximumObjects || firstOffset < 0)
                    throw new InvalidDataException("Object stream header values are outside the configured limits.");

                var decoded = PdfStreamDecoder.Decode(objectStream, limits);
                if (firstOffset > decoded.Length)
                    throw new InvalidDataException("Object stream /First is outside the decoded stream.");

                var header = new PdfSyntaxReader(decoded, limits.MaximumNestingDepth);
                var entries = new (int ObjectNumber, int Offset)[objectCount];
                for (var index = 0; index < objectCount; index++)
                {
                    var objectNumber = ReadInteger(header);
                    var offset = ReadInteger(header);
                    entries[index] = (objectNumber, offset);
                }

                foreach (var reference in group.OrderBy(static item => item.Value.ObjectStreamIndex))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entryIndex = reference.Value.ObjectStreamIndex;
                    if (entryIndex < 0 || entryIndex >= entries.Length)
                        continue;

                    var (objectNumber, offset) = entries[entryIndex];
                    if (objectNumber != reference.Key || objects.ContainsKey(objectNumber))
                        continue;
                    var valueReader = new PdfSyntaxReader(decoded, limits.MaximumNestingDepth);
                    valueReader.SetPosition(checked(firstOffset + offset));
                    var value = valueReader.ReadObject();
                    objects[objectNumber] = new PdfIndirectObject(
                        objectNumber,
                        0,
                        value,
                        objectStreamObject.Offset + firstOffset + offset);
                    if (objects.Count >= limits.MaximumObjects)
                        return;
                }
            }
            catch (Exception exception) when (
                exception is InvalidDataException
                    or EndOfStreamException
                    or OverflowException
                    or PdfReaderException)
            {
                diagnostics.Add(new PdfDiagnosticEntry(
                    PdfDiagnosticSeverity.Warning,
                    "PDF010",
                    $"Object stream {group.Key} could not be parsed: {exception.Message}",
                    ObjectNumber: group.Key,
                    Exception: exception));
            }
        }
    }

    private static int ReadRequiredInteger(
        PdfDictionaryObject dictionary,
        PdfName name) =>
        dictionary.TryGetInteger(name, out var value) && value >= 0
            ? value
            : throw new InvalidDataException($"PDF dictionary entry {name} must be a non-negative integer.");

    private static string ReadLine(byte[] data, PdfSyntaxReader reader)
    {
        var start = reader.Position;
        while (reader.Position < data.Length
               && data[reader.Position] is not (byte)'\r' and not (byte)'\n')
            reader.SetPosition(reader.Position + 1);
        var line = Encoding.ASCII.GetString(data, start, reader.Position - start);
        if (reader.Position < data.Length && data[reader.Position] == (byte)'\r')
            reader.SetPosition(reader.Position + 1);
        if (reader.Position < data.Length && data[reader.Position] == (byte)'\n')
            reader.SetPosition(reader.Position + 1);
        return line;
    }

    private static PdfDictionaryObject? ReadTrailer(
        byte[] data,
        PdfLimits limits,
        ICollection<PdfDiagnosticEntry> diagnostics)
    {
        var text = Encoding.ASCII.GetString(data);
        var trailerPosition = text.LastIndexOf("trailer", StringComparison.Ordinal);
        if (trailerPosition < 0)
            return null;

        try
        {
            var reader = new PdfSyntaxReader(data, limits.MaximumNestingDepth);
            reader.SetPosition(trailerPosition + "trailer".Length);
            return reader.ReadObject() as PdfDictionaryObject;
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            diagnostics.Add(new PdfDiagnosticEntry(
                PdfDiagnosticSeverity.Warning,
                "PDF007",
                $"The trailer could not be parsed: {exception.Message}",
                trailerPosition,
                Exception: exception));
            return null;
        }
    }

    private static PdfDocumentInfo ReadDocumentInfo(
        PdfSourceDescriptor descriptor,
        IReadOnlyDictionary<int, PdfIndirectObject> objects,
        PdfDictionaryObject? trailer)
    {
        var values = trailer?.Get(PdfNames.Info) switch
        {
            PdfIndirectReference reference when objects.TryGetValue(reference.ObjectNumber, out var indirect)
                => indirect.Value as PdfDictionaryObject,
            PdfDictionaryObject dictionary => dictionary,
            _ => null,
        };

        var info = values is null ? null : new PdfInfoDictionary(values);
        return new PdfDocumentInfo(
            descriptor.Normalize(),
            info?.Title,
            info?.Author,
            info?.Subject,
            info?.Creator,
            info?.Producer,
            info?.Language,
            null);
    }

    private enum XrefEntryKind
    {
        Offset,
        ObjectStream,
    }

    private readonly record struct XrefEntry(
        XrefEntryKind Kind,
        long Offset,
        int Generation,
        int ObjectStreamNumber = 0,
        int ObjectStreamIndex = 0);

    private static bool IsWhitespace(byte value) =>
        value is 0 or 9 or 10 or 12 or 13 or 32;

    [GeneratedRegex(@"(?m)(?<!\d)(\d+)\s+(\d+)\s+obj\b", RegexOptions.CultureInvariant)]
    private static partial Regex ObjectHeader();
}
