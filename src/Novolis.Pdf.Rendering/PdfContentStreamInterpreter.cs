using System.Globalization;
using System.Text;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;

namespace Novolis.Pdf.Rendering;

/// <summary>Interprets the first portable PDF graphics and text operators.</summary>
public static class PdfContentStreamInterpreter
{
    /// <summary>Builds a render plan for a resolved page.</summary>
    public static PdfPageRenderPlan BuildPlan(
        PdfParsedDocument document,
        PdfResolvedPage page,
        PdfLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);
        limits ??= PdfLimits.Default;
        limits.Validate();

        var commands = new List<PdfDrawCommand>();
        var diagnostics = new List<PdfDiagnosticEntry>();
        foreach (var stream in page.Contents)
        {
            try
            {
                var bytes = PdfStreamDecoder.Decode(stream, limits);
                Interpret(
                    bytes,
                    page.Info.Index,
                    commands,
                    diagnostics,
                    limits,
                    document,
                    page.Resources,
                    PdfMatrix.Identity,
                    formDepth: 0);
            }
            catch (PdfReaderException exception)
            {
                diagnostics.Add(exception.Diagnostic with { PageIndex = page.Info.Index });
            }
            catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
            {
                diagnostics.Add(new PdfDiagnosticEntry(
                    PdfDiagnosticSeverity.Warning,
                    "PDF030",
                    $"Content stream could not be interpreted: {exception.Message}",
                    PageIndex: page.Info.Index,
                    Exception: exception));
            }
        }

