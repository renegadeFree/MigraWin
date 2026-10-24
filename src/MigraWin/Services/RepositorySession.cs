using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class RepositorySession : IAsyncDisposable
{
    private readonly FileStream _lock;
    private readonly RepositoryKeys _keys;
    private bool _disposed;

    private RepositorySession(
        string root,
        RepositoryFormat format,
        RepositoryKeys keys,
        FileStream repositoryLock)
    {
        Root = root;
        Format = format;
        _keys = keys;
        _lock = repositoryLock;
        Objects = new EncryptedObjectStore(root, keys);
    }

    public string Root { get; }
    public RepositoryFormat Format { get; }
    internal EncryptedObjectStore Objects { get; }
    internal RepositoryKeys Keys => _keys;

    public static async Task<(RepositorySession Session, string? RecoveryKey)> CreateAsync(
        string root,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
        {
            throw new ArgumentException("La password deve contenere almeno 10 caratteri.", nameof(password));
        }

        Directory.CreateDirectory(root);
        var formatPath = Path.Combine(root, "format.json");
        if (File.Exists(formatPath))
        {
            throw new IOException("La cartella contiene già un repository MigraWin.");
        }

        var repositoryLock = AcquireLock(root);
        try
        {
            foreach (var folder in new[] { "objects", "snapshots", "transactions", "reports" })
            {
                Directory.CreateDirectory(Path.Combine(root, folder));
            }

            var (format, recoveryKey) = RepositoryCrypto.Create(password);
            await RepositoryCrypto.WriteFormatAsync(formatPath, format, cancellationToken);
            var keys = RepositoryCrypto.Unlock(format, password);
            return (new RepositorySession(root, format, keys, repositoryLock), recoveryKey);
        }
        catch
        {
            await repositoryLock.DisposeAsync();
            throw;
        }
    }

    public static async Task<RepositorySession> OpenAsync(
        string root,
        string secret,
        CancellationToken cancellationToken = default)
    {
        var formatPath = Path.Combine(root, "format.json");
        if (!File.Exists(formatPath))
        {
            throw new FileNotFoundException("Repository MigraWin non trovato.", formatPath);
        }

        var repositoryLock = AcquireLock(root);
        try
        {
            var format = await RepositoryCrypto.ReadFormatAsync(formatPath, cancellationToken);
            var keys = RepositoryCrypto.Unlock(format, secret);
            return new RepositorySession(root, format, keys, repositoryLock);
        }
        catch
        {
            await repositoryLock.DisposeAsync();
            throw;
        }
    }

    public async Task SaveSnapshotAsync(SnapshotManifest manifest, CancellationToken cancellationToken)
    {
        var path = SnapshotPath(manifest.SnapshotId);
        var json = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonDefaults.Options);
        await RepositoryCrypto.WriteEncryptedManifestAsync(
            path,
            json,
            Keys,
            Format.RepositoryId,
            manifest.SnapshotId,
            cancellationToken);
    }

    public async Task<SnapshotManifest> LoadSnapshotAsync(string snapshotId, CancellationToken cancellationToken)
    {
        var path = SnapshotPath(snapshotId);
        var plaintext = await RepositoryCrypto.ReadEncryptedManifestAsync(
            path,
            Keys,
            Format.RepositoryId,
            snapshotId,
            cancellationToken);
        var manifest = JsonSerializer.Deserialize<SnapshotManifest>(plaintext, JsonDefaults.Options)
                       ?? throw new InvalidDataException("Manifest del backup non valido.");
        if (manifest.SchemaVersion != 1
            || !string.Equals(manifest.SnapshotId, snapshotId, StringComparison.Ordinal)
            || !string.Equals(manifest.RepositoryId, Format.RepositoryId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Il manifest non appartiene a questo repository o usa una versione non supportata.");
        }

        return manifest;
    }

    public async Task<IReadOnlyList<SnapshotSummary>> ListSnapshotsAsync(CancellationToken cancellationToken)
    {
        var result = new List<SnapshotSummary>();
        var folder = Path.Combine(Root, "snapshots");
        if (!Directory.Exists(folder))
        {
            return result;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*.snapshot"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshotId = Path.GetFileNameWithoutExtension(path);
            var manifest = await LoadSnapshotAsync(snapshotId, cancellationToken);
            result.Add(new SnapshotSummary
            {
                SnapshotId = manifest.SnapshotId,
                ProfileName = manifest.ProfileName,
                CreatedAt = manifest.CreatedAt,
                ApplicationCount = manifest.Applications.Count,
                FileCount = manifest.FileCount,
                LogicalBytes = manifest.LogicalBytes,
                CreatedBy = manifest.CreatedBy,
                MachineName = manifest.MachineName,
                IsIncremental = manifest.Scope == BackupScope.Incremental
                ,
                IsDifferential = manifest.Scope == BackupScope.Differential
            });
        }

        return result.OrderByDescending(snapshot => snapshot.CreatedAt).ToArray();
    }

    public async Task<SnapshotManifest?> GetLatestSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshots = await ListSnapshotsAsync(cancellationToken);
        return snapshots.Count == 0 ? null : await LoadSnapshotAsync(snapshots[0].SnapshotId, cancellationToken);
    }

    public async Task<long> VerifySnapshotAsync(SnapshotManifest manifest, CancellationToken cancellationToken)
    {
        long verified = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in manifest.Applications
                     .SelectMany(application => application.Components)
                     .SelectMany(component => component.Files))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ids.Add(file.ObjectId))
            {
                continue;
            }

            var length = await Objects.VerifyFileAsync(file.ObjectId, cancellationToken);
            if (length != file.Length)
            {
                throw new InvalidDataException($"Dimensione non valida per l'oggetto {file.ObjectId}.");
            }

            verified++;
        }

        return verified;
    }

    public async Task DeleteSnapshotAsync(string snapshotId, CancellationToken cancellationToken)
    {
        var path = SnapshotPath(snapshotId);
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete(path);
        var remaining = await ListSnapshotsAsync(cancellationToken);
        var referenced = await CollectReferencedObjectsAsync(remaining, cancellationToken);
        var objectsRoot = Path.Combine(Root, "objects");
        foreach (var objectPath in Directory.EnumerateFiles(objectsRoot, "*.mwo", SearchOption.AllDirectories))
        {
            var id = Path.GetFileNameWithoutExtension(objectPath);
            if (!referenced.Contains(id))
            {
                File.Delete(objectPath);
            }
        }
    }

    public async Task<string> ExportSnapshotAsync(
        string snapshotId,
        string parentDestination,
        CancellationToken cancellationToken)
    {
        var manifest = await LoadSnapshotAsync(snapshotId, cancellationToken);
        Directory.CreateDirectory(parentDestination);
        var exportRoot = Path.Combine(parentDestination, $"MigraWin-{snapshotId[..8]}");
        if (Directory.Exists(exportRoot) || File.Exists(exportRoot))
        {
            throw new IOException($"La destinazione esiste già: {exportRoot}");
        }

        var fullExport = Path.GetFullPath(exportRoot);
        var fullRoot = Path.GetFullPath(Root).TrimEnd('\\') + "\\";
        if ((fullExport + "\\").StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Esporta l'archivio autonomo fuori dal repository originale.");
        }

        Directory.CreateDirectory(Path.Combine(exportRoot, "objects"));
        Directory.CreateDirectory(Path.Combine(exportRoot, "snapshots"));
        File.Copy(Path.Combine(Root, "format.json"), Path.Combine(exportRoot, "format.json"));
        File.Copy(SnapshotPath(snapshotId), Path.Combine(exportRoot, "snapshots", snapshotId + ".snapshot"));

        var objectIds = manifest.Applications
            .SelectMany(application => application.Components)
            .SelectMany(component => component.Files)
            .Select(file => file.ObjectId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var objectId in objectIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.Combine(objectId[..2], objectId + ".mwo");
            var source = Path.Combine(Root, "objects", relative);
            var target = Path.Combine(exportRoot, "objects", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, false);
            var sourceHash = await HashFileAsync(source, cancellationToken);
            var targetHash = await HashFileAsync(target, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(sourceHash, targetHash))
            {
                throw new InvalidDataException($"Copia non integra per l'oggetto {objectId}.");
            }
        }

        var exportInfo = new
        {
            formatVersion = 1,
            repositoryId = Format.RepositoryId,
            snapshotId,
            exportedAt = DateTimeOffset.Now,
            objectCount = objectIds.Length
        };
        await File.WriteAllTextAsync(
            Path.Combine(exportRoot, "export.json"),
            JsonSerializer.Serialize(exportInfo, JsonDefaults.Options),
            cancellationToken);
        return exportRoot;
    }

    public async Task<string> ExportSnapshotArchiveAsync(
        string snapshotId,
        string archivePath,
        CancellationToken cancellationToken)
    {
        if (!Path.GetExtension(archivePath).Equals(".migrawin", StringComparison.OrdinalIgnoreCase))
        {
            archivePath += ".migrawin";
        }

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "MigraWin-export-" + Guid.NewGuid().ToString("N"));
        var temporaryArchive = archivePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var exportRoot = await ExportSnapshotAsync(snapshotId, temporaryRoot, cancellationToken);
            ZipFile.CreateFromDirectory(
                exportRoot,
                temporaryArchive,
                CompressionLevel.Fastest,
                includeBaseDirectory: false);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivePath))!);
            File.Move(temporaryArchive, archivePath, true);
            return archivePath;
        }
        finally
        {
            TryDeleteDirectory(temporaryRoot);
            if (File.Exists(temporaryArchive))
            {
                File.Delete(temporaryArchive);
            }
        }
    }

    private static async Task<byte[]> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await SHA256.HashDataAsync(stream, cancellationToken);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public async Task<long> VerifyRepositoryAsync(CancellationToken cancellationToken)
    {
        var manifests = await ListSnapshotsAsync(cancellationToken);
        var ids = await CollectReferencedObjectsAsync(manifests, cancellationToken);
        foreach (var id in ids)
        {
            await Objects.VerifyFileAsync(id, cancellationToken);
        }

        return ids.Count;
    }

    public async Task ChangePasswordAsync(
        string currentSecret,
        string newPassword,
        CancellationToken cancellationToken)
    {
        await RepositoryCrypto.ChangePasswordAsync(
            Format,
            Path.Combine(Root, "format.json"),
            currentSecret,
            newPassword,
            cancellationToken);
    }

    public async Task<int> PruneSnapshotsAsync(
        int keepLast,
        string protectedSnapshotId,
        CancellationToken cancellationToken)
    {
        if (keepLast <= 0)
        {
            return 0;
        }

        var snapshots = await ListSnapshotsAsync(cancellationToken);
        var keep = new HashSet<string>(StringComparer.Ordinal) { protectedSnapshotId };
        foreach (var snapshot in snapshots.OrderByDescending(item => item.CreatedAt).Take(keepLast))
        {
            keep.Add(snapshot.SnapshotId);
        }

        var remove = snapshots.Where(snapshot => !keep.Contains(snapshot.SnapshotId)).ToArray();
        foreach (var snapshot in remove)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(SnapshotPath(snapshot.SnapshotId));
        }

        var remaining = await ListSnapshotsAsync(cancellationToken);
        var referenced = await CollectReferencedObjectsAsync(remaining, cancellationToken);
        foreach (var objectPath in Directory.EnumerateFiles(
                     Path.Combine(Root, "objects"),
                     "*.mwo",
                     SearchOption.AllDirectories))
        {
            var id = Path.GetFileNameWithoutExtension(objectPath);
            if (!referenced.Contains(id))
            {
                File.Delete(objectPath);
            }
        }

        return remove.Length;
    }

    private async Task<HashSet<string>> CollectReferencedObjectsAsync(
        IEnumerable<SnapshotSummary> summaries,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var summary in summaries)
        {
            var manifest = await LoadSnapshotAsync(summary.SnapshotId, cancellationToken);
            foreach (var file in manifest.Applications
                         .SelectMany(application => application.Components)
                         .SelectMany(component => component.Files))
            {
                ids.Add(file.ObjectId);
            }
        }

        return ids;
    }

    private string SnapshotPath(string snapshotId)
    {
        if (snapshotId.Length != 32 || snapshotId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Identificatore del backup non valido.", nameof(snapshotId));
        }

        return Path.Combine(Root, "snapshots", snapshotId + ".snapshot");
    }

    private static FileStream AcquireLock(string root)
    {
        Directory.CreateDirectory(root);
        var lockPath = Path.Combine(root, ".migrawin.lock");
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new IOException("Il repository è già in uso da un'altra operazione.", exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _keys.Dispose();
        await _lock.DisposeAsync();
    }
}
