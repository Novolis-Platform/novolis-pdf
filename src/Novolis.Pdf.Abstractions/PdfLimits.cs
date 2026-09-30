namespace Novolis.Pdf.Abstractions;

/// <summary>Resource limits applied while reading an untrusted PDF.</summary>
public sealed record PdfLimits
{
    /// <summary>Default limits for ordinary local documents.</summary>
    public static PdfLimits Default { get; } = new();

    /// <summary>Maximum number of bytes read into the parser buffer.</summary>
    public long MaximumDocumentBytes { get; init; } = 128L * 1024 * 1024;

    /// <summary>Maximum number of indirect objects accepted.</summary>
    public int MaximumObjects { get; init; } = 500_000;

    /// <summary>Maximum recursive PDF value depth.</summary>
    public int MaximumNestingDepth { get; init; } = 128;

    /// <summary>Maximum decoded stream bytes.</summary>
    public long MaximumStreamBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>Maximum image pixels requested by a renderer.</summary>
    public long MaximumImagePixels { get; init; } = 80_000_000;

    /// <summary>Maximum number of pages exposed by a document.</summary>
    public int MaximumPages { get; init; } = 100_000;

    /// <summary>Validates configured limits.</summary>
    public void Validate()
    {
        if (MaximumDocumentBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumDocumentBytes));
        if (MaximumObjects <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumObjects));
        if (MaximumNestingDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumNestingDepth));
        if (MaximumStreamBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumStreamBytes));
        if (MaximumImagePixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumImagePixels));
        if (MaximumPages <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumPages));
    }
}
