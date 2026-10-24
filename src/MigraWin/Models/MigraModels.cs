using System.Collections.ObjectModel;
using System.Windows.Media;
using MigraWin.Infrastructure;
using MigraWin.Services;

namespace MigraWin.Models;

public enum SupportLevel
{
    Detected = 0,
    Archivable = 1,
    OnlineReinstall = 2,
    OfflineReinstall = 3,
    VerifiedMigration = 4
}

public enum BackupPreset
{
    InventoryOnly,
    SettingsOnly,
    DataOnly,
    PackageOnly,
    SettingsAndData,
    FullSupported,
    Custom
}

public enum BackupScope
{
    Full,
    Incremental,
    Differential
}

public enum ConflictPolicy
{
    KeepExisting,
    ReplaceWithRollback,
    KeepBoth
}

public enum InventoryOrigin
{
    Registry,
    MicrosoftStore,
    Portable,
    Manual
}

public sealed class InstalledApplication : ObservableObject
{
    private bool _isSelected;
    private bool _backupInstaller;
    private bool _backupSettings = true;
    private bool _backupData = true;
    private bool _backupInstalledFiles;
    private bool _backupCache;
    private bool _backupSensitiveSettings;
    private string _installerPath = string.Empty;
    private SupportLevel _supportLevel;
    private ImageSource? _iconSource;
    private bool _iconResolved;

    public required string InstallationId { get; init; }
    public required string ProductId { get; init; }
    public required string DisplayName { get; init; }
    public string Publisher { get; init; } = "Origine sconosciuta";
    public string Version { get; init; } = "Non disponibile";
    public string Architecture { get; init; } = "Non disponibile";
    public string Source { get; init; } = "Windows";
    public string SourceDetail { get; init; } = string.Empty;
    public string InstallLocation { get; init; } = string.Empty;
    public string DisplayIcon { get; init; } = string.Empty;
    public string ProductUrl { get; set; } = string.Empty;
    public string PackageFamilyName { get; init; } = string.Empty;
    public string UninstallString { get; init; } = string.Empty;
    public string UserName { get; init; } = Environment.UserName;
    public bool IsGame { get; init; }
    public long EstimatedSize { get; init; }
    public List<string> SettingsRoots { get; init; } = [];
    public List<string> DataRoots { get; init; } = [];
    public ObservableCollection<string> CustomDataRoots { get; } = [];
    public ObservableCollection<string> ExcludedDataRoots { get; } = [];

