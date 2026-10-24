using System.IO.Compression;
using System.Security.Cryptography;

namespace MigraWin.Services;

public sealed class PortableArchiveService
{
    private const long MaxArchiveBytes = 128L * 1024 * 1024 * 1024;
    private const long MaxExtractedBytes = 512L * 1024 * 1024 * 1024;
    private const int MaxEntries = 2_000_000;
    private readonly string _cacheRoot;

    public PortableArchiveService(string? cacheRoot = null)
    {
        _cacheRoot = cacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWin",
            "ImportedArchives");
    }

    public async Task<string> ExtractAsync(string archivePath, CancellationToken cancellationToken)
    {
        var archiveInfo = new FileInfo(archivePath);
        if (!archiveInfo.Exists
            || !archiveInfo.Extension.Equals(".migrawin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Seleziona un archivio MigraWin .migrawin.");
        }

        if (archiveInfo.Length <= 0 || archiveInfo.Length > MaxArchiveBytes)
        {
            throw new InvalidDataException("Dimensione dell'archivio non valida.");
        }

        Directory.CreateDirectory(_cacheRoot);
        var archiveHash = Convert.ToHexString(
            await HashFileAsync(archivePath, cancellationToken)).ToLowerInvariant();
        var destination = Path.Combine(_cacheRoot, archiveHash);
        if (File.Exists(Path.Combine(destination, "format.json")))
        {
            return destination;
        }

        var temporary = Path.Combine(_cacheRoot, ".extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            if (zip.Entries.Count > MaxEntries)
            {
                throw new InvalidDataException("L'archivio contiene troppi elementi.");
            }

            long extractedBytes = 0;
            var root = Path.GetFullPath(temporary).TrimEnd('\\') + "\\";
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                {
                    throw new InvalidDataException("L'archivio contiene collegamenti simbolici non consentiti.");
                }

                if (string.IsNullOrWhiteSpace(entry.FullName))
                {
                    continue;
                }

                var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("L'archivio tenta di estrarre fuori dalla destinazione.");
                }

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                extractedBytes = checked(extractedBytes + entry.Length);
                if (extractedBytes > MaxExtractedBytes)
                {
                    throw new InvalidDataException("Contenuto decompresso oltre il limite consentito.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var source = entry.Open();
                await using var output = new FileStream(
                    target,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    1024 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(true);
            }

            if (!File.Exists(Path.Combine(temporary, "format.json"))
                || !Directory.Exists(Path.Combine(temporary, "snapshots"))
                || !Directory.Exists(Path.Combine(temporary, "objects")))
            {
                throw new InvalidDataException("L'archivio non contiene un repository MigraWin valido.");
            }

            try
            {
                Directory.Move(temporary, destination);
            }
            catch (IOException) when (Directory.Exists(destination))
            {
                Directory.Delete(temporary, true);
            }

            return destination;
        }
        catch
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }

            throw;
        }
    }

    private static async Task<byte[]> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SHA256.HashDataAsync(stream, cancellationToken);
    }
}
