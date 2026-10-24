using MigraWin.Models;
using MigraWin.Services;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

if (args.Length == 2 && args[0] == "--git-adapter-roundtrip")
{
    await RunGitAdapterRoundTripAsync(args[1]);
    return;
}

var root = Path.Combine(Path.GetTempPath(), "MigraWinTests-" + Guid.NewGuid().ToString("N"));
var source = Path.Combine(root, "source");
var repositoryPath = Path.Combine(root, "repository");
var restored = Path.Combine(root, "restored");
var reports = Path.Combine(root, "reports");
const string password = "correct-horse-battery-staple";

try
{
    Directory.CreateDirectory(source);
    await File.WriteAllTextAsync(Path.Combine(source, "settings.json"), """{"theme":"dark"}""");
    Directory.CreateDirectory(Path.Combine(source, "Profiles"));
    Directory.CreateDirectory(Path.Combine(source, "Ignored"));
    await File.WriteAllTextAsync(Path.Combine(source, "Profiles", "copy.json"), """{"theme":"dark"}""");
    await File.WriteAllTextAsync(Path.Combine(source, "notes.txt"), "contenuto di prova");
    await File.WriteAllTextAsync(Path.Combine(source, "Ignored", "large.bin"), "escluso");
    await File.WriteAllTextAsync(
        Path.Combine(source, "credentials.json"),
        """{"api_key":"valore-che-non-deve-essere-salvato-per-impostazione"}""");

    var application = new InstalledApplication
    {
        InstallationId = "test-installation",
        ProductId = "test-product",
        DisplayName = "Applicazione di test",
        Version = "1.0.0",
        Source = "Test",
        Architecture = "x64",
        SupportLevel = SupportLevel.Archivable,
        IsSelected = true,
        BackupData = true
    };
    application.CustomDataRoots.Add(source);
    application.ExcludedDataRoots.Add(Path.Combine(source, "Ignored"));

    var backup = new BackupService();
    var (manifest, recoveryKey) = await backup.CreateBackupAsync(
        repositoryPath,
        password,
        [application],
        BackupScope.Incremental,
        10,
        "Collaudo",
        CancellationToken.None);
    Assert(!string.IsNullOrWhiteSpace(recoveryKey), "La chiave di recupero deve essere generata.");
    Assert(manifest.FileCount == 3,
        $"Devono essere acquisiti tutti i file non sensibili (trovati {manifest.FileCount}).");
    Assert(manifest.Applications.Count == 1, "Il manifest deve contenere l'applicazione.");
    Assert(manifest.Warnings.Any(warning => warning.Contains("sensibile", StringComparison.OrdinalIgnoreCase)),
        "Un file con credenziali deve essere escluso senza autorizzazione esplicita.");

    var objectFiles = Directory.GetFiles(Path.Combine(repositoryPath, "objects"), "*.mwo", SearchOption.AllDirectories);
    Assert(objectFiles.Length == 2, "La deduplicazione deve conservare due oggetti distinti.");

    await AssertThrowsAsync<UnauthorizedAccessException>(
        () => RepositorySession.OpenAsync(repositoryPath, "password-sbagliata"),
        "Una password errata deve essere rifiutata.");

    await using (var recoverySession = await RepositorySession.OpenAsync(repositoryPath, recoveryKey!))
    {
        Assert((await recoverySession.ListSnapshotsAsync(CancellationToken.None)).Count == 1,
            "La chiave di recupero deve aprire il repository.");
    }

    await using var opened = await RepositorySession.OpenAsync(repositoryPath, password);
    await AssertThrowsAsync<IOException>(
        () => RepositorySession.OpenAsync(repositoryPath, password),
        "Una seconda scrittura concorrente sullo stesso repository deve essere rifiutata.");
    var snapshots = await opened.ListSnapshotsAsync(CancellationToken.None);
    Assert(snapshots.Count == 1, "Il repository deve contenere un backup.");
    var loaded = await opened.LoadSnapshotAsync(manifest.SnapshotId, CancellationToken.None);
    var component = loaded.Applications[0].Components.First(item => item.Files.Count > 0);
    var options = new RestoreOptions { ConflictPolicy = ConflictPolicy.KeepExisting };
    options.ApplicationIds.Add(loaded.Applications[0].InstallationId);
    options.ComponentKeys.Add(RestoreOptions.ComponentKey(loaded.Applications[0].InstallationId, component));
    options.RootMappings[component.Root] = restored;

    var restore = new RestoreService(reports);
    var result = await restore.RestoreAsync(opened, loaded, options, CancellationToken.None);
    Assert(result.FilesRestored == 3, "Devono essere ripristinati tutti i file.");
    Assert(result.Errors.Count == 0, "Il ripristino non deve produrre errori.");
    Assert(
        await File.ReadAllTextAsync(Path.Combine(restored, "notes.txt")) == "contenuto di prova",
        "Il contenuto ripristinato deve coincidere.");

    var conflictTarget = Path.Combine(root, "conflict-target");
    Directory.CreateDirectory(conflictTarget);
    await File.WriteAllTextAsync(Path.Combine(conflictTarget, "notes.txt"), "contenuto della destinazione");
    options.RootMappings[component.Root] = conflictTarget;
    options.ConflictPolicy = ConflictPolicy.ReplaceWithRollback;
    var replacedResult = await restore.RestoreAsync(opened, loaded, options, CancellationToken.None);
    Assert(replacedResult.FilesReplaced == 1 && replacedResult.Errors.Count == 0,
        "La sostituzione deve creare una copia preventiva senza errori.");
    Assert(
        await File.ReadAllTextAsync(Path.Combine(conflictTarget, "notes.txt")) == "contenuto di prova",
        "Il file di destinazione deve essere sostituito dal contenuto ripristinato.");
    Assert(
        Directory.EnumerateFiles(replacedResult.RollbackDirectory, "notes.txt", SearchOption.AllDirectories)
            .Any(path => File.ReadAllText(path) == "contenuto della destinazione"),
        "Il contenuto precedente deve essere conservato nel rollback.");

    var keepBothTarget = Path.Combine(root, "keep-both-target");
    Directory.CreateDirectory(keepBothTarget);
    await File.WriteAllTextAsync(Path.Combine(keepBothTarget, "settings.json"), "destinazione");
    options.RootMappings[component.Root] = keepBothTarget;
    options.ConflictPolicy = ConflictPolicy.KeepBoth;
    var keepBothResult = await restore.RestoreAsync(opened, loaded, options, CancellationToken.None);
    Assert(keepBothResult.Errors.Count == 0
           && File.Exists(Path.Combine(keepBothTarget, "settings (1).json")),
        "La politica Salva entrambi deve conservare il file esistente e creare un nuovo nome.");

    var exportedRoot = await opened.ExportSnapshotAsync(
        manifest.SnapshotId,
        Path.Combine(root, "exported"),
        CancellationToken.None);
    await using (var exportedSession = await RepositorySession.OpenAsync(exportedRoot, password))
    {
        Assert((await exportedSession.ListSnapshotsAsync(CancellationToken.None)).Count == 1,
            "L'archivio autonomo deve essere apribile senza il repository originale.");
    }

    var portableArchive = Path.Combine(root, "portable.migrawin");
    await opened.ExportSnapshotArchiveAsync(
        manifest.SnapshotId,
        portableArchive,
        CancellationToken.None);
    var importedRoot = await new PortableArchiveService(Path.Combine(root, "archive-cache"))
        .ExtractAsync(portableArchive, CancellationToken.None);
    await using (var importedSession = await RepositorySession.OpenAsync(importedRoot, password))
    {
        Assert((await importedSession.ListSnapshotsAsync(CancellationToken.None)).Count == 1,
            "L'archivio .migrawin deve poter essere importato come repository autonomo.");
    }

    var maliciousArchive = Path.Combine(root, "malicious.migrawin");
    using (var zip = ZipFile.Open(maliciousArchive, ZipArchiveMode.Create))
    {
        var entry = zip.CreateEntry("../escape.txt");
        await using var writer = new StreamWriter(entry.Open());
        await writer.WriteAsync("non deve uscire");
    }

    await AssertThrowsAsync<InvalidDataException>(
        () => new PortableArchiveService(Path.Combine(root, "archive-cache"))
            .ExtractAsync(maliciousArchive, CancellationToken.None),
        "Un archivio con percorso traversal deve essere rifiutato.");
    Assert(!File.Exists(Path.Combine(root, "escape.txt")),
        "L'estrazione non deve scrivere fuori dalla cache.");

    var older = JsonSerializer.Deserialize<SnapshotManifest>(
        JsonSerializer.Serialize(loaded, JsonDefaults.Options),
        JsonDefaults.Options)!;
    older.SnapshotId = Guid.NewGuid().ToString("N");
    older.CreatedAt = loaded.CreatedAt.AddDays(-1);
    await opened.SaveSnapshotAsync(older, CancellationToken.None);
    var snapshotsForComparison = await opened.ListSnapshotsAsync(CancellationToken.None);
    var comparison = await new SnapshotComparisonService().CompareAsync(
        opened,
        snapshotsForComparison[1],
        snapshotsForComparison[0],
        CancellationToken.None);
    Assert(comparison.ApplicationsAdded == 0
           && comparison.ApplicationsRemoved == 0
           && comparison.Changes.Single().Kind == "no-change",
        "Due manifest equivalenti non devono produrre false variazioni.");
    var removed = await opened.PruneSnapshotsAsync(1, manifest.SnapshotId, CancellationToken.None);
    Assert(removed == 1 && (await opened.ListSnapshotsAsync(CancellationToken.None)).Count == 1,
        "La conservazione deve rimuovere solo le copie oltre il limite e proteggere l'ultima.");

    var malicious = JsonSerializer.Deserialize<SnapshotManifest>(
        JsonSerializer.Serialize(loaded, JsonDefaults.Options),
        JsonDefaults.Options)!;
    foreach (var file in malicious.Applications
                 .SelectMany(application => application.Components)
                 .SelectMany(component => component.Files))
    {
        file.RelativePath = @"..\fuori.txt";
    }

    var traversalTarget = Path.Combine(root, "traversal-target");
    options.RootMappings[component.Root] = traversalTarget;
    await AssertThrowsAsync<InvalidDataException>(
        () => restore.RestoreAsync(opened, malicious, options, CancellationToken.None),
        "Un percorso relativo che esce dalla destinazione deve essere rifiutato.");
    Assert(!File.Exists(Path.Combine(root, "fuori.txt")),
        "Il ripristino non deve scrivere fuori dalla destinazione.");

    var profilePath = Path.Combine(root, "profiles.json");
    var profiles = new ProfileStore(profilePath);
    await profiles.SaveAsync([new BackupProfile
    {
        Name = "Profilo test",
        Applications =
        [
            new ProfileApplication
            {
                InstallationId = application.InstallationId,
                ProductId = application.ProductId,
                DisplayName = application.DisplayName,
                BackupData = true
            }
        ]
    }]);
    var storedProfiles = await profiles.LoadAsync();
    Assert(storedProfiles.Count == 1 && storedProfiles[0].Name == "Profilo test", "Il profilo deve essere ripristinato.");

    var history = new HistoryStore(Path.Combine(root, "history.db"));
    await history.InitializeAsync();
    await history.RecordAsync(DateTimeOffset.Now, "Test", "Cronologia persistente", "success");
    var historyItems = await history.LoadRecentAsync();
    Assert(historyItems.Count == 1 && historyItems[0].Title == "Test",
        "La cronologia SQLite deve sopravvivere alla riapertura.");

    var updateBytes = Encoding.UTF8.GetBytes("installer-fittizio");
    var updateHash = Convert.ToHexString(SHA256.HashData(updateBytes));
    using var releaseDocument = JsonDocument.Parse(
        $$"""
        {
          "tag_name": "v9.0.0",
          "html_url": "https://github.com/renegadeFree/MigraWin/releases/tag/v9.0.0",
          "body": "Aggiornamento di prova",
          "draft": false,
          "prerelease": false,
          "assets": [
            {
              "name": "MigraWin-9.0.0-x64.msi",
              "browser_download_url": "https://github.com/renegadeFree/MigraWin/releases/download/v9.0.0/MigraWin-9.0.0-x64.msi"
            },
            {
              "name": "SHA256SUMS.txt",
              "browser_download_url": "https://github.com/renegadeFree/MigraWin/releases/download/v9.0.0/SHA256SUMS.txt"
            }
          ]
        }
        """);
    var updateHandler = new StubHttpHandler(request =>
        request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt", StringComparison.Ordinal)
            ? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent($"{updateHash}  MigraWin-9.0.0-x64.msi\n")
            }
            : new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(updateBytes)
            });
    var updateService = new UpdateService(
        new HttpClient(updateHandler),
        updateDirectory: Path.Combine(root, "updates"));
    Assert(UpdateService.DefaultUpdateDirectory().EndsWith(
               Path.Combine("MigraWinUpdates"),
               StringComparison.OrdinalIgnoreCase),
        "La cartella predefinita degli aggiornamenti deve essere stabile e accessibile a Windows Installer.");
    var availableUpdate = updateService.ParseRelease(releaseDocument.RootElement, new Version(1, 4, 0));
    Assert(availableUpdate is { Version.Major: 9 },
        "Il client aggiornamenti deve riconoscere una release più recente.");
    var downloadedInstaller = await updateService.DownloadInstallerAsync(
        availableUpdate!,
        null,
        CancellationToken.None);
    Assert(File.Exists(downloadedInstaller)
           && (await File.ReadAllBytesAsync(downloadedInstaller)).SequenceEqual(updateBytes),
        "L'installer aggiornato deve essere scaricato solo dopo la verifica SHA-256.");
    var badHashHandler = new StubHttpHandler(request =>
        request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt", StringComparison.Ordinal)
            ? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent($"{new string('0', 64)}  MigraWin-9.0.0-x64.msi\n")
            }
            : new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(updateBytes)
            });
    await AssertThrowsAsync<CryptographicException>(
        () => new UpdateService(
                new HttpClient(badHashHandler),
                updateDirectory: Path.Combine(root, "bad-updates"))
            .DownloadInstallerAsync(availableUpdate!, null, CancellationToken.None),
        "Un installer con hash errato deve essere rifiutato.");

    var scheduleId = Guid.NewGuid().ToString("N");
    var secretStore = new LocalSecretStore(Path.Combine(root, "secrets"));
    await secretStore.SaveAsync(scheduleId, password);
    Assert(await secretStore.LoadAsync(scheduleId) == password,
        "La credenziale protetta deve essere recuperabile dallo stesso utente.");
    var protectedCredential = await File.ReadAllBytesAsync(
        Path.Combine(root, "secrets", scheduleId + ".credential"));
    Assert(!ContainsSequence(protectedCredential, Encoding.UTF8.GetBytes(password)),
        "La password non deve comparire in chiaro nella credenziale.");
    secretStore.Delete(scheduleId);

    var scheduleService = new ScheduleService();
    var scheduleProfile = new BackupProfile { Id = scheduleId, Name = "Test pianificazione" };
    await scheduleService.CreateDailyAsync(scheduleProfile, "23:59");
    Assert(await scheduleService.ExistsAsync(scheduleId),
        "L'attività pianificata deve essere visibile in Task Scheduler.");
    await scheduleService.DeleteAsync(scheduleId);
    Assert(!await scheduleService.ExistsAsync(scheduleId),
        "La rimozione deve eliminare l'attività pianificata.");
    scheduleProfile.ScheduleEnabled = true;
    var cleanupProfiles = new ProfileStore(Path.Combine(root, "cleanup-profiles.json"));
    await cleanupProfiles.SaveAsync([scheduleProfile]);
    await secretStore.SaveAsync(scheduleId, password);
    await scheduleService.CreateDailyAsync(scheduleProfile, "23:58");
    Assert(await ScheduledMaintenance.RemoveAllSchedulesAsync(
               CancellationToken.None,
               cleanupProfiles,
               scheduleService,
               secretStore) == 0,
        "La pulizia della disinstallazione deve completarsi.");
    Assert(!await scheduleService.ExistsAsync(scheduleId)
           && !File.Exists(Path.Combine(root, "secrets", scheduleId + ".credential"))
           && (await cleanupProfiles.LoadAsync()).Single().ScheduleEnabled == false,
        "La pulizia deve rimuovere attività, credenziale e stato pianificato.");

    var scheduledApplication = (await new InventoryService().ScanAsync(
            includeStoreApps: false,
            cancellationToken: CancellationToken.None))
        .First(application => !application.IsGame);
    var runnerProfileId = Guid.NewGuid().ToString("N");
    var runnerProfiles = new ProfileStore(Path.Combine(root, "scheduled-profiles.json"));
    await runnerProfiles.SaveAsync(
    [
        new BackupProfile
        {
            Id = runnerProfileId,
            Name = "Backup pianificato test",
            Destination = Path.Combine(root, "scheduled-repository"),
            ScheduleEnabled = true,
            ScheduleTime = "23:59",
            Applications =
            [
                new ProfileApplication
                {
                    InstallationId = scheduledApplication.InstallationId,
                    ProductId = scheduledApplication.ProductId,
                    DisplayName = scheduledApplication.DisplayName
                }
            ]
        }
    ]);
    var runnerSecrets = new LocalSecretStore(Path.Combine(root, "runner-secrets"));
    await runnerSecrets.SaveAsync(runnerProfileId, password);
    var scheduledResult = await ScheduledBackupRunner.RunAsync(
        runnerProfileId,
        CancellationToken.None,
        runnerProfiles,
        runnerSecrets,
        new HistoryStore(Path.Combine(root, "runner-history.db")),
        Path.Combine(root, "runner.log"),
        Path.Combine(root, "runner-reports"),
        includeStoreApps: false);
    Assert(scheduledResult.ExitCode == 0 && File.Exists(scheduledResult.ReportPath),
        "Il runner pianificato deve creare un nuovo snapshot e un rapporto.");
    await using (var scheduledRepository = await RepositorySession.OpenAsync(
                     Path.Combine(root, "scheduled-repository"),
                     password))
    {
        Assert((await scheduledRepository.ListSnapshotsAsync(CancellationToken.None)).Count == 1,
            "Il backup pianificato deve essere pubblicato nel repository.");
    }

    var differentialRepository = Path.Combine(root, "differential-repository");
    application.BackupSensitiveSettings = true;
    var sensitiveRepository = Path.Combine(root, "sensitive-repository");
    var (sensitiveManifest, _) = await backup.CreateBackupAsync(
        sensitiveRepository,
        password,
        [application],
        BackupScope.Full,
        10,
        "Segreti espliciti",
        CancellationToken.None);
    Assert(sensitiveManifest.FileCount == 4,
        "L'inclusione esplicita deve acquisire anche il file sensibile.");
    application.BackupSensitiveSettings = false;
    await backup.CreateBackupAsync(
        differentialRepository,
        password,
        [application],
        BackupScope.Full,
        10,
        "Base differenziale",
        CancellationToken.None);
    var (differentialManifest, _) = await backup.CreateBackupAsync(
        differentialRepository,
        password,
        [application],
        BackupScope.Differential,
        10,
        "Differenziale",
        CancellationToken.None);
    Assert(differentialManifest.Scope == BackupScope.Differential
           && differentialManifest.ParentSnapshotId is not null,
        "Il backup differenziale deve mantenere il riferimento alla base.");

    var passwordRepository = Path.Combine(root, "password-repository");
    var (_, passwordRecoveryKey) = await backup.CreateBackupAsync(
        passwordRepository,
        password,
        [application],
        BackupScope.Full,
        10,
        "Cambio password",
        CancellationToken.None);
    await using (var passwordSession = await RepositorySession.OpenAsync(passwordRepository, password))
    {
        await passwordSession.ChangePasswordAsync(
            password,
            "nuova-password-robusta",
            CancellationToken.None);
    }

    await AssertThrowsAsync<UnauthorizedAccessException>(
        () => RepositorySession.OpenAsync(passwordRepository, password),
        "La vecchia password non deve più aprire il repository.");
    await using (var newPasswordSession = await RepositorySession.OpenAsync(
                     passwordRepository,
                     "nuova-password-robusta"))
    {
        Assert((await newPasswordSession.ListSnapshotsAsync(CancellationToken.None)).Count == 1,
            "La nuova password deve aprire il repository.");
    }

    await using (var recoveryAfterChange = await RepositorySession.OpenAsync(
                     passwordRepository,
                     passwordRecoveryKey!))
    {
        Assert((await recoveryAfterChange.ListSnapshotsAsync(CancellationToken.None)).Count == 1,
            "La chiave di recupero deve restare valida dopo il cambio password.");
    }

    var originalObject = objectFiles.OrderBy(path => path).First();
    await using (var stream = new FileStream(originalObject, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        stream.Position = stream.Length - 1;
        var value = stream.ReadByte();
        stream.Position--;
        stream.WriteByte((byte)(value ^ 0xFF));
    }

    var tamperedRestore = Path.Combine(root, "tampered");
    options.RootMappings[component.Root] = tamperedRestore;
    options.ConflictPolicy = ConflictPolicy.ReplaceWithRollback;
    var tamperedResult = await restore.RestoreAsync(opened, loaded, options, CancellationToken.None);
    Assert(tamperedResult.Errors.Count > 0, "Un oggetto manomesso deve produrre un errore esplicito.");

    Console.WriteLine(
        "OK: cifratura, cambio password, recupero, lock, deduplicazione, incrementale, differenziale, confronto, esportazione .migrawin, conservazione, segreti, profili, SQLite, DPAPI, pianificazione, conflitti, rollback, ripristino e manomissione verificati.");
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, true);
    }
}

