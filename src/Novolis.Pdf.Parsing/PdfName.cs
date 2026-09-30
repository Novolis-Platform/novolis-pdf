namespace Novolis.Pdf.Parsing;

/// <summary>Type-safe PDF name token without the leading slash.</summary>
public readonly record struct PdfName : IEquatable<string>
{
    /// <summary>Creates a name from a PDF token, accepting an optional leading slash.</summary>
    public PdfName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value[0] == '/' ? value[1..] : value;
        if (string.IsNullOrWhiteSpace(Value))
            throw new ArgumentException("A PDF name cannot be empty.", nameof(value));
    }

    /// <summary>Name text without the leading slash.</summary>
    public string Value { get; }

    /// <summary>Creates a name object for embedding in the PDF object graph.</summary>
    public PdfNameObject ToObject() => new(Value);

    /// <inheritdoc />
    public bool Equals(string? other)
    {
        if (other is null)
            return false;
        var candidate = other.Length > 0 && other[0] == '/' ? other[1..] : other;
        return string.Equals(Value, candidate, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override string ToString() => "/" + Value;
}
