using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MigraWin.Infrastructure;
using MigraWin.Models;
using MigraWin.Services;

namespace MigraWin.ViewModels;

public sealed class ActivityItem
{
    public required DateTimeOffset Time { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required string Kind { get; init; }
    public string TimeLabel => Time.LocalDateTime.ToString("HH:mm");
    public string Glyph => Kind switch
    {
        "success" => "\uE73E",
        "warning" => "\uE7BA",
        "error" => "\uEA39",
        _ => "\uE946"
    };
}

public sealed class RestoreComponentItem : ObservableObject
{
    private bool _isSelected = true;

    public required SnapshotComponent Component { get; init; }
    public required string Key { get; init; }
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Label => Component.Sensitive ? Component.Label + " · sensibile" : Component.Label;
    public string CountLabel => $"{Component.Files.Count:N0} file · {InstalledApplication.FormatBytes(Component.Files.Sum(file => file.Length))}";
}

public sealed class RestoreApplicationItem : ObservableObject
{
    private bool _isSelected = true;

    public required SnapshotApplication Application { get; init; }
    public required ObservableCollection<RestoreComponentItem> Components { get; init; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                foreach (var component in Components)
                {
                    component.IsSelected = value;
                }
            }
        }
    }

    public string VersionLabel => $"{Application.Version} · {Application.Architecture}";
}

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly InventoryService _inventoryService = new();
    private readonly BackupService _backupService = new();
    private readonly RestoreService _restoreService = new();
    private readonly SnapshotComparisonService _comparisonService = new();
    private readonly PortableArchiveService _portableArchiveService = new();
    private readonly UpdateService _updateService = new();
    private readonly ProfileStore _profileStore = new();
    private readonly PackageCatalog _packageCatalog = new();
    private readonly HistoryStore _historyStore = new();
    private readonly LocalSecretStore _localSecretStore = new();
    private readonly ScheduleService _scheduleService = new();
    private readonly ApplicationArtworkService _artworkService = new();
    private readonly DialogService _dialogs = new();
    private RepositorySession? _repository;
    private CancellationTokenSource? _operationCancellation;
    private SnapshotManifest? _activeManifest;
    private int _navigationIndex;
    private string _statusText = "Pronto per analizzare il computer.";
    private string _searchText = string.Empty;
    private int _applicationFilterIndex;
    private bool _isBusy;
    private double _progressValue;
    private bool _isProgressIndeterminate;
    private InstalledApplication? _selectedApplication;
    private string _selectedIconStatus = string.Empty;
    private CancellationTokenSource? _artworkCancellation;
    private string _profileName = $"Backup del {DateTime.Now:dd/MM/yyyy}";
    private string _backupDestination = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "MigraWin Backups");
    private string _backupPassword = string.Empty;
    private string _backupPasswordConfirmation = string.Empty;
    private bool _isIncremental = true;
    private bool _isDifferential;
    private string _recoveryKey = string.Empty;
    private string _archivePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "MigraWin Backups");
    private string _archivePassword = string.Empty;
    private string _newRepositoryPassword = string.Empty;
    private string _newRepositoryPasswordConfirmation = string.Empty;
    private bool _repositoryOpened;
    private SnapshotSummary? _selectedSnapshot;
    private SnapshotSummary? _comparisonFromSnapshot;
    private SnapshotSummary? _comparisonToSnapshot;
    private string _comparisonSummary = "Apri un repository e confronta due punti di ripristino.";
    private bool _runInstallers;
    private int _conflictPolicyIndex = 1;
    private string _restoreAnalysis = "Carica un backup per analizzare la destinazione.";
    private PackageEntry? _selectedPackage;
    private BackupProfile? _selectedProfile;
    private int _themeChoiceIndex;
    private bool _includeStoreApps = true;
    private bool _includePersonalScripts = true;
    private int _retentionChoiceIndex;
    private bool _scheduleEnabled;
    private string _scheduleTime = "03:00";
    private string _scheduleStatus = "Nessuna pianificazione configurata.";
    private string _reportText = string.Empty;
    private AvailableUpdate? _availableUpdate;
    private string _updateStatus = "Controllo aggiornamenti non ancora eseguito.";
    private bool _isUpdating;
    private bool _isCompactNavigation;
    private bool _initialized;

    public MainViewModel()
    {
        ApplicationsView = CollectionViewSource.GetDefaultView(Applications);
        ApplicationsView.Filter = FilterApplication;
        ApplicationsView.SortDescriptions.Add(new SortDescription(nameof(InstalledApplication.DisplayName),
            ListSortDirection.Ascending));
        SnapshotsView = CollectionViewSource.GetDefaultView(Snapshots);
        _inventoryService.StatusChanged += message => StatusText = message;
        _backupService.ProgressChanged += OnProgress;
        _restoreService.ProgressChanged += OnProgress;

        NavigateCommand = new ParameterRelayCommand<string>(index =>
        {
            if (int.TryParse(index, out var value) && value is >= 0 and <= 8)
            {
                NavigationIndex = value;
            }
        });
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusy);
        AddPortableApplicationCommand = new RelayCommand(AddPortableApplication);
        SelectAllFilteredCommand = new RelayCommand(() =>
        {
            foreach (var application in ApplicationsView.Cast<InstalledApplication>())
            {
                application.IsSelected = true;
            }

            RefreshSummary();
        });
        ClearSelectionCommand = new RelayCommand(() =>
        {
            foreach (var application in Applications)
            {
                application.IsSelected = false;
            }

            RefreshSummary();
        });
        ApplyPresetCommand = new ParameterRelayCommand<BackupPreset>(ApplyPreset);
        BrowseBackupDestinationCommand = new RelayCommand(BrowseBackupDestination);
        AddCustomFolderCommand = new AsyncRelayCommand(AddCustomFolderAsync);
        AddExclusionCommand = new AsyncRelayCommand(AddExclusionAsync);
        ClearExclusionsCommand = new RelayCommand(ClearExclusions, () => SelectedApplication is not null);
        OpenOfficialPageCommand = new RelayCommand(OpenOfficialPage, () => HasOfficialUrl);
        BrowseInstallerCommand = new RelayCommand(BrowseInstaller);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(CancelOperation, () => IsBusy);
        CopyRecoveryKeyCommand = new RelayCommand(CopyRecoveryKey, () => RecoveryKey.Length > 0);
        BrowseRepositoryCommand = new RelayCommand(BrowseRepository);
        OpenPortableArchiveCommand = new AsyncRelayCommand(OpenPortableArchiveAsync, () => !IsBusy);
        OpenRepositoryCommand = new AsyncRelayCommand(OpenRepositoryAsync, () => !IsBusy);
        ChangeRepositoryPasswordCommand = new AsyncRelayCommand(
            ChangeRepositoryPasswordAsync,
            () => !IsBusy && _repository is not null);
        VerifySnapshotCommand = new AsyncRelayCommand(VerifySelectedSnapshotAsync, () => !IsBusy && SelectedSnapshot is not null);
        ExportSnapshotCommand = new AsyncRelayCommand(ExportSelectedSnapshotAsync, () => !IsBusy && SelectedSnapshot is not null);
        DeleteSnapshotCommand = new AsyncRelayCommand(DeleteSelectedSnapshotAsync, () => !IsBusy && SelectedSnapshot is not null);
        CompareSnapshotsCommand = new AsyncRelayCommand(
            CompareSnapshotsAsync,
            () => !IsBusy && ComparisonFromSnapshot is not null && ComparisonToSnapshot is not null);
        PrepareRestoreCommand = new AsyncRelayCommand(PrepareRestoreAsync, () => !IsBusy && SelectedSnapshot is not null);
        AnalyzeRestoreCommand = new RelayCommand(AnalyzeRestore, () => RestoreApplications.Count > 0);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => !IsBusy && RestoreApplications.Count > 0);
        ImportPackageCommand = new AsyncRelayCommand(ImportPackageAsync, () => !IsBusy);
        AssignPackageCommand = new ParameterRelayCommand<PackageEntry>(AssignPackage);
        RemovePackageCommand = new AsyncRelayCommand(RemoveSelectedPackageAsync, () => SelectedPackage is not null);
        SaveProfileCommand = new AsyncRelayCommand(SaveProfileAsync, () => Applications.Any(application => application.IsSelected));
        LoadProfileCommand = new RelayCommand(LoadSelectedProfile, () => SelectedProfile is not null);
        ExportProfileCommand = new AsyncRelayCommand(ExportProfileAsync, () => SelectedProfile is not null);
        ImportProfileCommand = new AsyncRelayCommand(ImportProfileAsync);
        RemoveScheduleCommand = new AsyncRelayCommand(RemoveSelectedScheduleAsync, () => SelectedProfile is not null);
        RefreshReportCommand = new AsyncRelayCommand(RefreshReportAsync);
        OpenReportsFolderCommand = new RelayCommand(OpenReportsFolder);
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsUpdating);
        InstallUpdateCommand = new AsyncRelayCommand(InstallUpdateAsync, () => AvailableUpdate is not null && !IsUpdating);
    }

    public ObservableCollection<InstalledApplication> Applications { get; } = [];
    public ICollectionView ApplicationsView { get; }
    public ObservableCollection<SnapshotSummary> Snapshots { get; } = [];
    public ICollectionView SnapshotsView { get; }
    public ObservableCollection<RestoreApplicationItem> RestoreApplications { get; } = [];
    public ObservableCollection<PathMapping> RestoreMappings { get; } = [];
    public ObservableCollection<ActivityItem> Activities { get; } = [];
    public ObservableCollection<ImageSource> SelectedProductImages { get; } = [];
    public ObservableCollection<PackageEntry> Packages { get; } = [];
    public ObservableCollection<BackupProfile> Profiles { get; } = [];
    public ObservableCollection<SnapshotChange> ComparisonChanges { get; } = [];
    public IEnumerable<InstalledApplication> SelectedApplications => Applications.Where(application => application.IsSelected);

    public ICommand NavigateCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand AddPortableApplicationCommand { get; }
    public ICommand SelectAllFilteredCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand BrowseBackupDestinationCommand { get; }
    public ICommand AddCustomFolderCommand { get; }
    public ICommand AddExclusionCommand { get; }
    public ICommand ClearExclusionsCommand { get; }
    public ICommand OpenOfficialPageCommand { get; }
    public ICommand BrowseInstallerCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CopyRecoveryKeyCommand { get; }
    public ICommand BrowseRepositoryCommand { get; }
    public ICommand OpenPortableArchiveCommand { get; }
    public ICommand OpenRepositoryCommand { get; }
    public ICommand ChangeRepositoryPasswordCommand { get; }
    public ICommand VerifySnapshotCommand { get; }
    public ICommand ExportSnapshotCommand { get; }
    public ICommand DeleteSnapshotCommand { get; }
    public ICommand CompareSnapshotsCommand { get; }
    public ICommand PrepareRestoreCommand { get; }
    public ICommand AnalyzeRestoreCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand ImportPackageCommand { get; }
    public ICommand AssignPackageCommand { get; }
    public ICommand RemovePackageCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand LoadProfileCommand { get; }
    public ICommand ExportProfileCommand { get; }
    public ICommand ImportProfileCommand { get; }
    public ICommand RemoveScheduleCommand { get; }
    public ICommand RefreshReportCommand { get; }
    public ICommand OpenReportsFolderCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand InstallUpdateCommand { get; }

    public int NavigationIndex
    {
        get => _navigationIndex;
        set
        {
            if (SetProperty(ref _navigationIndex, value))
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(IsOverviewPage));
                OnPropertyChanged(nameof(IsInventoryPage));
                OnPropertyChanged(nameof(IsBackupPage));
                OnPropertyChanged(nameof(IsArchivePage));
                OnPropertyChanged(nameof(IsComparisonPage));
                OnPropertyChanged(nameof(IsRestorePage));
                OnPropertyChanged(nameof(IsPackagesPage));
                OnPropertyChanged(nameof(IsActivityPage));
                OnPropertyChanged(nameof(IsSettingsPage));
            }
        }
    }

    public string PageTitle => NavigationIndex switch
    {
        0 => "Panoramica",
        1 => "Inventario applicazioni",
        2 => "Configura backup",
        3 => "Archivio backup",
        4 => "Confronto copie",
        5 => "Ripristino selettivo",
        6 => "Libreria pacchetti",
        7 => "Attività e rapporti",
        8 => "Impostazioni",
        _ => "MigraWin"
    };
    public bool IsOverviewPage => NavigationIndex == 0;
    public bool IsInventoryPage => NavigationIndex == 1;
    public bool IsBackupPage => NavigationIndex == 2;
    public bool IsArchivePage => NavigationIndex == 3;
    public bool IsComparisonPage => NavigationIndex == 4;
    public bool IsRestorePage => NavigationIndex == 5;
    public bool IsPackagesPage => NavigationIndex == 6;
    public bool IsActivityPage => NavigationIndex == 7;
    public bool IsSettingsPage => NavigationIndex == 8;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshApplicationsView();
            }
        }
    }

    public int ApplicationFilterIndex
    {
        get => _applicationFilterIndex;
        set
        {
            if (SetProperty(ref _applicationFilterIndex, value))
            {
                RefreshApplicationsView();
            }
        }
    }

    public InstalledApplication? SelectedApplication
    {
        get => _selectedApplication;
        set
        {
            if (SetProperty(ref _selectedApplication, value))
            {
                _artworkCancellation?.Cancel();
                _artworkCancellation?.Dispose();
                _artworkCancellation = null;
                SelectedProductImages.Clear();
                SelectedIconStatus = value is null
                    ? string.Empty
                    : value.IconSource is null
                        ? "Nessuna immagine locale disponibile."
                        : "Icona dell'installazione.";
                if (value is not null && HasHttpOfficialUrl(value.ProductUrl))
                {
                    _artworkCancellation = new CancellationTokenSource();
                    _ = LoadSelectedArtworkAsync(value, _artworkCancellation.Token);
                }

                OnPropertyChanged(nameof(HasSelectedApplication));
                OnPropertyChanged(nameof(HasOfficialUrl));
                OnPropertyChanged(nameof(HasProductImages));
                RaiseCommandStates();
            }
        }
    }

    public bool HasSelectedApplication => SelectedApplication is not null;
    public bool HasOfficialUrl => SelectedApplication is not null
                                   && Uri.TryCreate(SelectedApplication.ProductUrl, UriKind.Absolute, out var uri)
                                   && uri.Scheme is "http" or "https" or "ms-windows-store";
    public bool HasProductImages => SelectedProductImages.Count > 0;
    public string SelectedIconStatus
    {
        get => _selectedIconStatus;
        private set => SetProperty(ref _selectedIconStatus, value);
    }
    public string InstalledCountLabel => Applications.Count.ToString("N0");
    public string SelectedCountLabel => Applications.Count(application => application.IsSelected).ToString("N0");
    public string ExcludedGamesLabel => Applications.Count(application => application.IsGame).ToString("N0");
    public string LastBackupLabel => Snapshots.Count == 0 ? "Nessun backup" : Snapshots[0].DateLabel;
    public string SelectedSizeLabel => InstalledApplication.FormatBytes(
        Applications.Where(application => application.IsSelected).Sum(application => application.EstimatedSize));
    public string SelectedAppsSummary => $"{SelectedCountLabel} applicazioni · {SelectedSizeLabel} stimati";

    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value);
    }

    public string BackupDestination
    {
        get => _backupDestination;
        set => SetProperty(ref _backupDestination, value);
    }

    public string BackupPassword
    {
        get => _backupPassword;
        set
        {
            if (SetProperty(ref _backupPassword, value))
            {
                OnPropertyChanged(nameof(BackupReadinessText));
            }
        }
    }

    public string BackupPasswordConfirmation
    {
        get => _backupPasswordConfirmation;
        set
        {
            if (SetProperty(ref _backupPasswordConfirmation, value))
            {
                OnPropertyChanged(nameof(BackupReadinessText));
            }
        }
    }

    public bool IsIncremental
    {
        get => _isIncremental;
        set
        {
            if (SetProperty(ref _isIncremental, value) && value)
            {
                IsDifferential = false;
            }
        }
    }

    public bool IsDifferential
    {
        get => _isDifferential;
        set
        {
            if (SetProperty(ref _isDifferential, value) && value)
            {
                IsIncremental = false;
            }
        }
    }

    public string RecoveryKey
    {
        get => _recoveryKey;
        set
        {
            if (SetProperty(ref _recoveryKey, value))
            {
                OnPropertyChanged(nameof(HasRecoveryKey));
            }
        }
    }

    public bool HasRecoveryKey => RecoveryKey.Length > 0;
    public string BackupReadinessText
    {
        get
        {
            if (Applications.Count(application => application.IsSelected) == 0)
            {
                return "Seleziona almeno un'applicazione nell'inventario.";
            }

            if (BackupPassword.Length < 10)
            {
                return "La password deve contenere almeno 10 caratteri.";
            }

            if (BackupPassword != BackupPasswordConfirmation)
            {
                return "Le password non coincidono.";
            }

            if (string.IsNullOrWhiteSpace(BackupDestination))
            {
                return "Scegli una destinazione.";
            }

            if (ScheduleEnabled && !TimeOnly.TryParseExact(ScheduleTime, "HH:mm", out _))
            {
                return "L'orario pianificato deve usare il formato HH:mm.";
            }

            return "Pronto per la verifica e il backup.";
        }
    }

    public string ArchivePath
    {
        get => _archivePath;
        set => SetProperty(ref _archivePath, value);
    }

    public string ArchivePassword
    {
        get => _archivePassword;
        set => SetProperty(ref _archivePassword, value);
    }

    public string NewRepositoryPassword
    {
        get => _newRepositoryPassword;
        set => SetProperty(ref _newRepositoryPassword, value);
    }

    public string NewRepositoryPasswordConfirmation
    {
        get => _newRepositoryPasswordConfirmation;
        set => SetProperty(ref _newRepositoryPasswordConfirmation, value);
    }

    public bool RepositoryOpened
    {
        get => _repositoryOpened;
        set => SetProperty(ref _repositoryOpened, value);
    }

    public SnapshotSummary? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set
        {
            if (SetProperty(ref _selectedSnapshot, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public SnapshotSummary? ComparisonFromSnapshot
    {
        get => _comparisonFromSnapshot;
        set
        {
            if (SetProperty(ref _comparisonFromSnapshot, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public SnapshotSummary? ComparisonToSnapshot
    {
        get => _comparisonToSnapshot;
        set
        {
            if (SetProperty(ref _comparisonToSnapshot, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ComparisonSummary
    {
        get => _comparisonSummary;
        set => SetProperty(ref _comparisonSummary, value);
    }

    public bool RunInstallers
    {
        get => _runInstallers;
        set => SetProperty(ref _runInstallers, value);
    }

    public int ConflictPolicyIndex
    {
        get => _conflictPolicyIndex;
        set => SetProperty(ref _conflictPolicyIndex, value);
    }

    public string RestoreAnalysis
    {
        get => _restoreAnalysis;
        set => SetProperty(ref _restoreAnalysis, value);
    }

    public PackageEntry? SelectedPackage
    {
        get => _selectedPackage;
        set
        {
            if (SetProperty(ref _selectedPackage, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public BackupProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public int ThemeChoiceIndex
    {
        get => _themeChoiceIndex;
        set
        {
            if (SetProperty(ref _themeChoiceIndex, value))
            {
                ThemeManager.Apply((ThemePreference)value);
            }
        }
    }

    public bool IncludeStoreApps
    {
        get => _includeStoreApps;
        set => SetProperty(ref _includeStoreApps, value);
    }

    public bool IncludePersonalScripts
    {
        get => _includePersonalScripts;
        set => SetProperty(ref _includePersonalScripts, value);
    }

    public int RetentionChoiceIndex
    {
        get => _retentionChoiceIndex;
        set => SetProperty(ref _retentionChoiceIndex, value);
    }

    public bool ScheduleEnabled
    {
        get => _scheduleEnabled;
        set
        {
            if (SetProperty(ref _scheduleEnabled, value))
            {
                OnPropertyChanged(nameof(BackupReadinessText));
            }
        }
    }

    public string ScheduleTime
    {
        get => _scheduleTime;
        set
        {
            if (SetProperty(ref _scheduleTime, value))
            {
                OnPropertyChanged(nameof(BackupReadinessText));
            }
        }
    }

    public string ScheduleStatus
    {
        get => _scheduleStatus;
        set => SetProperty(ref _scheduleStatus, value);
    }

    public string ReportText
    {
        get => _reportText;
        set => SetProperty(ref _reportText, value);
    }

    public AvailableUpdate? AvailableUpdate
    {
        get => _availableUpdate;
        private set
        {
            if (SetProperty(ref _availableUpdate, value))
            {
                OnPropertyChanged(nameof(UpdateAvailable));
                OnPropertyChanged(nameof(UpdateVersionLabel));
                RaiseCommandStates();
            }
        }
    }

    public bool UpdateAvailable => AvailableUpdate is not null;
    public string UpdateVersionLabel => AvailableUpdate is null
        ? "Nessun aggiornamento disponibile"
        : $"Versione {AvailableUpdate.Version} disponibile";

    public string UpdateStatus
    {
        get => _updateStatus;
        private set => SetProperty(ref _updateStatus, value);
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        private set
        {
            if (SetProperty(ref _isUpdating, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsCompactNavigation
    {
        get => _isCompactNavigation;
        set => SetProperty(ref _isCompactNavigation, value);
    }

    public string VersionLabel => $"MigraWin {UpdateService.CurrentVersion} · Windows x64";
    public string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MigraWin");

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ThemeManager.Apply(ThemePreference.System);
        await _historyStore.InitializeAsync();
        foreach (var activity in await _historyStore.LoadRecentAsync())
        {
            Activities.Add(new ActivityItem
            {
                Time = activity.Time,
                Title = activity.Title,
                Detail = activity.Detail,
                Kind = activity.Kind
            });
        }

        await ReloadProfilesAsync();
        await ReloadPackagesAsync();
        await RefreshReportAsync();
        await CheckForUpdatesAsync(silent: true);
        AddActivity("Pronto", "MigraWin è pronto. Avvia l'analisi per costruire l'inventario.", "info");
        await _historyStore.PruneAsync();
    }

    public void SetBackupPassword(string password) => BackupPassword = password;
    public void SetBackupPasswordConfirmation(string password) => BackupPasswordConfirmation = password;
    public void SetArchivePassword(string password) => ArchivePassword = password;
    public void SetNewRepositoryPassword(string password) => NewRepositoryPassword = password;
    public void SetNewRepositoryPasswordConfirmation(string password) => NewRepositoryPasswordConfirmation = password;

    private async Task AnalyzeAsync()
    {
        BeginOperation("Analisi del computer in corso…", indeterminate: true);
        try
        {
            var inventory = await _inventoryService.ScanAsync(
                IncludeStoreApps,
                IncludePersonalScripts,
                _operationCancellation!.Token);
            Applications.Clear();
            foreach (var application in inventory)
            {
                application.PropertyChanged += ApplicationOnPropertyChanged;
                Applications.Add(application);
            }

            RefreshApplicationsView();
            RefreshSummary();
            NavigationIndex = 1;
            StatusText = $"Inventario completato: {Applications.Count:N0} applicazioni trovate.";
            AddActivity("Analisi completata",
                $"{Applications.Count:N0} applicazioni rilevate, {ExcludedGamesLabel} giochi esclusi.",
                "success");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analisi annullata.";
            AddActivity("Analisi annullata", "L'inventario precedente non è stato modificato.", "warning");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            AddActivity("Analisi non completata", exception.Message, "error");
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private void AddPortableApplication()
    {
        var executable = _dialogs.PickFile(
            "Seleziona l'eseguibile portabile",
            "Programmi|*.exe;*.cmd;*.bat;*.ps1|Tutti i file|*.*");
        if (executable is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(executable)!;
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(executable.ToUpperInvariant())));
        var application = new InstalledApplication
        {
            InstallationId = identity[..24],
            ProductId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directory.ToUpperInvariant())))[..24],
            DisplayName = Path.GetFileNameWithoutExtension(executable),
            Publisher = "Aggiunto manualmente",
            Version = "Non disponibile",
            Architecture = Environment.Is64BitProcess ? "x64" : "x86",
            Source = "Portabile",
            SourceDetail = "Eseguibile aggiunto dall'utente",
            InstallLocation = directory,
            DisplayIcon = executable,
            SupportLevel = SupportLevel.Archivable,
            BackupData = true,
            IsSelected = true
        };
        application.CustomDataRoots.Add(directory);
        application.PropertyChanged += ApplicationOnPropertyChanged;
        Applications.Add(application);
        SelectedApplication = application;
        RefreshApplicationsView();
        RefreshSummary();
        StatusText = $"{application.DisplayName} aggiunto come software portabile.";
        AddActivity("Software portabile aggiunto", executable, "info");
    }

    private void ApplyPreset(BackupPreset preset)
    {
        foreach (var application in Applications)
        {
            if (application.IsGame)
            {
                application.IsSelected = false;
                continue;
            }

            switch (preset)
            {
                case BackupPreset.InventoryOnly:
                    application.IsSelected = true;
                    application.BackupInstaller = false;
                    application.BackupSettings = false;
                    application.BackupData = false;
                    application.BackupInstalledFiles = false;
                    application.BackupCache = false;
                    break;
                case BackupPreset.SettingsOnly:
                    application.IsSelected = application.HasSettings;
                    application.BackupInstaller = false;
                    application.BackupSettings = application.HasSettings;
                    application.BackupData = false;
                    application.BackupInstalledFiles = false;
                    break;
                case BackupPreset.DataOnly:
                    application.IsSelected = application.HasData;
                    application.BackupInstaller = false;
                    application.BackupSettings = false;
                    application.BackupData = application.HasData;
                    application.BackupInstalledFiles = false;
                    break;
                case BackupPreset.PackageOnly:
                    application.IsSelected = application.HasInstaller;
                    application.BackupInstaller = application.HasInstaller;
                    application.BackupSettings = false;
                    application.BackupData = false;
                    application.BackupInstalledFiles = false;
                    break;
                case BackupPreset.SettingsAndData:
                    application.IsSelected = application.HasSettings || application.HasData;
                    application.BackupInstaller = false;
                    application.BackupSettings = application.HasSettings;
                    application.BackupData = application.HasData;
                    application.BackupInstalledFiles = false;
                    break;
                case BackupPreset.FullSupported:
                    application.IsSelected = application.HasSettings || application.HasData || application.HasInstaller;
                    application.BackupInstaller = application.HasInstaller;
                    application.BackupSettings = application.HasSettings;
                    application.BackupData = application.HasData;
                    application.BackupInstalledFiles = false;
                    break;
            }
        }

        RefreshSummary();
        StatusText = preset switch
        {
            BackupPreset.InventoryOnly => "Profilo inventario applicato: nessun contenuto verrà acquisito.",
            BackupPreset.SettingsOnly => "Profilo impostazioni applicato.",
            BackupPreset.DataOnly => "Profilo dati applicato.",
            BackupPreset.PackageOnly => "Profilo pacchetti applicato. Servono installer importati.",
            BackupPreset.SettingsAndData => "Profilo impostazioni e dati applicato.",
            BackupPreset.FullSupported => "Profilo completo applicato con eccezioni visibili.",
            _ => "Selezione aggiornata."
        };
    }

    private void BrowseBackupDestination()
    {
        var selected = _dialogs.PickFolder("Scegli dove conservare il repository", BackupDestination);
        if (selected is not null)
        {
            BackupDestination = selected;
            ArchivePath = selected;
        }
    }

    private async Task AddCustomFolderAsync()
    {
        if (SelectedApplication is null)
        {
            return;
        }

        var selected = _dialogs.PickFolder($"Aggiungi una cartella dati per {SelectedApplication.DisplayName}");
        if (selected is null)
        {
            return;
        }

        if (!SelectedApplication.CustomDataRoots.Contains(selected, StringComparer.OrdinalIgnoreCase))
        {
            SelectedApplication.CustomDataRoots.Add(selected);
            SelectedApplication.BackupData = true;
            RefreshSummary();
        }

        await Task.CompletedTask;
    }

    private async Task AddExclusionAsync()
    {
        if (SelectedApplication is null)
        {
            return;
        }

        var selected = _dialogs.PickFolder($"Scegli una cartella da escludere per {SelectedApplication.DisplayName}");
        if (selected is null)
        {
            return;
        }

        if (!SelectedApplication.ExcludedDataRoots.Contains(selected, StringComparer.OrdinalIgnoreCase))
        {
            SelectedApplication.ExcludedDataRoots.Add(selected);
        }

        await Task.CompletedTask;
    }

    private void ClearExclusions()
    {
        SelectedApplication?.ExcludedDataRoots.Clear();
    }

    private void BrowseInstaller()
    {
        if (SelectedApplication is null)
        {
            return;
        }

        var selected = _dialogs.PickFile(
            $"Installer per {SelectedApplication.DisplayName}",
            "Installer|*.exe;*.msi;*.msix;*.appx;*.zip|Tutti i file|*.*");
        if (selected is not null)
        {
            SelectedApplication.InstallerPath = selected;
            SelectedApplication.BackupInstaller = true;
            if (SelectedApplication.SupportLevel < SupportLevel.OfflineReinstall)
            {
                SelectedApplication.SupportLevel = SupportLevel.OfflineReinstall;
            }

            RefreshSummary();
        }
    }

    private async Task CreateBackupAsync()
    {
        if (Applications.All(application => !application.IsSelected))
        {
            _dialogs.ShowInfo("Seleziona almeno un'applicazione.", "Nessuna applicazione selezionata");
            return;
        }

        if (BackupPassword.Length < 10)
        {
            _dialogs.ShowInfo("Usa una password di almeno 10 caratteri.", "Password troppo corta");
            return;
        }

        if (BackupPassword != BackupPasswordConfirmation)
        {
            _dialogs.ShowInfo("Le password non coincidono.", "Verifica la password");
            return;
        }

        if (!_dialogs.Confirm(
                $"Verrà creato un repository cifrato in:\n{BackupDestination}\n\nLe applicazioni originali non verranno modificate. Continuare?",
                "Conferma backup"))
        {
            return;
        }

        if (_repositoryOpened
            && string.Equals(Path.GetFullPath(_archivePath), Path.GetFullPath(BackupDestination), StringComparison.OrdinalIgnoreCase))
        {
            await CloseRepositoryAsync();
        }

        BeginOperation("Preparazione del backup…", indeterminate: true);
        try
        {
            var (manifest, recoveryKey) = await _backupService.CreateBackupAsync(
                BackupDestination,
                BackupPassword,
                Applications,
                IsDifferential ? BackupScope.Differential
                : IsIncremental ? BackupScope.Incremental
                : BackupScope.Full,
                RetentionChoiceIndex switch { 0 => 10, 1 => 20, 2 => 50, _ => 0 },
                ProfileName,
                _operationCancellation!.Token);
            RecoveryKey = recoveryKey ?? string.Empty;
            await ReportService.WriteBackupReportAsync(
                manifest,
                BackupDestination,
                recoveryKey is not null,
                _operationCancellation.Token);
            ArchivePath = BackupDestination;
            ArchivePassword = BackupPassword;
            await OpenRepositoryCoreAsync(BackupPassword);
            ScheduleStatus = await UpdateScheduledProfileAsync(
                manifest,
                BackupPassword,
                _operationCancellation.Token);
            await RefreshReportAsync();
            StatusText = $"Backup verificato: {manifest.FileCount:N0} file da {manifest.Applications.Count} applicazioni. "
                         + ScheduleStatus;
            AddActivity("Backup verificato",
                $"{manifest.FileCount:N0} file, {InstalledApplication.FormatBytes(manifest.LogicalBytes)} logici, repository cifrato.",
                "success");
            NavigationIndex = 3;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Backup annullato prima della pubblicazione.";
            AddActivity("Backup annullato", "Il repository precedente è rimasto invariato.", "warning");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            AddActivity("Backup non completato", exception.Message, "error");
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private void CopyRecoveryKey()
    {
        if (RecoveryKey.Length > 0)
        {
            Clipboard.SetText(RecoveryKey);
            StatusText = "Chiave di recupero copiata. Conservala fuori dal repository.";
        }
    }

    private void BrowseRepository()
    {
        var selected = _dialogs.PickFolder("Apri un repository MigraWin", ArchivePath);
        if (selected is not null)
        {
            ArchivePath = selected;
        }
    }

    private async Task OpenPortableArchiveAsync()
    {
        var archivePath = _dialogs.PickFile(
            "Apri archivio MigraWin autonomo",
            "Archivio MigraWin|*.migrawin|Tutti i file|*.*");
        if (archivePath is null)
        {
            return;
        }

        BeginOperation("Estrazione sicura dell'archivio autonomo…", indeterminate: true);
        try
        {
            await CloseRepositoryAsync();
            ArchivePath = await _portableArchiveService.ExtractAsync(
                archivePath,
                _operationCancellation!.Token);
            if (ArchivePassword.Length > 0)
            {
                await OpenRepositoryCoreAsync(ArchivePassword);
                var verifiedObjects = await _repository!.VerifyRepositoryAsync(_operationCancellation.Token);
                StatusText = $"Archivio autonomo aperto e verificato: {verifiedObjects:N0} oggetti.";
            }
            else
            {
                StatusText = "Archivio autonomo estratto. Inserisci la password o la chiave di recupero e apri l'archivio.";
            }

            AddActivity("Archivio autonomo aperto",
                $"Origine: {Path.GetFileName(archivePath)}",
                "success");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            AddActivity("Archivio autonomo non valido", exception.Message, "error");
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task OpenRepositoryAsync()
    {
        if (string.IsNullOrWhiteSpace(ArchivePassword))
        {
            _dialogs.ShowInfo("Inserisci la password o la chiave di recupero del repository.");
            return;
        }

        BeginOperation("Apertura e verifica del repository…", indeterminate: true);
        try
        {
            await CloseRepositoryAsync();
            await OpenRepositoryCoreAsync(ArchivePassword);
            StatusText = $"Repository aperto: {Snapshots.Count:N0} backup disponibili.";
            AddActivity("Repository aperto", $"{Snapshots.Count:N0} punti di ripristino letti.", "success");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            AddActivity("Archivio non accessibile", exception.Message, "error");
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task OpenRepositoryCoreAsync(string secret)
    {
        _repository = await RepositorySession.OpenAsync(ArchivePath, secret);
        RepositoryOpened = true;
        Snapshots.Clear();
        foreach (var snapshot in await _repository.ListSnapshotsAsync(CancellationToken.None))
        {
            Snapshots.Add(snapshot);
        }

        SelectedSnapshot = Snapshots.FirstOrDefault();
        ResetComparisonSelection();
        OnPropertyChanged(nameof(LastBackupLabel));
        RaiseCommandStates();
    }

    private async Task VerifySelectedSnapshotAsync()
    {
        if (_repository is null || SelectedSnapshot is null)
        {
            return;
        }

        BeginOperation("Verifica crittografica degli oggetti…", indeterminate: true);
        try
        {
            var manifest = await _repository.LoadSnapshotAsync(SelectedSnapshot.SnapshotId, _operationCancellation!.Token);
            var count = await _repository.VerifySnapshotAsync(manifest, _operationCancellation.Token);
            StatusText = $"Verifica completata: {count:N0} oggetti distinti integri.";
            AddActivity("Verifica completata", $"{count:N0} oggetti autenticati e leggibili.", "success");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            AddActivity("Verifica fallita", exception.Message, "error");
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task ChangeRepositoryPasswordAsync()
    {
        if (_repository is null)
        {
            return;
        }

        if (ArchivePassword.Length == 0)
        {
            _dialogs.ShowInfo("Inserisci la password o la chiave attualmente valida.");
            return;
        }

        if (NewRepositoryPassword.Length < 10)
        {
            _dialogs.ShowInfo("La nuova password deve contenere almeno 10 caratteri.");
            return;
        }

        if (NewRepositoryPassword != NewRepositoryPasswordConfirmation)
        {
            _dialogs.ShowInfo("Le nuove password non coincidono.");
            return;
        }

        if (!_dialogs.Confirm(
                "La nuova password sostituirà quella corrente. La chiave di recupero resterà valida. Continuare?",
                "Cambia password"))
        {
            return;
        }

        BeginOperation("Riprotezione della chiave del repository…", indeterminate: true);
        try
        {
            await _repository.ChangePasswordAsync(
                ArchivePassword,
                NewRepositoryPassword,
                _operationCancellation!.Token);
            ArchivePassword = NewRepositoryPassword;
            BackupPassword = NewRepositoryPassword;
            BackupPasswordConfirmation = NewRepositoryPassword;
            foreach (var profile in Profiles.Where(profile =>
                         profile.ScheduleEnabled
                         && Path.GetFullPath(profile.Destination).Equals(
                             Path.GetFullPath(_repository.Root),
                             StringComparison.OrdinalIgnoreCase)))
            {
                await _localSecretStore.SaveAsync(profile.Id, NewRepositoryPassword, _operationCancellation.Token);
            }

            NewRepositoryPassword = string.Empty;
            NewRepositoryPasswordConfirmation = string.Empty;
            StatusText = "Password del repository aggiornata. La chiave di recupero resta valida.";
            AddActivity("Password repository cambiata",
                "Chiave dei contenuti riprotetta senza riscrivere gli oggetti.",
                "success");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            AddActivity("Cambio password non completato", exception.Message, "error");
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task CompareSnapshotsAsync()
    {
        if (_repository is null || ComparisonFromSnapshot is null || ComparisonToSnapshot is null)
        {
            return;
        }

        if (ComparisonFromSnapshot.SnapshotId == ComparisonToSnapshot.SnapshotId)
        {
            _dialogs.ShowInfo("Scegli due punti di ripristino diversi.");
            return;
        }

        BeginOperation("Confronto dei manifest…", indeterminate: true);
        try
        {
            var result = await _comparisonService.CompareAsync(
                _repository,
                ComparisonFromSnapshot,
                ComparisonToSnapshot,
                _operationCancellation!.Token);
            ComparisonChanges.Clear();
            foreach (var change in result.Changes)
            {
                ComparisonChanges.Add(change);
            }

            ComparisonSummary = result.ChangeSummary;
            var report = await SnapshotComparisonService.WriteReportAsync(
                result,
                null,
                _operationCancellation.Token);
            StatusText = $"Confronto completato. Rapporto: {report}";
            AddActivity("Confronto completato", result.ChangeSummary, "success");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            AddActivity("Confronto non completato", exception.Message, "error");
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task DeleteSelectedSnapshotAsync()
    {
        if (_repository is null || SelectedSnapshot is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                "Eliminare questo punto di backup? Gli oggetti non usati da altre copie verranno rimossi.",
                "Elimina backup"))
        {
            return;
        }

        BeginOperation("Aggiornamento della conservazione…", indeterminate: true);
        try
        {
            var id = SelectedSnapshot.SnapshotId;
            await _repository.DeleteSnapshotAsync(id, _operationCancellation!.Token);
            Snapshots.Clear();
            foreach (var snapshot in await _repository.ListSnapshotsAsync(_operationCancellation.Token))
            {
                Snapshots.Add(snapshot);
            }

            SelectedSnapshot = Snapshots.FirstOrDefault();
            ResetComparisonSelection();
            StatusText = "Backup eliminato; gli oggetti condivisi sono stati conservati.";
            AddActivity("Backup eliminato", "La pulizia ha protetto tutti gli oggetti ancora referenziati.", "warning");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task ExportSelectedSnapshotAsync()
    {
        if (_repository is null || SelectedSnapshot is null)
        {
            return;
        }

        var destination = _dialogs.SaveFile(
            "Esporta archivio autonomo",
            "Archivio MigraWin|*.migrawin",
            $"MigraWin-{SelectedSnapshot.SnapshotId[..8]}.migrawin");
        if (destination is null)
        {
            return;
        }

        BeginOperation("Copia e verifica degli oggetti necessari…", indeterminate: true);
        try
        {
            var exported = await _repository.ExportSnapshotArchiveAsync(
                SelectedSnapshot.SnapshotId,
                destination,
                _operationCancellation!.Token);
            StatusText = $"Archivio autonomo creato in {exported}";
            AddActivity("Archivio autonomo esportato", exported, "success");
            _dialogs.ShowInfo($"Archivio autonomo verificato:\n{exported}", "Esportazione completata");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            AddActivity("Esportazione non completata", exception.Message, "error");
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private void ResetComparisonSelection()
    {
        ComparisonToSnapshot = Snapshots.FirstOrDefault();
        ComparisonFromSnapshot = Snapshots.Skip(1).FirstOrDefault();
        ComparisonChanges.Clear();
        ComparisonSummary = Snapshots.Count < 2
            ? "Sono necessari almeno due punti di ripristino per eseguire un confronto."
            : "Scegli le due copie e avvia il confronto.";
    }

    private async Task PrepareRestoreAsync()
    {
        if (_repository is null || SelectedSnapshot is null)
        {
            return;
        }

        BeginOperation("Lettura del piano di ripristino…", indeterminate: true);
        try
        {
            _activeManifest = await _repository.LoadSnapshotAsync(
                SelectedSnapshot.SnapshotId,
                _operationCancellation!.Token);
            RestoreApplications.Clear();
            foreach (var application in _activeManifest.Applications)
            {
                var components = new ObservableCollection<RestoreComponentItem>(
                    application.Components.Select(component => new RestoreComponentItem
                    {
                        Component = component,
                        Key = RestoreOptions.ComponentKey(application.InstallationId, component),
                        IsSelected = !component.Sensitive
                    }));
                RestoreApplications.Add(new RestoreApplicationItem
                {
                    Application = application,
                    Components = components
                });
            }

            RestoreMappings.Clear();
            foreach (var root in _activeManifest.Applications
                         .SelectMany(application => application.Components)
                         .Select(component => component.Root)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var target = root == "${Packages}"
                    ? Path.Combine(Path.GetTempPath(), "MigraWinPackages", _activeManifest.SnapshotId)
                    : AdapterCatalog.Expand(root);
                RestoreMappings.Add(new PathMapping
                {
                    SourcePath = root,
                    TargetPath = target
                });
            }

            NavigationIndex = 5;
            AnalyzeRestore();
            StatusText = $"Piano pronto: {RestoreApplications.Count} applicazioni e {RestoreApplications.Sum(item => item.Components.Count)} componenti.";
            AddActivity("Piano di ripristino preparato",
                "Destinazione non modificata. Verifica le mappature prima di procedere.",
                "info");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private void AnalyzeRestore()
    {
        var conflicts = 0;
        var selectedFiles = 0;
        var mappings = RestoreMappings.ToDictionary(mapping => mapping.SourcePath, mapping => mapping.TargetPath,
            StringComparer.OrdinalIgnoreCase);
        foreach (var application in RestoreApplications.Where(item => item.IsSelected))
        {
            foreach (var component in application.Components.Where(item => item.IsSelected))
            {
                var root = mappings.GetValueOrDefault(component.Component.Root, string.Empty);
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                foreach (var file in component.Component.Files)
                {
                    selectedFiles++;
                    string target;
                    try
                    {
                        target = Path.GetFullPath(Path.Combine(root, file.RelativePath));
                    }
                    catch
                    {
                        conflicts++;
                        continue;
                    }

                    if (File.Exists(target))
                    {
                        conflicts++;
                    }
                }
            }
        }

        RestoreAnalysis = conflicts == 0
            ? $"{selectedFiles:N0} file pronti. Nessun conflitto rilevato nei percorsi attuali."
            : $"{selectedFiles:N0} file pronti. {conflicts:N0} file esistono già e seguiranno la politica scelta.";
    }

    private async Task RestoreAsync()
    {
        if (_repository is null || _activeManifest is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                "Il ripristino può creare o sostituire file nelle destinazioni mostrate. I file sostituiti vengono copiati nel rollback. Continuare?",
                "Conferma ripristino"))
        {
            return;
        }

        BeginOperation("Preparazione del ripristino…", indeterminate: true);
        try
        {
            var options = new RestoreOptions
            {
                ConflictPolicy = ConflictPolicyIndex switch
                {
                    0 => ConflictPolicy.ReplaceWithRollback,
                    2 => ConflictPolicy.KeepBoth,
                    _ => ConflictPolicy.KeepExisting
                },
                RunInstallers = RunInstallers
            };
            foreach (var mapping in RestoreMappings)
            {
                options.RootMappings[mapping.SourcePath] = mapping.TargetPath;
            }

            foreach (var application in RestoreApplications.Where(item => item.IsSelected))
            {
                options.ApplicationIds.Add(application.Application.InstallationId);
                foreach (var component in application.Components.Where(item => item.IsSelected))
                {
                    options.ComponentKeys.Add(component.Key);
                }
            }

            if (options.ApplicationIds.Count == 0 || options.ComponentKeys.Count == 0)
            {
                throw new InvalidOperationException("Seleziona almeno un componente da ripristinare.");
            }

            var result = await _restoreService.RestoreAsync(
                _repository,
                _activeManifest,
                options,
                _operationCancellation!.Token);
            await RefreshReportAsync();
            AddActivity(
                result.Errors.Count == 0 ? "Ripristino completato" : "Ripristino completato con eccezioni",
                $"{result.FilesRestored:N0} ripristinati, {result.FilesSkipped:N0} mantenuti, {result.Errors.Count} errori.",
                result.Errors.Count == 0 ? "success" : "warning");
            StatusText = result.Errors.Count == 0
                ? "Ripristino completato e verificato."
                : $"Ripristino completato con {result.Errors.Count} errori.";
            NavigationIndex = 7;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Ripristino annullato.";
            AddActivity("Ripristino annullato", "Le operazioni già concluse restano nel rapporto.", "warning");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            _dialogs.ShowError(exception.Message);
            AddActivity("Ripristino non completato", exception.Message, "error");
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task ImportPackageAsync()
    {
        var path = _dialogs.PickFile(
            "Importa un installer posseduto",
            "Installer|*.exe;*.msi;*.msix;*.appx;*.zip|Tutti i file|*.*");
        if (path is null)
        {
            return;
        }

        BeginOperation("Calcolo hash e controllo della firma…", indeterminate: true);
        try
        {
            var package = await _packageCatalog.ImportAsync(path, _operationCancellation!.Token);
            await ReloadPackagesAsync();
            SelectedPackage = Packages.First(item => item.Id == package.Id);
            StatusText = "Pacchetto importato. La firma rilevata non ne autorizza automaticamente l'esecuzione.";
            AddActivity("Pacchetto importato", $"{package.DisplayName} · SHA-256 {package.Sha256[..12]}…", "success");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            _dialogs.ShowError(exception.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private void AssignPackage(PackageEntry package)
    {
        if (SelectedApplication is null)
        {
            _dialogs.ShowInfo("Seleziona prima un'applicazione nell'inventario.");
            return;
        }

        if (!File.Exists(package.Path))
        {
            _dialogs.ShowError("Il file del pacchetto non è più disponibile nel percorso registrato.");
            return;
        }

        SelectedApplication.InstallerPath = package.Path;
        SelectedApplication.BackupInstaller = true;
        SelectedApplication.SupportLevel = SupportLevel.OfflineReinstall;
        StatusText = $"{package.DisplayName} associato a {SelectedApplication.DisplayName}.";
        AddActivity("Pacchetto associato", $"{package.DisplayName} → {SelectedApplication.DisplayName}", "info");
    }

    private async Task RemoveSelectedPackageAsync()
    {
        if (SelectedPackage is null)
        {
            return;
        }

        if (!_dialogs.Confirm("Rimuovere il pacchetto dal catalogo? Il file originale non verrà eliminato."))
        {
            return;
        }

        await _packageCatalog.RemoveAsync(SelectedPackage.Id, CancellationToken.None);
        await ReloadPackagesAsync();
    }

    private async Task<string> UpdateScheduledProfileAsync(
        SnapshotManifest manifest,
        string password,
        CancellationToken cancellationToken)
    {
        var existing = Profiles.FirstOrDefault(item =>
            item.Name.Equals(ProfileName, StringComparison.OrdinalIgnoreCase));
        if (!ScheduleEnabled && existing?.ScheduleEnabled != true)
        {
            return "Nessuna pianificazione configurata.";
        }

        var profile = existing ?? CreateProfileFromSelection();
        var previousScheduleEnabled = profile.ScheduleEnabled;
        profile.Destination = BackupDestination;
        profile.Scope = manifest.Scope;
        profile.KeepLastSnapshots = RetentionChoiceIndex switch { 0 => 10, 1 => 20, 2 => 50, _ => 0 };
        profile.ScheduleEnabled = ScheduleEnabled;
        profile.ScheduleTime = ScheduleTime.Trim();
        profile.UpdatedAt = DateTimeOffset.Now;
        profile.Applications = CreateProfileFromSelection().Applications;
        if (existing is null)
        {
            Profiles.Add(profile);
        }

        try
        {
            if (ScheduleEnabled)
            {
                await _localSecretStore.SaveAsync(profile.Id, password, cancellationToken);
                await _scheduleService.CreateDailyAsync(profile, profile.ScheduleTime, cancellationToken);
                await _profileStore.SaveAsync(Profiles, cancellationToken);
                SelectedProfile = profile;
                AddActivity("Pianificazione aggiornata",
                    $"{profile.Name} · ogni giorno alle {profile.ScheduleTime}.",
                    "success");
                return $"Backup pianificato ogni giorno alle {profile.ScheduleTime}.";
            }

            await _scheduleService.DeleteAsync(profile.Id, cancellationToken);
            _localSecretStore.Delete(profile.Id);
            await _profileStore.SaveAsync(Profiles, cancellationToken);
            SelectedProfile = profile;
            AddActivity("Pianificazione rimossa", profile.Name, "warning");
            return "Pianificazione automatica rimossa.";
        }
        catch (Exception exception)
        {
            profile.ScheduleEnabled = previousScheduleEnabled;
            StatusText = exception.Message;
            AddActivity("Pianificazione non aggiornata", exception.Message, "error");
            return "Backup riuscito, ma pianificazione non aggiornata: " + exception.Message;
        }
    }

    private async Task SaveProfileAsync()
    {
        var profile = CreateProfileFromSelection();
        var existing = Profiles.FirstOrDefault(item => item.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Preset = profile.Preset;
            existing.Destination = profile.Destination;
            existing.Scope = profile.Scope;
            existing.KeepLastSnapshots = profile.KeepLastSnapshots;
            existing.ScheduleEnabled = profile.ScheduleEnabled;
            existing.ScheduleTime = profile.ScheduleTime;
            existing.Applications = profile.Applications;
            existing.UpdatedAt = DateTimeOffset.Now;
        }
        else
        {
            Profiles.Add(profile);
        }

        await _profileStore.SaveAsync(Profiles, CancellationToken.None);
        SelectedProfile = Profiles.First(item => item.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        StatusText = $"Profilo «{profile.Name}» salvato senza password.";
        AddActivity("Profilo salvato", profile.Name, "success");
    }

    private void LoadSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        foreach (var application in Applications)
        {
            application.IsSelected = false;
        }

        var missing = 0;
        foreach (var entry in SelectedProfile.Applications)
        {
            var app = Applications.FirstOrDefault(item => item.InstallationId == entry.InstallationId);
            if (app is null)
            {
                missing++;
                continue;
            }

            app.IsSelected = true;
            app.BackupInstaller = entry.BackupInstaller;
            app.BackupSettings = entry.BackupSettings;
            app.BackupData = entry.BackupData;
            app.BackupInstalledFiles = entry.BackupInstalledFiles;
            app.BackupCache = entry.BackupCache;
            app.BackupSensitiveSettings = entry.BackupSensitiveSettings;
            app.InstallerPath = entry.InstallerPath;
            foreach (var root in entry.CustomDataRoots)
            {
                if (!app.CustomDataRoots.Contains(root, StringComparer.OrdinalIgnoreCase))
                {
                    app.CustomDataRoots.Add(root);
                }
            }

            app.ExcludedDataRoots.Clear();
            foreach (var root in entry.ExcludedDataRoots)
            {
                app.ExcludedDataRoots.Add(root);
            }
        }

        ProfileName = SelectedProfile.Name;
        BackupDestination = SelectedProfile.Destination;
        IsIncremental = SelectedProfile.Scope == BackupScope.Incremental;
        IsDifferential = SelectedProfile.Scope == BackupScope.Differential;
        ScheduleEnabled = SelectedProfile.ScheduleEnabled;
        ScheduleTime = SelectedProfile.ScheduleTime;
        ScheduleStatus = SelectedProfile.ScheduleEnabled
            ? $"Pianificazione giornaliera alle {SelectedProfile.ScheduleTime}."
            : "Nessuna pianificazione attiva per questo profilo.";
        RetentionChoiceIndex = SelectedProfile.KeepLastSnapshots switch
        {
            <= 10 => 0,
            <= 20 => 1,
            <= 50 => 2,
            _ => 2
        };
        RefreshSummary();
        StatusText = missing == 0
            ? $"Profilo «{SelectedProfile.Name}» applicato."
            : $"Profilo applicato; {missing} applicazioni del profilo non sono presenti su questo PC.";
    }

    private async Task ExportProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var path = _dialogs.SaveFile("Esporta profilo", "Profilo MigraWin|*.json", $"{SelectedProfile.Name}.json");
        if (path is null)
        {
            return;
        }

        await _profileStore.ExportAsync(path, SelectedProfile, CancellationToken.None);
        StatusText = "Profilo esportato senza segreti.";
    }

    private async Task ImportProfileAsync()
    {
        var path = _dialogs.PickFile("Importa profilo", "Profilo MigraWin|*.json");
        if (path is null)
        {
            return;
        }

        try
        {
            var profile = await _profileStore.ImportAsync(path, CancellationToken.None);
            Profiles.Add(profile);
            await _profileStore.SaveAsync(Profiles, CancellationToken.None);
            SelectedProfile = profile;
            StatusText = "Profilo importato. Le credenziali non sono incluse.";
        }
        catch (Exception exception)
        {
            _dialogs.ShowError(exception.Message);
        }
    }

    private async Task RemoveSelectedScheduleAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (!SelectedProfile.ScheduleEnabled)
        {
            _dialogs.ShowInfo("Il profilo selezionato non ha una pianificazione attiva.");
            return;
        }

        if (!_dialogs.Confirm($"Rimuovere il backup giornaliero di «{SelectedProfile.Name}»?"))
        {
            return;
        }

        await _scheduleService.DeleteAsync(SelectedProfile.Id);
        _localSecretStore.Delete(SelectedProfile.Id);
        SelectedProfile.ScheduleEnabled = false;
        SelectedProfile.UpdatedAt = DateTimeOffset.Now;
        await _profileStore.SaveAsync(Profiles);
        if (ProfileName.Equals(SelectedProfile.Name, StringComparison.OrdinalIgnoreCase))
        {
            ScheduleEnabled = false;
            ScheduleStatus = "Pianificazione automatica rimossa.";
        }

        AddActivity("Pianificazione rimossa", SelectedProfile.Name, "warning");
        StatusText = "Pianificazione rimossa e credenziale DPAPI eliminata.";
    }

    private BackupProfile CreateProfileFromSelection()
    {
        return new BackupProfile
        {
            Name = string.IsNullOrWhiteSpace(ProfileName) ? "Profilo senza nome" : ProfileName.Trim(),
            Destination = BackupDestination,
            Scope = IsDifferential ? BackupScope.Differential
                : IsIncremental ? BackupScope.Incremental
                : BackupScope.Full,
            KeepLastSnapshots = RetentionChoiceIndex switch { 0 => 10, 1 => 20, 2 => 50, _ => 0 },
            ScheduleEnabled = ScheduleEnabled,
            ScheduleTime = ScheduleTime,
            Applications = Applications.Where(application => application.IsSelected).Select(application => new ProfileApplication
            {
                InstallationId = application.InstallationId,
                ProductId = application.ProductId,
                DisplayName = application.DisplayName,
                BackupInstaller = application.BackupInstaller,
                BackupSettings = application.BackupSettings,
                BackupData = application.BackupData,
                BackupInstalledFiles = application.BackupInstalledFiles,
                BackupCache = application.BackupCache,
                BackupSensitiveSettings = application.BackupSensitiveSettings,
                InstallerPath = application.InstallerPath,
                CustomDataRoots = application.CustomDataRoots.ToList()
                ,
                ExcludedDataRoots = application.ExcludedDataRoots.ToList()
            }).ToList()
        };
    }

    private async Task ReloadProfilesAsync()
    {
        Profiles.Clear();
        foreach (var profile in await _profileStore.LoadAsync())
        {
            Profiles.Add(profile);
        }
    }

    private async Task ReloadPackagesAsync()
    {
        Packages.Clear();
        foreach (var package in await _packageCatalog.LoadAsync())
        {
            Packages.Add(package);
        }
    }

    private async Task RefreshReportAsync()
    {
        ReportText = await ReportService.ReadLastReportAsync();
    }

    private void OpenReportsFolder()
    {
        Directory.CreateDirectory(ReportService.ReportsDirectory);
        Process.Start(new ProcessStartInfo(ReportService.ReportsDirectory) { UseShellExecute = true });
    }

    private Task CheckForUpdatesAsync() => CheckForUpdatesAsync(silent: false);

    private async Task CheckForUpdatesAsync(bool silent)
    {
        IsUpdating = true;
        UpdateStatus = "Controllo delle release GitHub…";
        try
        {
            AvailableUpdate = await _updateService.CheckAsync();
            UpdateStatus = AvailableUpdate is null
                ? $"MigraWin {UpdateService.CurrentVersion} è aggiornato."
                : $"Disponibile MigraWin {AvailableUpdate.Version}. L'installazione richiede una tua conferma.";
            if (!silent && AvailableUpdate is null)
            {
                _dialogs.ShowInfo(UpdateStatus, "Aggiornamenti");
            }
        }
        catch (Exception exception)
        {
            UpdateStatus = "Controllo aggiornamenti non disponibile: " + exception.Message;
            if (!silent)
            {
                _dialogs.ShowError(UpdateStatus, "Aggiornamenti");
            }
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (AvailableUpdate is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Scaricare e avviare l'installer di MigraWin {AvailableUpdate.Version}?\n\nL'app verrà chiusa per consentire l'aggiornamento.",
                "Installa aggiornamento"))
        {
            return;
        }

        IsUpdating = true;
        try
        {
            UpdateStatus = "Download dell'aggiornamento…";
            var installer = await _updateService.DownloadInstallerAsync(
                AvailableUpdate,
                new Progress<double>(fraction =>
                    UpdateStatus = $"Download installer: {fraction:P0}"),
                CancellationToken.None);
            UpdateStatus = "Avvio dell'installer…";
            Process.Start(new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{installer}\" /passive /norestart",
                UseShellExecute = true
            });
            await Task.Delay(300);
            Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            UpdateStatus = "Aggiornamento non completato: " + exception.Message;
            _dialogs.ShowError(UpdateStatus, "Aggiornamenti");
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private bool FilterApplication(object item)
    {
        if (item is not InstalledApplication application)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            if (!application.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                && !application.Publisher.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                && !application.Version.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
        }

        return ApplicationFilterIndex switch
        {
            1 => application.SupportLevel >= SupportLevel.Archivable,
            2 => application.SupportLevel == SupportLevel.Detected,
            3 => application.IsGame,
            4 => application.IsSelected,
            5 => application.Source == "Script personale",
            _ => true
        };
    }

    private void RefreshApplicationsView()
    {
        ApplicationsView.Refresh();
    }

    private void ApplicationOnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(InstalledApplication.IsSelected)
            or nameof(InstalledApplication.BackupData)
            or nameof(InstalledApplication.BackupSettings)
            or nameof(InstalledApplication.HasInstaller)
            or nameof(InstalledApplication.EstimatedSize))
        {
            RefreshSummary();
        }
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(InstalledCountLabel));
        OnPropertyChanged(nameof(SelectedCountLabel));
        OnPropertyChanged(nameof(ExcludedGamesLabel));
        OnPropertyChanged(nameof(SelectedSizeLabel));
        OnPropertyChanged(nameof(SelectedAppsSummary));
        OnPropertyChanged(nameof(SelectedApplications));
        OnPropertyChanged(nameof(BackupReadinessText));
        RaiseCommandStates();
    }

    private void OnProgress(BackupProgress progress)
    {
        StatusText = $"{progress.Phase}: {progress.Detail}";
        if (progress.Total > 0)
        {
            IsProgressIndeterminate = false;
            ProgressValue = progress.Fraction * 100;
        }
    }

    private void BeginOperation(string status, bool indeterminate)
    {
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        IsProgressIndeterminate = indeterminate;
        ProgressValue = 0;
        StatusText = status;
    }

    private void EndOperation()
    {
        IsBusy = false;
        IsProgressIndeterminate = false;
        ProgressValue = 0;
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        RaiseCommandStates();
    }

    private void CancelOperation()
    {
        _operationCancellation?.Cancel();
        StatusText = "Annullamento richiesto. Attendo il punto sicuro corrente…";
    }

    private void AddActivity(string title, string detail, string kind)
    {
        var activity = new ActivityItem
        {
            Time = DateTimeOffset.Now,
            Title = title,
            Detail = detail,
            Kind = kind
        };
        Activities.Insert(0, activity);
        while (Activities.Count > 100)
        {
            Activities.RemoveAt(Activities.Count - 1);
        }

        _ = PersistActivityAsync(activity);
    }

    private async Task PersistActivityAsync(ActivityItem activity)
    {
        try
        {
            await _historyStore.RecordAsync(activity.Time, activity.Title, activity.Detail, activity.Kind);
        }
        catch
        {
            // A history failure must not invalidate a completed backup or restore.
        }
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new[]
                 {
                     AnalyzeCommand, CreateBackupCommand, CancelCommand, CopyRecoveryKeyCommand,
                     OpenPortableArchiveCommand, OpenRepositoryCommand, VerifySnapshotCommand, DeleteSnapshotCommand,
                     ChangeRepositoryPasswordCommand, ExportSnapshotCommand, CompareSnapshotsCommand,
                     PrepareRestoreCommand, RestoreCommand,
                     ImportPackageCommand, RemovePackageCommand,
                     SaveProfileCommand, LoadProfileCommand, ExportProfileCommand, RemoveScheduleCommand,
                     ClearExclusionsCommand, OpenOfficialPageCommand, CheckForUpdatesCommand, InstallUpdateCommand
                 })
        {
            switch (command)
            {
                case AsyncRelayCommand asyncCommand:
                    asyncCommand.RaiseCanExecuteChanged();
                    break;
                case RelayCommand relayCommand:
                    relayCommand.RaiseCanExecuteChanged();
                    break;
            }
        }
    }

    private async Task CloseRepositoryAsync()
    {
        if (_repository is not null)
        {
            await _repository.DisposeAsync();
            _repository = null;
        }

        RepositoryOpened = false;
    }

    public async ValueTask DisposeAsync()
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _artworkCancellation?.Cancel();
        _artworkCancellation?.Dispose();
        await CloseRepositoryAsync();
    }

    private async Task LoadSelectedArtworkAsync(
        InstalledApplication application,
        CancellationToken cancellationToken)
    {
        if (application.IconSource is null)
        {
            SelectedIconStatus = "Nessuna icona locale disponibile.";
        }

        IReadOnlyList<ImageSource> productImages;
        try
        {
            productImages = await _artworkService.LoadOfficialProductImagesAsync(application, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            productImages = [];
        }

        if (cancellationToken.IsCancellationRequested
            || !ReferenceEquals(SelectedApplication, application))
        {
            return;
        }

        foreach (var image in productImages)
        {
            SelectedProductImages.Add(image);
        }

        OnPropertyChanged(nameof(HasProductImages));
        if (application.IconSource is not null)
        {
            SelectedIconStatus = "Icona dell'installazione.";
        }
    }

    private void OpenOfficialPage()
    {
        var url = SelectedApplication?.ProductUrl;
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https" or "ms-windows-store"))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            _dialogs.ShowError($"Impossibile aprire il sito ufficiale: {exception.Message}");
        }
    }

    private static bool HasHttpOfficialUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";
}
