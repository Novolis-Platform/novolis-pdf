using Novolis.Pdf.Abstractions;

namespace Novolis.Pdf.Core;

/// <summary>Factories for standard local PDF sources.</summary>
public static class PdfSources
{
    /// <summary>Opens a local file for random-access reading.</summary>
    public static PdfStreamSource OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var stream = new FileStream(
            fullPath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        var info = new FileInfo(fullPath);
        return new PdfStreamSource(
            stream,
            new PdfSourceDescriptor(info.Name, fullPath, info.Length, info.LastWriteTimeUtc));
    }

    /// <summary>Wraps a readable seekable stream.</summary>
    public static PdfStreamSource FromStream(
        Stream stream,
        string displayName = "document.pdf",
        string? stableId = null,
        bool leaveOpen = false) =>
        new(
            stream,
            new PdfSourceDescriptor(displayName, stableId, stream.Length),
            leaveOpen);
}
