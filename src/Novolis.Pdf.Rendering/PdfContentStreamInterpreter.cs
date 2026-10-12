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
                case "c":
                    if (TryNumbers(operands, 6, out var curve))
                        AddCubicBezier(path, ctm, curve[0], curve[1], curve[2], curve[3], curve[4], curve[5]);
                    operands.Clear();
                    break;
                case "v":
                    if (path.Count > 0 && TryNumbers(operands, 4, out var firstCurve))
                    {
                        AddCubicBezier(
                            path,
                            CurrentPoint(path),
                            CurrentPoint(path),
                            ctm.Transform(new PdfPoint(firstCurve[0], firstCurve[1])),
                            ctm.Transform(new PdfPoint(firstCurve[2], firstCurve[3])));
                    }
                    operands.Clear();
                    break;
                case "y":
                    if (TryNumbers(operands, 4, out var secondCurve))
                    {
                        AddCubicBezier(
                            path,
                            ctm,
                            secondCurve[0],
                            secondCurve[1],
                            secondCurve[2],
                            secondCurve[3],
                            secondCurve[2],
                            secondCurve[3]);
                    }
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
                case "W":
                case "W*":
                    // The first raster backend does not maintain a clip stack.
                    // Discarding the pending path still keeps subsequent drawing
                    // commands aligned with the content stream.
                    operands.Clear();
                    break;
                case "n":
                    path.Clear();
                    closed = false;
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
                case "gs":
                    // External graphics states affect opacity, line caps, and
                    // other paint details that this backend does not model.
                    operands.Clear();
                    break;
                case "BMC":
                case "BDC":
                case "EMC":
                case "DP":
                    // Marked content carries accessibility and extraction
                    // metadata, not drawing commands.
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
        if (stream is null)
            return false;

        var subtype = stream.Dictionary.GetName(PdfNames.Subtype);
        if (subtype?.Value == PdfNames.Image.Value)
        {
            if (TryCreateImage(
                    document,
                    stream,
                    ctm,
                    pageIndex,
                    limits,
                    out var image,
                    out var diagnostic))
            {
                commands.Add(image!);
            }

            if (diagnostic is not null)
                diagnostics.Add(diagnostic);
            return true;
        }

        if (subtype?.Value != PdfNames.Form.Value)
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

    private static bool TryCreateImage(
        PdfParsedDocument document,
        PdfStreamObject stream,
        PdfMatrix ctm,
        int pageIndex,
        PdfLimits limits,
        out PdfImageCommand? image,
        out PdfDiagnosticEntry? diagnostic)
    {
        image = null;
        diagnostic = null;
        try
        {
            if (!stream.Dictionary.TryGetInteger(PdfNames.Width, out var width)
                || !stream.Dictionary.TryGetInteger(PdfNames.Height, out var height)
                || width <= 0
                || height <= 0)
            {
                diagnostic = ImageDiagnostic(
                    pageIndex,
                    "PDF033",
                    "Image XObject has no valid dimensions.");
                return false;
            }

            var bits = stream.Dictionary.TryGetInteger(PdfNames.BitsPerComponent, out var bitsValue)
                ? bitsValue
                : 8;
            if (bits is not (1 or 2 or 4 or 8))
            {
                diagnostic = ImageDiagnostic(
                    pageIndex,
                    "PDF034",
                    $"Image XObject uses unsupported bits-per-component value {bits}.");
                return false;
            }

            var format = ResolveImageFormat(document, stream.Dictionary.Get(PdfNames.ColorSpace));
            if (format is null)
            {
                diagnostic = ImageDiagnostic(
                    pageIndex,
                    "PDF035",
                    "Image XObject uses an unsupported color space.");
                return false;
            }

            var components = format switch
            {
                PdfImagePixelFormat.Gray => 1,
                PdfImagePixelFormat.Rgb => 3,
                PdfImagePixelFormat.Cmyk => 4,
                _ => 0,
            };
            var sampleBits = checked((long)width * height * components * bits);
            var sampleBytes = checked((sampleBits + 7) / 8);
            var pixelLimit = checked((long)width * height);
            if (pixelLimit > limits.MaximumImagePixels)
            {
                diagnostic = ImageDiagnostic(
                    pageIndex,
                    "PDF036",
                    $"Image XObject exceeds the configured {limits.MaximumImagePixels:N0}-pixel limit.");
                return false;
            }

            var samples = PdfStreamDecoder.Decode(stream, limits);
            if (samples.LongLength < sampleBytes)
            {
                diagnostic = ImageDiagnostic(
                    pageIndex,
                    "PDF037",
                    "Image XObject ended before all pixel samples were available.");
                return false;
            }

            image = new PdfImageCommand(
                samples,
                width,
                height,
                bits,
                format.Value,
                ctm);
            return true;
        }
        catch (PdfReaderException exception)
        {
            diagnostic = exception.Diagnostic with { PageIndex = pageIndex };
            return false;
        }
        catch (Exception exception) when (exception is InvalidDataException or OverflowException)
        {
            diagnostic = ImageDiagnostic(pageIndex, "PDF038", exception.Message, exception);
            return false;
        }
    }

    private static PdfImagePixelFormat? ResolveImageFormat(
        PdfParsedDocument document,
        PdfObject? value)
    {
        var resolved = document.Resolve(value);
        var name = resolved switch
        {
            PdfNameObject directName => directName.Value,
            PdfArrayObject array when array.Items.FirstOrDefault() is PdfNameObject first => first.Value,
            _ => null,
        };
        return name switch
        {
            "DeviceGray" or "G" => PdfImagePixelFormat.Gray,
            "DeviceRGB" or "RGB" => PdfImagePixelFormat.Rgb,
            "DeviceCMYK" or "CMYK" => PdfImagePixelFormat.Cmyk,
            _ => null,
        };
    }

    private static PdfDiagnosticEntry ImageDiagnostic(
        int pageIndex,
        string code,
        string message,
        Exception? exception = null) =>
        new(
            PdfDiagnosticSeverity.Warning,
            code,
            message,
            PageIndex: pageIndex,
            Exception: exception);

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

    private static void AddCubicBezier(
        ICollection<PdfPoint> path,
        PdfMatrix matrix,
        double control1X,
        double control1Y,
        double control2X,
        double control2Y,
        double endX,
        double endY)
    {
        if (path.Count == 0)
            return;
        var start = path.Last();

        AddCubicBezier(
            path,
            start,
            matrix.Transform(new PdfPoint(control1X, control1Y)),
            matrix.Transform(new PdfPoint(control2X, control2Y)),
            matrix.Transform(new PdfPoint(endX, endY)));
    }

    private static void AddCubicBezier(
        ICollection<PdfPoint> path,
        PdfPoint start,
        PdfPoint control1,
        PdfPoint control2,
        PdfPoint end)
    {
        const int segments = 12;
        for (var index = 1; index <= segments; index++)
        {
            var t = index / (double)segments;
            var inverse = 1 - t;
            path.Add(new PdfPoint(
                (inverse * inverse * inverse * start.X)
                + (3 * inverse * inverse * t * control1.X)
                + (3 * inverse * t * t * control2.X)
                + (t * t * t * end.X),
                (inverse * inverse * inverse * start.Y)
                + (3 * inverse * inverse * t * control1.Y)
                + (3 * inverse * t * t * control2.Y)
                + (t * t * t * end.Y)));
        }
    }

    private static PdfPoint CurrentPoint(IReadOnlyList<PdfPoint> path) =>
        path.Count == 0 ? new PdfPoint(0, 0) : path[^1];

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
                (byte)'<' when IsDictionaryStart() => ReadDictionary(depth + 1),
                (byte)'<' => new PdfStringObject(ReadHex(), true),
                (byte)'[' => ReadArray(depth + 1),
                (byte)'/' => new PdfNameObject(ReadName()),
                _ => ReadNumber(),
            };
        }

        private PdfDictionaryObject ReadDictionary(int depth)
        {
            _position += 2;
            var values = new Dictionary<string, PdfObject>(StringComparer.Ordinal);
            while (true)
            {
                SkipWhitespaceAndComments();
                if (IsDictionaryEnd())
                {
                    _position += 2;
                    return new PdfDictionaryObject(values);
                }

                if (_position >= _data.Length || _data[_position] != (byte)'/')
                    throw new InvalidDataException(
                        $"Expected content dictionary name at byte {_position}.");

                var name = ReadName();
                values[name] = ReadObject(depth);
            }
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
            var closed = false;
            while (_position < _data.Length)
            {
                var current = _data[_position++];
                if (current == (byte)'>')
                {
                    closed = true;
                    break;
                }
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
            if (!closed)
                throw new EndOfStreamException("Unterminated content hexadecimal string.");
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

        private bool IsDictionaryStart() =>
            _position + 1 < _data.Length
            && _data[_position] == (byte)'<'
            && _data[_position + 1] == (byte)'<';

        private bool IsDictionaryEnd() =>
            _position + 1 < _data.Length
            && _data[_position] == (byte)'>'
            && _data[_position + 1] == (byte)'>';
    }
}
