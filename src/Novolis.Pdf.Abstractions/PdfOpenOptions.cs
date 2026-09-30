namespace Novolis.Pdf.Abstractions;

/// <summary>Bounded, cancellation-aware options for opening a PDF document.</summary>
public sealed record PdfOpenOptions
{
    /// <summary>Default options for a local reader.</summary>
    public static PdfOpenOptions Default { get; } = new();

    /// <summary>Resource limits for parsing and rendering.</summary>
    public PdfLimits Limits { get; init; } = PdfLimits.Default;

    /// <summary>Whether optional malformed structures should produce diagnostics.</summary>
    public bool CollectDiagnostics { get; init; } = true;

    /// <summary>Validates the options before a read begins.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Limits);
        Limits.Validate();
    }
}
