using System.Text;
using System.Text.Json;
using MigraWin.Models;

namespace MigraWin.Services;

public static class ReportService
{
    public static string ReportsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "MigraWin",
        "Reports");

    public static string LastReportPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MigraWin",
        "LastReport.md");

    public static async Task<string> WriteBackupReportAsync(
        SnapshotManifest manifest,
        string repositoryRoot,
        bool recoveryKeyCreated,
        CancellationToken cancellationToken,
        string? reportsDirectory = null)
    {
        var directory = reportsDirectory ?? ReportsDirectory;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"Backup-{manifest.CreatedAt:yyyyMMdd-HHmmss}-{manifest.SnapshotId[..8]}.md");
        var text = new StringBuilder()
            .AppendLine("# Rapporto di backup MigraWin")
            .AppendLine()
            .AppendLine($"- Backup: `{manifest.SnapshotId}`")
            .AppendLine($"- Profilo: {manifest.ProfileName}")
            .AppendLine($"- Data: {manifest.CreatedAt:dd/MM/yyyy HH:mm:ss}")
            .AppendLine($"- Macchina: {manifest.MachineName}")
            .AppendLine($"- Modalità: {(manifest.Scope == BackupScope.Incremental ? "Incrementale" : "Completa logica")}")
            .AppendLine($"- Applicazioni: {manifest.Applications.Count}")
            .AppendLine($"- File: {manifest.FileCount:N0}")
            .AppendLine($"- Dimensione logica: {InstalledApplication.FormatBytes(manifest.LogicalBytes)}")
            .AppendLine($"- Repository: `{Sanitize(repositoryRoot)}`")
            .AppendLine($"- Chiave di recupero generata: {(recoveryKeyCreated ? "sì, mostrata durante la creazione" : "no, repository già esistente")}")
            .AppendLine()
            .AppendLine("## Applicazioni")
            .AppendLine();
        foreach (var application in manifest.Applications)
        {
            text.AppendLine($"### {application.DisplayName}")
                .AppendLine()
                .AppendLine($"- Versione: {application.Version}")
                .AppendLine($"- Supporto dichiarato: {application.SupportLevel}");
            foreach (var component in application.Components)
            {
                text.AppendLine($"- {component.Label}: {component.Files.Count} file"
                                + (component.Sensitive ? " (sensibile)" : string.Empty));
            }

            text.AppendLine();
        }

        if (manifest.Warnings.Count > 0)
        {
            text.AppendLine("## Eccezioni").AppendLine();
            foreach (var warning in manifest.Warnings)
            {
                text.AppendLine($"- {Sanitize(warning)}");
            }
        }

        text.AppendLine()
            .AppendLine("## Verifica")
            .AppendLine()
            .AppendLine("Il manifest cifrato può essere ricaricato dal repository; la verifica degli oggetti controlla autenticazione, dimensione e identità del contenuto.");
        await WriteBothAsync(path, text.ToString(), manifest, cancellationToken, reportsDirectory is null);
        return path;
    }

    public static async Task<string> WriteRestoreReportAsync(
        SnapshotManifest manifest,
        RestoreResult result,
        CancellationToken cancellationToken,
        string? reportsDirectory = null)
    {
        var directory = reportsDirectory ?? ReportsDirectory;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"Ripristino-{DateTime.Now:yyyyMMdd-HHmmss}-{manifest.SnapshotId[..8]}.md");
        var text = new StringBuilder()
            .AppendLine("# Rapporto di ripristino MigraWin")
            .AppendLine()
            .AppendLine($"- Backup: `{manifest.SnapshotId}`")
            .AppendLine($"- Profilo: {manifest.ProfileName}")
            .AppendLine($"- File ripristinati: {result.FilesRestored:N0}")
            .AppendLine($"- File mantenuti: {result.FilesSkipped:N0}")
            .AppendLine($"- File sostituiti con copia preventiva: {result.FilesReplaced:N0}")
            .AppendLine($"- Installer avviati: {result.InstallerLaunches:N0}")
            .AppendLine($"- Copia di rollback: `{Sanitize(result.RollbackDirectory)}`")
            .AppendLine()
            .AppendLine("## Esito")
            .AppendLine();
        if (result.Errors.Count == 0)
        {
            text.AppendLine("Tutti gli elementi selezionati sono stati elaborati senza errori.");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                text.AppendLine($"- ERRORE: {Sanitize(error)}");
            }
        }

        if (result.Warnings.Count > 0)
        {
            text.AppendLine().AppendLine("## Attività residue").AppendLine();
            foreach (var warning in result.Warnings)
            {
                text.AppendLine($"- {Sanitize(warning)}");
            }
        }

        await WriteBothAsync(path, text.ToString(), result, cancellationToken, reportsDirectory is null);
        return path;
    }

    public static async Task<string> ReadLastReportAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(LastReportPath))
        {
            return "Nessun rapporto disponibile. Completa un backup o un ripristino.";
        }

        return await File.ReadAllTextAsync(LastReportPath, cancellationToken);
    }

    private static async Task WriteBothAsync(
        string markdownPath,
        string markdown,
        object jsonObject,
        CancellationToken cancellationToken,
        bool updateLastReport = true)
    {
        if (updateLastReport)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LastReportPath)!);
            await File.WriteAllTextAsync(LastReportPath, markdown, Encoding.UTF8, cancellationToken);
        }

        await File.WriteAllTextAsync(markdownPath, markdown, Encoding.UTF8, cancellationToken);
        await File.WriteAllTextAsync(
            Path.ChangeExtension(markdownPath, ".json"),
            JsonSerializer.Serialize(jsonObject, JsonDefaults.Options),
            Encoding.UTF8,
            cancellationToken);
    }

    private static string Sanitize(string value)
    {
        var result = value;
        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                 }.Where(path => !string.IsNullOrWhiteSpace(path))
                  .OrderByDescending(path => path.Length))
        {
            result = result.Replace(folder, "${folder}", StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
