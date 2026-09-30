using System.Text.Json;

namespace Novolis.Pdf.Documents;

/// <summary>Atomic JSON-file local state store for recent documents and positions.</summary>
public sealed class JsonPdfDocumentStateStore : IPdfDocumentStateStore
{
    private const int MaximumRecentDocuments = 20;
    private readonly string _rootPath;
    private readonly string _recentPath;
    private readonly string _positionsPath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates a store below a host-provided user-space directory.</summary>
    public JsonPdfDocumentStateStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.GetFullPath(rootPath);
        _recentPath = Path.Combine(_rootPath, "recent.json");
        _positionsPath = Path.Combine(_rootPath, "positions.json");
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PdfRecentDocument>> LoadRecentAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAsync<List<PdfRecentDocument>>(_recentPath, cancellationToken)
                .ConfigureAwait(false)
                ?? [];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PdfRecentDocument>> RememberAsync(
        PdfRecentDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(document.StableId))
            throw new ArgumentException("A stable document id is required.", nameof(document));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await ReadAsync<List<PdfRecentDocument>>(_recentPath, cancellationToken)
                .ConfigureAwait(false)
                ?? [];
            entries.RemoveAll(entry => string.Equals(
                entry.StableId,
                document.StableId,
                StringComparison.Ordinal));
            entries.Insert(0, document with { DisplayName = NormalizeName(document.DisplayName) });
            if (entries.Count > MaximumRecentDocuments)
                entries.RemoveRange(MaximumRecentDocuments, entries.Count - MaximumRecentDocuments);
            await WriteAsync(_recentPath, entries, cancellationToken).ConfigureAwait(false);
            return entries;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<PdfReadingPosition?> LoadPositionAsync(
        string stableId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var positions = await ReadAsync<Dictionary<string, PdfReadingPosition>>(
                    _positionsPath,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? new(StringComparer.Ordinal);
            return positions.TryGetValue(stableId, out var position) ? position : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SavePositionAsync(
        PdfReadingPosition position,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (string.IsNullOrWhiteSpace(position.StableId))
            throw new ArgumentException("A stable document id is required.", nameof(position));
        if (position.PageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(position));
        if (!double.IsFinite(position.Zoom) || position.Zoom <= 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var positions = await ReadAsync<Dictionary<string, PdfReadingPosition>>(
                    _positionsPath,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? new(StringComparer.Ordinal);
            positions[position.StableId] = position with
            {
                UpdatedUtc = position.UpdatedUtc == default
                    ? DateTimeOffset.UtcNow
                    : position.UpdatedUtc,
            };
            await WriteAsync(_positionsPath, positions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(
        string stableId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await ReadAsync<List<PdfRecentDocument>>(_recentPath, cancellationToken)
                .ConfigureAwait(false)
                ?? [];
            entries.RemoveAll(entry => string.Equals(entry.StableId, stableId, StringComparison.Ordinal));
            await WriteAsync(_recentPath, entries, cancellationToken).ConfigureAwait(false);

            var positions = await ReadAsync<Dictionary<string, PdfReadingPosition>>(
                    _positionsPath,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? new(StringComparer.Ordinal);
            positions.Remove(stableId);
            await WriteAsync(_positionsPath, positions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return default;

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (IOException)
        {
            return default;
        }
    }

    private async Task WriteAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, _jsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "document.pdf" : name.Trim();
}
