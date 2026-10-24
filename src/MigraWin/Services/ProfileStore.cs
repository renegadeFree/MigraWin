using System.Text.Json;
using System.Text.Json.Serialization;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class ProfileStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions ProfileOptions = new(JsonDefaults.Options)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public ProfileStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWin",
            "profiles.json");
    }

    public async Task<IReadOnlyList<BackupProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<List<BackupProfile>>(
                   stream,
                   ProfileOptions,
                   cancellationToken)
               ?? throw new InvalidDataException("Elenco dei profili non valido.");
    }

    public async Task SaveAsync(IReadOnlyList<BackupProfile> profiles, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, profiles, ProfileOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(true);
        }

        File.Move(temp, _path, true);
    }

    public async Task ExportAsync(string destination, BackupProfile profile, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, profile, ProfileOptions, cancellationToken);
    }

    public async Task<BackupProfile> ImportAsync(string source, CancellationToken cancellationToken)
    {
        if (new FileInfo(source).Length > 10 * 1024 * 1024)
        {
            throw new InvalidDataException("Il profilo supera il limite di 10 MB.");
        }

        await using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        var profile = await JsonSerializer.DeserializeAsync<BackupProfile>(
                          stream,
                          ProfileOptions,
                          cancellationToken)
                      ?? throw new InvalidDataException("Profilo non valido.");
        if (string.IsNullOrWhiteSpace(profile.Name)
            || profile.Applications is null
            || profile.Applications.Any(application =>
                string.IsNullOrWhiteSpace(application.InstallationId)
                || string.IsNullOrWhiteSpace(application.ProductId)
                || string.IsNullOrWhiteSpace(application.DisplayName)))
        {
            throw new InvalidDataException("Il profilo non contiene le identità necessarie.");
        }
        profile.Id = Guid.NewGuid().ToString("N");
        profile.UpdatedAt = DateTimeOffset.Now;
        return profile;
    }
}