    public SupportLevel SupportLevel
    {
        get => _supportLevel;
        set
        {
            if (SetProperty(ref _supportLevel, value))
            {
                OnPropertyChanged(nameof(SupportLabel));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var accepted = value && !IsGame;
            if (SetProperty(ref _isSelected, accepted))
            {
                OnPropertyChanged(nameof(SelectionLabel));
            }
        }
    }

    public bool BackupInstaller
    {
        get => _backupInstaller;
        set => SetProperty(ref _backupInstaller, value && HasInstaller);
    }

    public bool BackupSettings
    {
        get => _backupSettings;
        set => SetProperty(ref _backupSettings, value);
    }

    public bool BackupData
    {
        get => _backupData;
        set => SetProperty(ref _backupData, value);
    }

    public bool BackupInstalledFiles
    {
        get => _backupInstalledFiles;
        set => SetProperty(ref _backupInstalledFiles, value);
    }

    public bool BackupCache
    {
        get => _backupCache;
        set => SetProperty(ref _backupCache, value);
    }

    public bool BackupSensitiveSettings
    {
        get => _backupSensitiveSettings;
        set => SetProperty(ref _backupSensitiveSettings, value);
    }

    public string InstallerPath
    {
        get => _installerPath;
        set
        {
            if (SetProperty(ref _installerPath, value))
            {
                OnPropertyChanged(nameof(HasInstaller));
                if (string.IsNullOrWhiteSpace(value))
                {
                    BackupInstaller = false;
                }
            }
        }
    }

    public bool HasInstaller => !string.IsNullOrWhiteSpace(InstallerPath) && File.Exists(InstallerPath);
    public bool HasSettings => SettingsRoots.Count > 0;
    public bool HasData => DataRoots.Count + CustomDataRoots.Count > 0;
    public bool HasInstalledFiles => !string.IsNullOrWhiteSpace(InstallLocation)
                                     && Directory.Exists(InstallLocation);
    public ImageSource? IconSource
    {
        get
        {
            if (!_iconResolved)
            {
                _iconResolved = true;
                _iconSource = ApplicationArtworkService.LoadLocalIcon(this);
            }

            return _iconSource;
        }
    }

    public string IconInitials
    {
        get
        {
            var words = DisplayName.Split(
                [' ', '-', '_', '.', '(', ')', '[', ']'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var letters = words
                .Select(word => word.FirstOrDefault(char.IsLetterOrDigit))
                .Where(letter => letter != '\0')
                .Take(2)
                .ToArray();
            return letters.Length == 0 ? "?" : new string(letters).ToUpperInvariant();
        }
    }

    public string SupportLabel => SupportLevel switch
    {
        SupportLevel.Detected => "Rilevato",
        SupportLevel.Archivable => "Archiviabile",
        SupportLevel.OnlineReinstall => "Reinstallabile online",
        SupportLevel.OfflineReinstall => "Reinstallabile offline",
        SupportLevel.VerifiedMigration => "Migrazione verificata",
        _ => "Rilevato"
    };
    public string SelectionLabel => IsGame ? "Gioco escluso" : IsSelected ? "Nel backup" : "Non selezionato";
    public string SizeLabel => EstimatedSize > 0 ? FormatBytes(EstimatedSize) : "Da calcolare";
    public string VersionLabel => $"{Version} · {Architecture}";

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }
}

public sealed class BackupProfile : ObservableObject
{
    private string _name = "Nuovo profilo";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    public BackupPreset Preset { get; set; } = BackupPreset.Custom;
    public string Destination { get; set; } = string.Empty;
    public BackupScope Scope { get; set; } = BackupScope.Incremental;
    public int KeepLastSnapshots { get; set; } = 10;
    public bool ScheduleEnabled { get; set; }
    public string ScheduleTime { get; set; } = "03:00";
    public bool IncludeNewApplications { get; set; }
    public List<ProfileApplication> Applications { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class ProfileApplication
{
    public required string InstallationId { get; init; }
    public required string ProductId { get; init; }
    public required string DisplayName { get; init; }
    public bool BackupInstaller { get; init; }
    public bool BackupSettings { get; init; }
    public bool BackupData { get; init; }
    public bool BackupInstalledFiles { get; init; }
    public bool BackupCache { get; init; }
    public bool BackupSensitiveSettings { get; init; }
    public string InstallerPath { get; init; } = string.Empty;
    public List<string> CustomDataRoots { get; init; } = [];
    public List<string> ExcludedDataRoots { get; init; } = [];
}

public sealed class SnapshotSummary
{
    public required string SnapshotId { get; init; }
    public required string ProfileName { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required int ApplicationCount { get; init; }
    public required int FileCount { get; init; }
    public required long LogicalBytes { get; init; }
    public required string CreatedBy { get; init; }
    public required string MachineName { get; init; }
    public required bool IsIncremental { get; init; }
    public required bool IsDifferential { get; init; }
    public string DateLabel => CreatedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
    public string SizeLabel => InstalledApplication.FormatBytes(LogicalBytes);
    public string ContentLabel => $"{ApplicationCount} app · {FileCount:N0} file";
    public string ScopeLabel => IsDifferential ? "Differenziale" : IsIncremental ? "Incrementale" : "Completa";
}

public sealed class SnapshotManifest
{
    public int SchemaVersion { get; set; } = 1;
    public required string SnapshotId { get; set; }
    public required string RepositoryId { get; set; }
    public string? ParentSnapshotId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public required string MachineName { get; set; }
    public required string CreatedBy { get; set; }
    public required string ProfileName { get; set; }
    public BackupScope Scope { get; set; } = BackupScope.Incremental;
    public List<SnapshotApplication> Applications { get; set; } = [];
    public int FileCount { get; set; }
    public long LogicalBytes { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public sealed class SnapshotApplication
{
    public required string InstallationId { get; set; }
    public required string ProductId { get; set; }
    public required string DisplayName { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public SupportLevel SupportLevel { get; set; }
    public List<SnapshotComponent> Components { get; set; } = [];
    public string SupportLabel => SupportLevel switch
    {
        SupportLevel.Detected => "Rilevato",
        SupportLevel.Archivable => "Archiviabile",
        SupportLevel.OnlineReinstall => "Reinstallabile online",
        SupportLevel.OfflineReinstall => "Reinstallabile offline",
        SupportLevel.VerifiedMigration => "Migrazione verificata",
        _ => "Rilevato"
    };
}

public sealed class SnapshotComponent
{
    public required string Kind { get; set; }
    public required string Label { get; set; }
    public required string Root { get; set; }
    public bool Sensitive { get; set; }
    public List<SnapshotFile> Files { get; set; } = [];
    public List<string> EmptyDirectories { get; set; } = [];
}

public sealed class SnapshotFile
{
    public required string RelativePath { get; set; }
    public required string ObjectId { get; set; }
    public long Length { get; set; }
    public DateTimeOffset LastWriteTime { get; set; }
    public FileAttributes Attributes { get; set; }
}

public sealed class BackupProgress
{
    public string Phase { get; init; } = "Preparazione";
    public string Detail { get; init; } = string.Empty;
    public int Completed { get; init; }
    public int Total { get; init; }
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Completed / Total, 0, 1);
}

public sealed class RestoreResult
{
    public int FilesRestored { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesReplaced { get; set; }
    public int InstallerLaunches { get; set; }
    public string RollbackDirectory { get; set; } = string.Empty;
    public string ReportPath { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class PathMapping : ObservableObject
{
    private string _targetPath = string.Empty;

    public required string SourcePath { get; init; }
    public string DisplaySource => SourcePath;
    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value);
    }
}
