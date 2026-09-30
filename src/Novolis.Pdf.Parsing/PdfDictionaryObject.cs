namespace Novolis.Pdf.Parsing;

/// <summary>PDF dictionary object.</summary>
public sealed record PdfDictionaryObject(IReadOnlyDictionary<string, PdfObject> Values) : PdfObject
{
    /// <inheritdoc />
    public override PdfObjectKind Kind => PdfObjectKind.Dictionary;

    /// <summary>Returns a value by name.</summary>
    public PdfObject? Get(string name) =>
        Values.TryGetValue(name, out var value) ? value : null;

    /// <summary>Returns a value by typed name.</summary>
    public PdfObject? Get(PdfName name) => Get(name.Value);

    /// <summary>Gets a value using a type-safe runtime discriminator.</summary>
    public bool TryGet<T>(string name, out T? value)
        where T : PdfObject
    {
        if (Values.TryGetValue(name, out var candidate) && candidate is T typed)
        {
            value = typed;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Gets a typed value using a typed name.</summary>
    public bool TryGet<T>(PdfName name, out T? value)
        where T : PdfObject =>
        TryGet(name.Value, out value);

    /// <summary>Returns a typed value by name.</summary>
    public T? Get<T>(string name)
        where T : PdfObject =>
        Get(name) as T;

    /// <summary>Returns a typed value by typed name.</summary>
    public T? Get<T>(PdfName name)
        where T : PdfObject =>
        Get<T>(name.Value);

    /// <summary>Returns a name value as text.</summary>
    public string? GetName(string name) => Get<PdfNameObject>(name)?.Value;

    /// <summary>Returns a typed name value.</summary>
    public PdfName? GetName(PdfName name) =>
        Get<PdfNameObject>(name) is { } value ? value.AsName() : null;

    /// <summary>Returns a numeric value.</summary>
    public double? GetNumber(string name) => Get<PdfNumberObject>(name)?.Value;

    /// <summary>Returns a numeric value by typed name.</summary>
    public double? GetNumber(PdfName name) => GetNumber(name.Value);

    /// <summary>Reads a finite integer value by typed name.</summary>
    public bool TryGetInteger(PdfName name, out int value)
    {
        if (GetNumber(name) is { } number
            && double.IsFinite(number)
            && number == Math.Truncate(number)
            && number >= int.MinValue
            && number <= int.MaxValue)
        {
            value = (int)number;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>Returns a referenced object value.</summary>
    public PdfIndirectReference? GetReference(string name) =>
        Get<PdfIndirectReference>(name);

    /// <summary>Returns a referenced object value by typed name.</summary>
    public PdfIndirectReference? GetReference(PdfName name) =>
        GetReference(name.Value);

    /// <summary>Returns an array value.</summary>
    public PdfArrayObject? GetArray(string name) =>
        Get<PdfArrayObject>(name);

    /// <summary>Returns an array value by typed name.</summary>
    public PdfArrayObject? GetArray(PdfName name) =>
        GetArray(name.Value);

    /// <summary>Returns a dictionary value.</summary>
    public PdfDictionaryObject? GetDictionary(string name) =>
        Get<PdfDictionaryObject>(name);

    /// <summary>Returns a dictionary value by typed name.</summary>
    public PdfDictionaryObject? GetDictionary(PdfName name) =>
        GetDictionary(name.Value);

    /// <summary>Returns a stream value.</summary>
    public PdfStreamObject? GetStream(string name) =>
        Get<PdfStreamObject>(name);

    /// <summary>Returns a stream value by typed name.</summary>
    public PdfStreamObject? GetStream(PdfName name) =>
        GetStream(name.Value);

    /// <summary>Returns a string value.</summary>
    public PdfStringObject? GetString(string name) =>
        Get<PdfStringObject>(name);

    /// <summary>Returns a string value by typed name.</summary>
    public PdfStringObject? GetString(PdfName name) =>
        GetString(name.Value);
}
