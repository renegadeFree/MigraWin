using MigraWin.Models;

namespace MigraWin.Services;

public sealed record ScheduledRunResult(int ExitCode, string Message, string? ReportPath);

public static class ScheduledBackupRunner
{
    public static bool IsScheduledInvocation(IReadOnlyList<string> arguments) =>
        arguments.Any(argument => argument.Equals("--scheduled-backup", StringComparison.OrdinalIgnoreCase));

    public static string? GetArgumentValue(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    public static async Task<ScheduledRunResult> RunAsync(
        string profileId,
        CancellationToken cancellationToken = default,
        ProfileStore? profileStore = null,
        LocalSecretStore? secretStore = null,
        HistoryStore? historyStore = null,
        string? logPath = null,
        string? reportsDirectory = null,
        bool includeStoreApps = true)
    {
        var effectiveLogPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWin",
            "Schedules",
            "last-run.log");
        Directory.CreateDirectory(Path.GetDirectoryName(effectiveLogPath)!);
        var effectiveProfiles = profileStore ?? new ProfileStore();
        var effectiveSecrets = secretStore ?? new LocalSecretStore();
        try
        {
            var profiles = await effectiveProfiles.LoadAsync(cancellationToken);
            var profile = profiles.FirstOrDefault(item => item.Id == profileId)
                          ?? throw new InvalidDataException("Profilo pianificato non trovato.");
            if (!profile.ScheduleEnabled)
            {
                throw new InvalidOperationException("La pianificazione è disattivata nel profilo.");
            }

            var password = await effectiveSecrets.LoadAsync(profileId, cancellationToken);
            var inventory = await new InventoryService().ScanAsync(
                includeStoreApps,
                cancellationToken);
            var applicationsById = inventory.ToDictionary(
                application => application.InstallationId,
                StringComparer.Ordinal);
            var selected = new List<InstalledApplication>();
            var missing = new List<string>();
            foreach (var saved in profile.Applications)
            {
                if (!applicationsById.TryGetValue(saved.InstallationId, out var application))
                {
                    missing.Add(saved.DisplayName);
                    continue;
                }

                application.IsSelected = true;
                application.BackupInstaller = saved.BackupInstaller;
                application.BackupSettings = saved.BackupSettings;
                application.BackupData = saved.BackupData;
                application.BackupInstalledFiles = saved.BackupInstalledFiles;
                application.BackupSensitiveSettings = saved.BackupSensitiveSettings;
                application.InstallerPath = saved.InstallerPath;
                application.BackupCache = false;
                application.ExcludedDataRoots.Clear();
                foreach (var root in saved.ExcludedDataRoots)
                {
                    application.ExcludedDataRoots.Add(root);
                }

                selected.Add(application);
            }

            if (selected.Count == 0)
            {
                throw new InvalidOperationException(
                    "Nessuna applicazione del profilo è presente su questo PC.");
            }

            var backupService = new BackupService();
            var (manifest, _) = await backupService.CreateBackupAsync(
                profile.Destination,
                password,
                selected,
                profile.Scope,
                profile.KeepLastSnapshots,
                profile.Name,
                cancellationToken);
            if (missing.Count > 0)
            {
                manifest.Warnings.Add($"Applicazioni del profilo non trovate: {string.Join(", ", missing)}.");
            }

            var reportPath = await ReportService.WriteBackupReportAsync(
                manifest,
                profile.Destination,
                recoveryKeyCreated: false,
                cancellationToken,
                reportsDirectory);
            var message =
                $"Backup pianificato completato: {manifest.FileCount:N0} file da {manifest.Applications.Count} applicazioni.";
            await WriteLogAsync(effectiveLogPath, message, CancellationToken.None);
            await RecordHistoryAsync(historyStore, message, "success");
            return new ScheduledRunResult(0, message, reportPath);
        }
        catch (Exception exception)
        {
            var message = "Backup pianificato non riuscito: " + exception.Message;
            await WriteLogAsync(effectiveLogPath, message, CancellationToken.None);
            await RecordHistoryAsync(historyStore, message, "error");
            return new ScheduledRunResult(1, message, null);
        }
    }

    private static async Task WriteLogAsync(string path, string message, CancellationToken cancellationToken)
    {
        await File.AppendAllTextAsync(
            path,
            $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}",
            cancellationToken);
    }

    private static async Task RecordHistoryAsync(HistoryStore? historyStore, string message, string kind)
    {
        try
        {
            var history = historyStore ?? new HistoryStore();
            await history.InitializeAsync();
            await history.RecordAsync(DateTimeOffset.Now, "Esecuzione pianificata", message, kind);
        }
        catch
        {
            // The external scheduler log already contains the failure.
        }
    }
}
