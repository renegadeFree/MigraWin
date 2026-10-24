using System.Security.Cryptography;
using System.Text.Json;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class PackageEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = "Origine locale";
    public string Path { get; set; } = string.Empty;
    public long Length { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool SignaturePresent { get; set; }
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.Now;
    public string SizeLabel => InstalledApplication.FormatBytes(Length);
    public string SignatureLabel => LocalizationService.Translate(
        SignaturePresent ? "Firma rilevata" : "Firma non verificata");
}

public sealed class PackageCatalog
{
    private readonly string _path;

    public PackageCatalog(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWin",
            "packages.json");
    }

    public async Task<IReadOnlyList<PackageEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<List<PackageEntry>>(stream, JsonDefaults.Options, cancellationToken)
               ?? [];
    }

    public async Task<PackageEntry> ImportAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Installer non trovato.", path);
        }

        var info = new FileInfo(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var entry = new PackageEntry
        {
            DisplayName = Path.GetFileNameWithoutExtension(path),
            Path = path,
            Length = info.Length,
            Sha256 = Convert.ToHexString(hash).ToLowerInvariant(),
            SignaturePresent = HasEmbeddedSignature(path)
        };
        var packages = (await LoadAsync(cancellationToken)).ToList();
        packages.RemoveAll(package => package.Sha256.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase));
        packages.Add(entry);
        await SaveAsync(packages, cancellationToken);
        return entry;
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken)
    {
        var packages = (await LoadAsync(cancellationToken)).Where(package => package.Id != id).ToArray();
        await SaveAsync(packages, cancellationToken);
    }

    private async Task SaveAsync(IReadOnlyList<PackageEntry> packages, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, packages, JsonDefaults.Options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(true);
        }

        File.Move(temp, _path, true);
    }

    private static bool HasEmbeddedSignature(string path)
    {
        try
        {
            using var certificate = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);
            return certificate.Subject.Length > 0;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
