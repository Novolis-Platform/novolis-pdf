using System.Globalization;
using System.Text;

namespace Novolis.Pdf.Parsing;

internal sealed class PdfSyntaxReader
{
    private readonly byte[] _data;
    private readonly int _maximumDepth;

    public PdfSyntaxReader(byte[] data, int maximumDepth)
    {
        _data = data;
        _maximumDepth = maximumDepth;
    }

    public int Position { get; private set; }

    public void SetPosition(int position)
    {
        if (position < 0 || position > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(position));
        Position = position;
    }

    public PdfObject ReadObject(int depth = 0)
    {
        if (depth > _maximumDepth)
            throw new InvalidDataException("PDF object nesting limit exceeded.");

        SkipWhitespaceAndComments();
        if (Position >= _data.Length)
            throw new EndOfStreamException("Unexpected end of PDF object.");

        if (Peek("<<"))
            return ReadDictionary(depth + 1);
        if (_data[Position] == (byte)'[')
            return ReadArray(depth + 1);
        if (_data[Position] == (byte)'(')
            return new PdfStringObject(ReadLiteralString());
        if (_data[Position] == (byte)'<')
            return new PdfStringObject(ReadHexString(), true);
        if (_data[Position] == (byte)'/')
            return new PdfNameObject(ReadName());
        if (StartsWithKeyword("true"))
        {
            Position += 4;
            return new PdfBooleanObject(true);
        }
        if (StartsWithKeyword("false"))
        {
            Position += 5;
            return new PdfBooleanObject(false);
        }
        if (StartsWithKeyword("null"))
        {
            Position += 4;
            return PdfNullObject.Instance;
        }
        if (IsNumberStart(_data[Position]))
            return ReadNumberOrReference();

        throw new InvalidDataException($"Unexpected PDF token at byte {Position}.");
    }

    public bool TryReadKeyword(string keyword)
    {
        SkipWhitespaceAndComments();
        if (!StartsWithKeyword(keyword))
            return false;
        Position += keyword.Length;
        return true;
    }

    public void SkipWhitespaceAndComments()
    {
        while (Position < _data.Length)
        {
            if (IsWhitespace(_data[Position]))
            {
                Position++;
                continue;
            }

            if (_data[Position] == (byte)'%')
            {
                while (Position < _data.Length && _data[Position] is not (byte)'\r' and not (byte)'\n')
                    Position++;
                continue;
            }

            break;
        }
    }

    public int FindNext(string value, int start)
    {
        var needle = Encoding.ASCII.GetBytes(value);
        for (var index = Math.Max(0, start); index <= _data.Length - needle.Length; index++)
        {
            if (_data.AsSpan(index, needle.Length).SequenceEqual(needle))
                return index;
        }

        return -1;
    }

    public string ReadKeyword()
    {
        SkipWhitespaceAndComments();
        var start = Position;
        while (Position < _data.Length && !IsDelimiter(_data[Position]))
            Position++;
        return Encoding.ASCII.GetString(_data, start, Position - start);
    }

    private PdfObject ReadNumberOrReference()
    {
        var start = Position;
        var first = ReadNumber();
        var afterFirst = Position;
        SkipWhitespaceAndComments();
        if (Position < _data.Length && IsNumberStart(_data[Position]))
        {
            var second = ReadNumber();
            var afterSecond = Position;
            SkipWhitespaceAndComments();
            if (StartsWithKeyword("R"))
            {
                Position += 1;
                if (TryGetInt(first, out var objectNumber)
                    && TryGetInt(second, out var generation))
                    return new PdfIndirectReference(objectNumber, generation);
            }

            Position = afterFirst;
        }
        else
        {
            Position = afterFirst;
        }

        var raw = Encoding.ASCII.GetString(_data, start, Position - start);
        return new PdfNumberObject(first, raw);
    }

    private double ReadNumber()
    {
        var start = Position;
        if (_data[Position] is (byte)'+' or (byte)'-')
            Position++;

        while (Position < _data.Length
               && (_data[Position] == (byte)'.' || _data[Position] is >= (byte)'0' and <= (byte)'9'))
            Position++;

        var raw = Encoding.ASCII.GetString(_data, start, Position - start);
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException($"Invalid PDF number '{raw}'.");
        return value;
    }

    private PdfDictionaryObject ReadDictionary(int depth)
    {
        Position += 2;
        var values = new Dictionary<string, PdfObject>(StringComparer.Ordinal);
        while (true)
        {
            SkipWhitespaceAndComments();
            if (Peek(">>"))
            {
                Position += 2;
                break;
            }

            if (Position >= _data.Length || _data[Position] != (byte)'/')
                throw new InvalidDataException($"Expected dictionary name at byte {Position}.");

            var name = ReadName();
            var value = ReadObject(depth);
            values[name] = value;
        }

        return new PdfDictionaryObject(values);
    }

