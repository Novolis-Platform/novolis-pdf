namespace Novolis.Pdf.Parsing;

/// <summary>Typed view over a font resource dictionary.</summary>
public sealed class PdfFontResource
{
    private readonly PdfParsedDocument _document;

    /// <summary>Creates a font resource view.</summary>
    public PdfFontResource(PdfParsedDocument document, PdfDictionaryObject dictionary)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dictionary);
        _document = document;
        Dictionary = dictionary;
    }

    /// <summary>Underlying font dictionary.</summary>
    public PdfDictionaryObject Dictionary { get; }

    /// <summary>Font subtype.</summary>
    public PdfName? Subtype => Dictionary.GetName(PdfNames.Subtype);

    /// <summary>Font encoding name when it is a name object.</summary>
    public PdfName? Encoding => Dictionary.GetName(PdfNames.Encoding);

    /// <summary>Whether the font is a Type 0 composite font.</summary>
    public bool IsType0 => Subtype == PdfNames.Type0;

    /// <summary>Whether the encoding is an identity mapping.</summary>
    public bool IsIdentityEncoding =>
        Encoding == PdfNames.IdentityH || Encoding == PdfNames.IdentityV;

    /// <summary>Base font name, including an optional subset prefix.</summary>
    public PdfName? BaseFont => Dictionary.GetName(PdfNames.BaseFont);

    /// <summary>Family-style name with any six-letter subset prefix removed.</summary>
    public string? FamilyName
    {
        get
        {
            var value = BaseFont?.Value;
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var plus = value.IndexOf('+');
            return plus is >= 6 and <= 7 ? value[(plus + 1)..] : value;
        }
    }

    /// <summary>Simple-font widths array.</summary>
    public PdfArrayObject? Widths => Dictionary.GetArray(PdfNames.Widths);

    /// <summary>First encoded character code.</summary>
    public int FirstChar => Dictionary.TryGetInteger(PdfNames.FirstChar, out var first)
        ? first
        : 0;

    /// <summary>Missing-width metric from the font descriptor.</summary>
    public double MissingWidth =>
        Dictionary.GetDictionary(PdfNames.FontDescriptor)?.GetNumber(PdfNames.MissingWidth)
        ?? 500;

    /// <summary>CID-font default width, falling back to the descriptor missing width.</summary>
    public double DefaultWidth
    {
        get
        {
            var simple = IsType0 ? ResolveDescendant() ?? this : this;
            return simple.Dictionary.GetNumber(PdfNames.Dw) ?? simple.MissingWidth;
        }
    }

    /// <summary>Width of one character or CID, in thousandths of an em.</summary>
    public double WidthForCode(int code)
    {
        var simple = IsType0 ? ResolveDescendant() ?? this : this;
        var cidWidths = simple.Dictionary.GetArray(PdfNames.W);
        if (cidWidths is not null)
        {
            var items = cidWidths.Items;
            for (var index = 0; index < items.Count;)
            {
                if (items[index] is not PdfNumberObject first)
                {
                    index++;
                    continue;
                }

                if (index + 1 < items.Count && items[index + 1] is PdfArrayObject array)
                {
                    var cid = (int)first.Value;
                    foreach (var item in array.Items)
                    {
                        if (cid == code && item is PdfNumberObject width)
                            return width.Value;
                        cid++;
                    }

                    index += 2;
                    continue;
                }

                if (index + 2 < items.Count
                    && items[index + 1] is PdfNumberObject last
                    && items[index + 2] is PdfNumberObject runWidth
                    && code >= first.Value
                    && code <= last.Value)
                {
                    return runWidth.Value;
                }

                index += 3;
            }
        }

        if (simple.Widths is { } simpleWidths)
        {
            var offset = code - simple.FirstChar;
            if (simpleWidths.Get<PdfNumberObject>(offset) is { } width)
                return width.Value;
        }

        return simple.Dictionary.GetNumber(PdfNames.Dw) ?? simple.MissingWidth;
    }

    /// <summary>Advance for encoded text bytes at the current text-space font size.</summary>
    public double AdvanceForBytes(ReadOnlySpan<byte> bytes, double fontSize)
    {
        var identity = IsType0 || IsIdentityEncoding;
        var step = identity ? 2 : 1;
        if (bytes.Length < step || fontSize <= 0 || !double.IsFinite(fontSize))
            return Math.Max(0, fontSize * 0.5);

        var total = 0d;
        for (var index = 0; index + step <= bytes.Length; index += step)
        {
            var code = identity
                ? (bytes[index] << 8) | bytes[index + 1]
                : bytes[index];
            total += WidthForCode(code);
        }

        return fontSize * total / 1000d;
    }

    /// <summary>Resolves an embedded ToUnicode CMap stream.</summary>
    public PdfStreamObject? ResolveToUnicode() =>
        _document.ResolveStream(Dictionary.Get(PdfNames.ToUnicode));

    /// <summary>Resolves the first descendant font for a Type 0 font.</summary>
    public PdfFontResource? ResolveDescendant()
    {
        var descendants = Dictionary.GetArray(PdfNames.DescendantFonts);
        if (descendants is null || descendants.Items.Count == 0)
            return null;
        var dictionary = _document.ResolveDictionary(descendants.Items[0]);
        return dictionary is null ? null : new PdfFontResource(_document, dictionary);
    }

    /// <summary>Resolves an embedded TrueType, OpenType, or Type 1 font program.</summary>
    public PdfStreamObject? ResolveEmbeddedFontProgram()
    {
        var simple = IsType0 ? ResolveDescendant() ?? this : this;
        var descriptor = _document.ResolveDictionary(simple.Dictionary.Get(PdfNames.FontDescriptor))
            ?? simple.Dictionary.GetDictionary(PdfNames.FontDescriptor);
        if (descriptor is null)
            return null;
        return _document.ResolveStream(descriptor.Get(PdfNames.FontFile2))
            ?? _document.ResolveStream(descriptor.Get(PdfNames.FontFile3))
            ?? _document.ResolveStream(descriptor.Get(PdfNames.FontFile));
    }
}
