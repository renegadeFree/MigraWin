using System.Text.Json;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class BackupDefaults
{
    public bool IncludeSettings { get; set; } = true;
    public bool IncludeData { get; set; } = true;
    public bool IncludeInstalledFiles { get; set; } = true;

    public void ApplyTo(InstalledApplication application)
    {
        if (IncludeSettings && application.HasSettings)
        {
            application.BackupSettings = true;
        }

        if (IncludeData && application.HasData)
        {
            application.BackupData = true;
        }

        if (IncludeInstalledFiles && application.HasInstalledFiles)
        {
            application.BackupInstalledFiles = true;
        }
    }
}

public sealed class AppSettingsStore
{
    private readonly string _path;

    public AppSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWin",
            "settings.json");
    }

    public async Task<BackupDefaults> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return new BackupDefaults();
        }

        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<BackupDefaults>(
                   stream,
                   JsonDefaults.Options,
                   cancellationToken)
               ?? new BackupDefaults();
    }

    public async Task SaveAsync(BackupDefaults settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonDefaults.Options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(true);
        }

        File.Move(temporaryPath, _path, true);
    }
}