    private PdfArrayObject ReadArray(int depth)
    {
        Position++;
        var values = new List<PdfObject>();
        while (true)
        {
            SkipWhitespaceAndComments();
            if (Position >= _data.Length)
                throw new EndOfStreamException("Unexpected end of PDF array.");
            if (_data[Position] == (byte)']')
            {
                Position++;
                break;
            }

            values.Add(ReadObject(depth));
        }

        return new PdfArrayObject(values);
    }

    private byte[] ReadLiteralString()
    {
        Position++;
        var bytes = new List<byte>();
        var depth = 1;
        while (Position < _data.Length)
        {
            var current = _data[Position++];
            if (current == (byte)'\\')
            {
                if (Position >= _data.Length)
                    break;
                var escaped = _data[Position++];
                bytes.Add(escaped switch
                {
                    (byte)'n' => (byte)'\n',
                    (byte)'r' => (byte)'\r',
                    (byte)'t' => (byte)'\t',
                    (byte)'b' => (byte)'\b',
                    (byte)'f' => (byte)'\f',
                    (byte)'(' => (byte)'(',
                    (byte)')' => (byte)')',
                    (byte)'\\' => (byte)'\\',
                    (byte)'\r' => ConsumeLineContinuation((byte)'\n'),
                    (byte)'\n' => 0,
                    _ => escaped,
                });
                continue;
            }

            if (current == (byte)'(')
            {
                depth++;
                bytes.Add(current);
                continue;
            }

            if (current == (byte)')')
            {
                depth--;
                if (depth == 0)
                    break;
                bytes.Add(current);
                continue;
            }

            bytes.Add(current);
        }

        if (depth != 0)
            throw new EndOfStreamException("Unterminated PDF literal string.");
        return bytes.ToArray();
    }

    private byte ConsumeLineContinuation(byte next)
    {
        if (Position < _data.Length && _data[Position] == next)
            Position++;
        return 0;
    }

    private byte[] ReadHexString()
    {
        Position++;
        var bytes = new List<byte>();
        var high = -1;
        while (Position < _data.Length)
        {
            var current = _data[Position++];
            if (current == (byte)'>')
                break;
            if (IsWhitespace(current))
                continue;
            var nibble = HexValue(current);
            if (nibble < 0)
                throw new InvalidDataException("Invalid hexadecimal PDF string.");
            if (high < 0)
                high = nibble;
            else
            {
                bytes.Add((byte)((high << 4) | nibble));
                high = -1;
            }
        }

        if (high >= 0)
            bytes.Add((byte)(high << 4));
        return bytes.ToArray();
    }

    private string ReadName()
    {
        Position++;
        var builder = new StringBuilder();
        while (Position < _data.Length && !IsDelimiter(_data[Position]))
        {
            var current = _data[Position++];
            if (current == (byte)'#'
                && Position + 1 < _data.Length
                && HexValue(_data[Position]) >= 0
                && HexValue(_data[Position + 1]) >= 0)
            {
                builder.Append((char)((HexValue(_data[Position]) << 4) | HexValue(_data[Position + 1])));
                Position += 2;
            }
            else
            {
                builder.Append((char)current);
            }
        }

        return builder.ToString();
    }

    private bool StartsWithKeyword(string keyword)
    {
        if (Position + keyword.Length > _data.Length)
            return false;
        if (!Encoding.ASCII.GetBytes(keyword).AsSpan().SequenceEqual(_data.AsSpan(Position, keyword.Length)))
            return false;
        return Position + keyword.Length == _data.Length
            || IsDelimiter(_data[Position + keyword.Length]);
    }

    private bool Peek(string value)
    {
        if (Position + value.Length > _data.Length)
            return false;
        return Encoding.ASCII.GetBytes(value).AsSpan().SequenceEqual(_data.AsSpan(Position, value.Length));
    }

    private static bool TryGetInt(double value, out int result)
    {
        result = (int)value;
        return double.IsFinite(value) && value == result;
    }

    private static bool IsNumberStart(byte value) =>
        value is (byte)'+' or (byte)'-' or (byte)'.'
        || value is >= (byte)'0' and <= (byte)'9';

    private static bool IsWhitespace(byte value) =>
        value is 0 or 9 or 10 or 12 or 13 or 32;

    private static bool IsDelimiter(byte value) =>
        IsWhitespace(value)
        || value is (byte)'/' or (byte)'[' or (byte)']' or (byte)'<' or (byte)'>'
            or (byte)'(' or (byte)')' or (byte)'%';

    private static int HexValue(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - '0',
        >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
        >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
        _ => -1,
    };
}
