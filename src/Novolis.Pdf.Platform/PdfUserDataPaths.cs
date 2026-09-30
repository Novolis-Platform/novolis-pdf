namespace Novolis.Pdf.Platform;

/// <summary>Simple user-space path implementation for a host.</summary>
public sealed class PdfUserDataPaths : IPdfUserDataPaths
{
    /// <summary>Creates paths below <paramref name="rootPath"/>.</summary>
    public PdfUserDataPaths(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = Path.GetFullPath(rootPath);
        StatePath = Path.Combine(RootPath, "state");
        CachePath = Path.Combine(RootPath, "cache");
    }

    /// <inheritdoc />
    public string RootPath { get; }

    /// <inheritdoc />
    public string StatePath { get; }

    /// <inheritdoc />
    public string CachePath { get; }
}
