namespace Novolis.Pdf.Platform;

/// <summary>User-space paths selected by a host for PDF reader state.</summary>
public interface IPdfUserDataPaths
{
    /// <summary>Root path for local reader state.</summary>
    string RootPath { get; }

    /// <summary>Path for recent documents and reading positions.</summary>
    string StatePath { get; }

    /// <summary>Path for regenerable page thumbnails and render cache.</summary>
    string CachePath { get; }
}