        return new PdfPageRenderPlan(page.Info, commands, diagnostics);
    }

    private static void Interpret(
        byte[] bytes,
        int pageIndex,
        ICollection<PdfDrawCommand> commands,
        ICollection<PdfDiagnosticEntry> diagnostics,
        PdfLimits limits,
        PdfParsedDocument document,
        PdfDictionaryObject? resources,
        PdfMatrix initialCtm,
        int formDepth)
    {
        var reader = new ContentReader(bytes, limits.MaximumNestingDepth);
        var operands = new List<PdfObject>();
        var path = new List<PdfPoint>();
        var closed = false;
        var ctm = initialCtm;
        var textMatrix = PdfMatrix.Identity;
        var fontSize = 12d;
        var fontName = (string?)null;
        var fillColor = PdfColor.Black;
        var strokeColor = PdfColor.Black;
        var strokeWidth = 1d;
        var textLineMatrix = PdfMatrix.Identity;
        var leading = 0d;
        var graphicsStack = new Stack<GraphicsState>();

        while (reader.TryRead(out var value, out var operation))
        {
            if (value is not null)
            {
                operands.Add(value);
                continue;
            }

            switch (operation)
            {
                case "q":
                    graphicsStack.Push(new GraphicsState(ctm, fillColor, strokeColor, strokeWidth));
                    operands.Clear();
                    break;
                case "Q" when graphicsStack.TryPop(out var savedState):
                    ctm = savedState.Ctm;
                    fillColor = savedState.FillColor;
                    strokeColor = savedState.StrokeColor;
                    strokeWidth = savedState.StrokeWidth;
                    operands.Clear();
                    break;
                case "cm" when TryNumbers(operands, 6, out var matrixValues):
                    ctm = ctm.Multiply(new PdfMatrix(
                        matrixValues[0],
                        matrixValues[1],
                        matrixValues[2],
                        matrixValues[3],
                        matrixValues[4],
                        matrixValues[5]));
                    operands.Clear();
                    break;
                case "m" when TryNumbers(operands, 2, out var move):
                    path.Add(ctm.Transform(new PdfPoint(move[0], move[1])));
                    closed = false;
                    operands.Clear();
                    break;
                case "l" when TryNumbers(operands, 2, out var line):
                    path.Add(ctm.Transform(new PdfPoint(line[0], line[1])));
                    operands.Clear();
                    break;
                case "re" when TryNumbers(operands, 4, out var rectangle):
                    AddRectangle(path, ctm, rectangle);
                    closed = true;
                    operands.Clear();
                    break;
                case "h":
                    closed = true;
                    operands.Clear();
                    break;
                case "S":
                case "s":
                    EmitPath(commands, path, closed, fill: false, stroke: true, strokeWidth, fillColor, strokeColor);
                    path.Clear();
                    closed = false;
                    operands.Clear();
                    break;
                case "f":
                case "F":
                case "f*":
                    EmitPath(commands, path, closed, fill: true, stroke: false, strokeWidth, fillColor, strokeColor);
                    path.Clear();
                    closed = false;
                    operands.Clear();
                    break;
                case "B":
                case "b":
                    EmitPath(commands, path, closed, fill: true, stroke: true, strokeWidth, fillColor, strokeColor);
                    path.Clear();
                    closed = false;
                    operands.Clear();
                    break;
                case "w" when TryNumbers(operands, 1, out var width):
                    strokeWidth = System.Math.Max(0.1, width[0]);
                    operands.Clear();
                    break;
                case "rg" when TryNumbers(operands, 3, out var fill):
                    fillColor = new PdfColor(fill[0], fill[1], fill[2]);
                    operands.Clear();
                    break;
                case "RG" when TryNumbers(operands, 3, out var stroke):
                    strokeColor = new PdfColor(stroke[0], stroke[1], stroke[2]);
                    operands.Clear();
                    break;
                case "g" when TryNumbers(operands, 1, out var gray):
                    fillColor = new PdfColor(gray[0], gray[0], gray[0]);
                    operands.Clear();
                    break;
                case "G" when TryNumbers(operands, 1, out var grayStroke):
                    strokeColor = new PdfColor(grayStroke[0], grayStroke[0], grayStroke[0]);
                    operands.Clear();
                    break;
                case "BT":
                    textMatrix = PdfMatrix.Identity;
                    textLineMatrix = PdfMatrix.Identity;
                    leading = 0;
                    operands.Clear();
                    break;
                case "ET":
                    operands.Clear();
                    break;
                case "Tf" when TryNumbers(operands, 1, out var typeSize):
                    fontSize = System.Math.Max(1, typeSize[0]);
                    if (operands.FirstOrDefault() is PdfNameObject name)
                        fontName = name.Value;
                    operands.Clear();
                    break;
                case "Tm" when TryNumbers(operands, 6, out var textTransform):
                    textMatrix = new PdfMatrix(
                        textTransform[0],
                        textTransform[1],
                        textTransform[2],
                        textTransform[3],
                        textTransform[4],
                        textTransform[5]);
                    textLineMatrix = textMatrix;
                    operands.Clear();
                    break;
                case "Td" or "TD" when TryNumbers(operands, 2, out var textDelta):
                    if (operation == "TD")
                        leading = -textDelta[1];
                    textLineMatrix = textLineMatrix.Multiply(
                        new PdfMatrix(1, 0, 0, 1, textDelta[0], textDelta[1]));
                    textMatrix = textLineMatrix;
                    operands.Clear();
                    break;
                case "T*" :
                    textLineMatrix = textLineMatrix.Multiply(new PdfMatrix(1, 0, 0, 1, 0, -leading));
                    textMatrix = textLineMatrix;
                    operands.Clear();
                    break;
                case "Tj" or "'":
                    if (operation == "'")
                    {
                        textLineMatrix = textLineMatrix.Multiply(
                            new PdfMatrix(1, 0, 0, 1, 0, -leading));
                        textMatrix = textLineMatrix;
                    }
                    if (operands.LastOrDefault() is PdfStringObject text)
                    {
                        EmitText(commands, ctm, textMatrix, text, fontSize, fillColor, fontName);
                        textMatrix = AdvanceEncoded(
                            textMatrix,
                            text.Bytes,
                            fontSize,
                            ResolveFont(document, resources, fontName));
                    }
                    operands.Clear();
                    break;
                case "TJ":
                    if (operands.LastOrDefault() is PdfArrayObject textArray)
                    {
                        var font = ResolveFont(document, resources, fontName);
                        foreach (var item in textArray.Items)
                        {
                            if (item is PdfStringObject fragment)
                            {
                                EmitText(commands, ctm, textMatrix, fragment, fontSize, fillColor, fontName);
                                textMatrix = AdvanceEncoded(
                                    textMatrix,
                                    fragment.Bytes,
                                    fontSize,
                                    font);
                            }
                            else if (item is PdfNumberObject adjust)
                            {
                                textMatrix = textMatrix.Multiply(
                                    new PdfMatrix(1, 0, 0, 1, -adjust.Value / 1000d * fontSize, 0));
                            }
                        }
                    }
                    operands.Clear();
                    break;
                case "Do":
                    if (operands.LastOrDefault() is PdfNameObject xobjectName
                        && InvokeXObject(
                            xobjectName.Value,
                            document,
                            resources,
                            ctm,
                            pageIndex,
                            commands,
                            diagnostics,
                            limits,
                            formDepth))
                    {
                        operands.Clear();
                        break;
                    }

                    diagnostics.Add(new PdfDiagnosticEntry(
                        PdfDiagnosticSeverity.Information,
                        "PDF031",
                        "An image/form XObject was skipped by the first raster backend.",
                        PageIndex: pageIndex));
                    operands.Clear();
                    break;
                default:
                    if (operation is not null
                        && operation is not "Do")
                    {
                        diagnostics.Add(new PdfDiagnosticEntry(
                            PdfDiagnosticSeverity.Information,
                            "PDF032",
                            $"Unsupported content operator '{operation}'.",
                            PageIndex: pageIndex));
                    }
                    operands.Clear();
                    break;
            }
        }
    }

    private static void EmitText(
        ICollection<PdfDrawCommand> commands,
        PdfMatrix ctm,
        PdfMatrix textMatrix,
        PdfStringObject value,
        double fontSize,
        PdfColor color,
        string? fontName)
    {
        if (value.Bytes.Length == 0)
            return;
        var text = value.GetText();
        commands.Add(new PdfTextCommand(
            text,
            ctm.Transform(textMatrix.Transform(new PdfPoint(0, 0))),
            fontSize,
            color.Clamp(),
            fontName,
            value.Bytes.ToArray(),
            ctm,
            textMatrix));
    }

    private static PdfFontResource? ResolveFont(
        PdfParsedDocument document,
        PdfDictionaryObject? resources,
        string? fontName) =>
        resources is null
            ? null
            : new PdfResourceDictionary(document, resources).ResolveFont(fontName);

    private static PdfMatrix AdvanceEncoded(
        PdfMatrix textMatrix,
        byte[] bytes,
        double fontSize,
        PdfFontResource? font)
    {
        var advance = font?.AdvanceForBytes(bytes, fontSize)
            ?? System.Math.Max(0, (bytes.Length >= 2 ? bytes.Length / 2d : bytes.Length) * fontSize * 0.5);
        return textMatrix.Multiply(new PdfMatrix(1, 0, 0, 1, advance, 0));
    }

    private static bool InvokeXObject(
        string name,
        PdfParsedDocument document,
        PdfDictionaryObject? resources,
        PdfMatrix ctm,
        int pageIndex,
        ICollection<PdfDrawCommand> commands,
        ICollection<PdfDiagnosticEntry> diagnostics,
        PdfLimits limits,
        int formDepth)
    {
        if (formDepth >= 8 || resources is null)
            return false;

        var stream = new PdfResourceDictionary(document, resources).ResolveXObject(name);
        if (stream is null || stream.Dictionary.GetName(PdfNames.Subtype) != PdfNames.Form)
            return false;

        var formResources = document.ResolveDictionary(stream.Dictionary.Get(PdfNames.Resources))
            ?? resources;
        var formCtm = ctm.Multiply(ReadMatrix(stream.Dictionary.GetArray(PdfNames.Matrix)));
        var bytes = PdfStreamDecoder.Decode(stream, limits);
        Interpret(
            bytes,
            pageIndex,
            commands,
            diagnostics,
            limits,
            document,
            formResources,
            formCtm,
            formDepth + 1);
        return true;
    }

    private static PdfMatrix ReadMatrix(PdfArrayObject? array)
    {
        if (array is null || array.Items.Count < 6)
            return PdfMatrix.Identity;
        var values = array.Items.OfType<PdfNumberObject>().Select(static item => item.Value).Take(6).ToArray();
        return values.Length == 6
            ? new PdfMatrix(values[0], values[1], values[2], values[3], values[4], values[5])
            : PdfMatrix.Identity;
    }

    private static void EmitPath(
        ICollection<PdfDrawCommand> commands,
        IReadOnlyList<PdfPoint> path,
        bool closed,
        bool fill,
        bool stroke,
        double strokeWidth,
        PdfColor fillColor,
        PdfColor strokeColor)
    {
        if (path.Count < 2)
            return;
        commands.Add(new PdfPathCommand(
            path.ToArray(),
            closed,
            fill,
            stroke,
            strokeWidth,
            fillColor.Clamp(),
            strokeColor.Clamp()));
    }

    private static void AddRectangle(ICollection<PdfPoint> path, PdfMatrix matrix, IReadOnlyList<double> values)
    {
        var x = values[0];
        var y = values[1];
        var width = values[2];
        var height = values[3];
        path.Add(matrix.Transform(new PdfPoint(x, y)));
        path.Add(matrix.Transform(new PdfPoint(x + width, y)));
        path.Add(matrix.Transform(new PdfPoint(x + width, y + height)));
        path.Add(matrix.Transform(new PdfPoint(x, y + height)));
    }

    private static bool TryNumbers(
        IReadOnlyList<PdfObject> operands,
        int count,
        out double[] numbers)
    {
        numbers = [];
        if (operands.Count < count)
            return false;
        var values = operands.Skip(operands.Count - count).OfType<PdfNumberObject>().ToArray();
        if (values.Length != count)
            return false;
        numbers = values.Select(static value => value.Value).ToArray();
        return true;
    }

    private readonly record struct GraphicsState(
        PdfMatrix Ctm,
        PdfColor FillColor,
        PdfColor StrokeColor,
        double StrokeWidth);

    private sealed class ContentReader(byte[] data, int maximumDepth)
    {
        private readonly byte[] _data = data;
        private readonly int _maximumDepth = maximumDepth;
        private int _position;

        public bool TryRead(out PdfObject? value, out string? operation)
        {
            SkipWhitespaceAndComments();
            value = null;
            operation = null;
            if (_position >= _data.Length)
                return false;

            var current = _data[_position];
            if (current is (byte)'(' or (byte)'<' or (byte)'[' or (byte)'/')
            {
                value = ReadObject(0);
                return true;
            }
            if (current is (byte)'+' or (byte)'-' or (byte)'.'
                || current is >= (byte)'0' and <= (byte)'9')
            {
                value = ReadNumber();
                return true;
            }

            var start = _position;
            while (_position < _data.Length && !IsDelimiter(_data[_position]))
                _position++;
            operation = Encoding.ASCII.GetString(_data, start, _position - start);
            return operation.Length > 0;
        }

        private PdfObject ReadObject(int depth)
        {
            if (depth > _maximumDepth)
                throw new InvalidDataException("Content object nesting limit exceeded.");
            SkipWhitespaceAndComments();
            if (_position >= _data.Length)
                throw new EndOfStreamException();

            return _data[_position] switch
            {
                (byte)'(' => new PdfStringObject(ReadLiteral()),
                (byte)'<' => new PdfStringObject(ReadHex(), true),
                (byte)'[' => ReadArray(depth + 1),
                (byte)'/' => new PdfNameObject(ReadName()),
                _ => ReadNumber(),
            };
        }

        private PdfArrayObject ReadArray(int depth)
        {
            _position++;
            var items = new List<PdfObject>();
            while (true)
            {
                SkipWhitespaceAndComments();
                if (_position >= _data.Length)
                    throw new EndOfStreamException();
                if (_data[_position] == (byte)']')
                {
                    _position++;
                    return new PdfArrayObject(items);
                }
                items.Add(ReadObject(depth));
            }
        }

        private PdfNumberObject ReadNumber()
        {
            var start = _position;
            if (_data[_position] is (byte)'+' or (byte)'-')
                _position++;
            while (_position < _data.Length
                   && (_data[_position] == (byte)'.'
                       || _data[_position] is >= (byte)'0' and <= (byte)'9'))
                _position++;
            var raw = Encoding.ASCII.GetString(_data, start, _position - start);
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new InvalidDataException($"Invalid content number '{raw}'.");
            return new PdfNumberObject(value, raw);
        }

        private byte[] ReadLiteral()
        {
            _position++;
            var bytes = new List<byte>();
            var depth = 1;
            while (_position < _data.Length)
            {
                var current = _data[_position++];
                if (current == (byte)'\\' && _position < _data.Length)
                {
                    var escaped = _data[_position++];
                    bytes.Add(escaped switch
                    {
                        (byte)'n' => (byte)'\n',
                        (byte)'r' => (byte)'\r',
                        (byte)'t' => (byte)'\t',
                        (byte)'b' => (byte)'\b',
                        (byte)'f' => (byte)'\f',
                        _ => escaped,
                    });
                    continue;
                }
                if (current == (byte)'(')
                    depth++;
                else if (current == (byte)')' && --depth == 0)
                    break;
                else
                    bytes.Add(current);
            }
            return bytes.ToArray();
        }

        private byte[] ReadHex()
        {
            _position++;
            var bytes = new List<byte>();
            var high = -1;
            while (_position < _data.Length)
            {
                var current = _data[_position++];
                if (current == (byte)'>')
                    break;
                if (IsWhitespace(current))
                    continue;
                var nibble = HexValue(current);
                if (nibble < 0)
                    throw new InvalidDataException("Invalid content hexadecimal string.");
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
            _position++;
            var start = _position;
            while (_position < _data.Length && !IsDelimiter(_data[_position]))
                _position++;
            return Encoding.ASCII.GetString(_data, start, _position - start);
        }

        private void SkipWhitespaceAndComments()
        {
            while (_position < _data.Length)
            {
                if (IsWhitespace(_data[_position]))
                {
                    _position++;
                    continue;
                }
                if (_data[_position] == (byte)'%')
                {
                    while (_position < _data.Length && _data[_position] is not (byte)'\r' and not (byte)'\n')
                        _position++;
                    continue;
                }
                break;
            }
        }

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
}