return;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static async Task AssertThrowsAsync<T>(Func<Task> action, string message) where T : Exception
{
    try
    {
        await action();
    }
    catch (T)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static bool ContainsSequence(byte[] haystack, byte[] needle)
{
    for (var index = 0; index <= haystack.Length - needle.Length; index++)
    {
        if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle))
        {
            return true;
        }
    }

    return false;
}

static async Task RunGitAdapterRoundTripAsync(string workRoot)
{
    var sourceConfig = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gitconfig");
    Assert(File.Exists(sourceConfig), "La fixture Git locale non esiste.");
    Directory.CreateDirectory(workRoot);
    try
    {
        var applications = await new InventoryService().ScanAsync(false, CancellationToken.None);
        var git = applications.FirstOrDefault(application =>
            application.DisplayName.Equals("Git", StringComparison.OrdinalIgnoreCase));
        Assert(git is not null, "L'adattatore Git non è stato rilevato.");
        git!.IsSelected = true;
        git.BackupSettings = true;
        git.BackupData = false;
        var repositoryPath = Path.Combine(workRoot, "repository");
        var (manifest, _) = await new BackupService().CreateBackupAsync(
            repositoryPath,
            "Git-adapter-password-2026",
            [git],
            BackupScope.Full,
            10,
            "Git adapter end-to-end",
            CancellationToken.None);
        await using var repository = await RepositorySession.OpenAsync(
            repositoryPath,
            "Git-adapter-password-2026");
        var manifestApplication = manifest.Applications.Single();
        Assert(manifestApplication.Components.Any(component => component.Files.Count > 0),
            $"L'adattatore Git non ha acquisito file (avvisi: {string.Join(" | ", manifest.Warnings)}).");
        var restoreRoot = Path.Combine(workRoot, "restored-profile");
        var options = new RestoreOptions { ConflictPolicy = ConflictPolicy.KeepExisting };
        options.ApplicationIds.Add(manifestApplication.InstallationId);
        foreach (var component in manifestApplication.Components)
        {
            var key = RestoreOptions.ComponentKey(manifestApplication.InstallationId, component);
            if (component.Files.Count == 0)
            {
                continue;
            }

            options.ComponentKeys.Add(key);
            options.RootMappings[component.Root] = restoreRoot;
        }

        var result = await new RestoreService(Path.Combine(workRoot, "reports"))
            .RestoreAsync(repository, manifest, options, CancellationToken.None);
        Assert(result.Errors.Count == 0 && result.FilesRestored > 0,
            $"Il ripristino dell'adattatore Git non è riuscito (file {result.FilesRestored}, errori: {string.Join(" | ", result.Errors)}).");
        var restoredConfig = Path.Combine(restoreRoot, ".gitconfig");
        Assert(File.Exists(restoredConfig), "La configurazione Git non è stata ripristinata.");
        Assert((await File.ReadAllTextAsync(restoredConfig)) ==
               (await File.ReadAllTextAsync(sourceConfig)),
            "La configurazione Git ripristinata deve coincidere con l'originale.");
        Console.WriteLine("OK: adattatore Git reale, backup cifrato e ripristino isolato verificati.");
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, true);
        }
    }
}

sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(responseFactory(request));
}
