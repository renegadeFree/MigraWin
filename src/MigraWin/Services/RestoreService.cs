using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class RestoreOptions
{
    public HashSet<string> ApplicationIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ComponentKeys { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> RootMappings { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConflictPolicy ConflictPolicy { get; set; } = ConflictPolicy.KeepExisting;
    public bool RunInstallers { get; set; }

    public static string ComponentKey(string applicationId, SnapshotComponent component) =>
        $"{applicationId}|{component.Kind}|{component.Root}";
}

public sealed class RestoreService
{
    private readonly string? _reportDirectory;

    public RestoreService(string? reportDirectory = null)
    {
        _reportDirectory = reportDirectory;
    }

    public event Action<BackupProgress>? ProgressChanged;

    public async Task<RestoreResult> RestoreAsync(
        RepositorySession repository,
        SnapshotManifest manifest,
        RestoreOptions options,
        CancellationToken cancellationToken)
    {
        var runId = $"{manifest.SnapshotId}-{DateTime.Now:yyyyMMdd-HHmmss}";
        var result = new RestoreResult
        {
            RollbackDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MigraWin",
                "Rollback",
                runId)
        };
        var selectedApplications = manifest.Applications
            .Where(application => options.ApplicationIds.Contains(application.InstallationId))
            .ToArray();
        var components = selectedApplications
            .SelectMany(application => application.Components.Select(component => (Application: application, Component: component)))
            .Where(pair => options.ComponentKeys.Contains(RestoreOptions.ComponentKey(pair.Application.InstallationId, pair.Component)))
            .ToArray();
        var totalFiles = components.Sum(pair => pair.Component.Files.Count);
        var completed = 0;
        var installers = new List<(InstalledPath Installer, string ApplicationName)>();

        foreach (var (application, component) in components)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetRoot = ResolveTargetRoot(component.Root, manifest.SnapshotId, options.RootMappings);
            Directory.CreateDirectory(targetRoot);
            foreach (var emptyDirectory in component.EmptyDirectories)
            {
                var directory = SafeCombine(targetRoot, emptyDirectory);
                EnsureNoReparsePoints(targetRoot, directory);
                Directory.CreateDirectory(directory);
            }

            foreach (var file in component.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = SafeCombine(targetRoot, file.RelativePath);
                EnsureNoReparsePoints(targetRoot, destination);
                ProgressChanged?.Invoke(new BackupProgress
                {
                    Phase = "Ripristino",
                    Detail = $"{application.DisplayName} · {Path.GetFileName(destination)}",
                    Completed = completed,
                    Total = totalFiles
                });

                if (File.Exists(destination))
                {
                    switch (options.ConflictPolicy)
                    {
                        case ConflictPolicy.KeepExisting:
                            result.FilesSkipped++;
                            completed++;
                            continue;
                        case ConflictPolicy.KeepBoth:
                            destination = NextAvailableName(destination);
                            break;
                        case ConflictPolicy.ReplaceWithRollback:
                            BackupExistingFile(destination, runId, application, component, file);
                            result.FilesReplaced++;
                            break;
                    }
                }

                try
                {
                    await repository.Objects.RestoreFileAsync(
                        file.ObjectId,
                        destination,
                        new Progress<long>(bytes => ProgressChanged?.Invoke(new BackupProgress
                        {
                            Phase = "Ripristino",
                            Detail = $"{application.DisplayName} · {Path.GetFileName(destination)} · {InstalledApplication.FormatBytes(bytes)}",
                            Completed = completed,
                            Total = totalFiles
                        })),
                        cancellationToken);
                    File.SetLastWriteTimeUtc(destination, file.LastWriteTime.UtcDateTime);
                    ApplySafeAttributes(destination, file.Attributes);
                    result.FilesRestored++;
                    if (component.Kind == "package")
                    {
                        installers.Add((new InstalledPath(destination, component.Root), application.DisplayName));
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
                {
                    result.Errors.Add($"{application.DisplayName}: {file.RelativePath}: {exception.Message}");
                }

                completed++;
            }
        }

        if (options.RunInstallers)
        {
            foreach (var (installer, applicationName) in installers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    result.InstallerLaunches++;
                    await LaunchInstallerAsync(installer.Path, cancellationToken);
                }
                catch (Exception exception)
                {
                    result.Errors.Add($"Installer di {applicationName}: {exception.Message}");
                }
            }
        }
        else if (installers.Count > 0)
        {
            result.Warnings.Add($"{installers.Count} installer sono stati ripristinati ma non avviati.");
        }

        result.ReportPath = await ReportService.WriteRestoreReportAsync(
            manifest,
            result,
            cancellationToken,
            _reportDirectory);
        return result;
    }

    private static string ResolveTargetRoot(
        string storedRoot,
        string snapshotId,
        IReadOnlyDictionary<string, string> mappings)
    {
        if (mappings.TryGetValue(storedRoot, out var explicitTarget) && !string.IsNullOrWhiteSpace(explicitTarget))
        {
            return Path.GetFullPath(explicitTarget);
        }

        if (storedRoot == "${Packages}")
        {
            return Path.Combine(Path.GetTempPath(), "MigraWinPackages", snapshotId);
        }

        var expanded = AdapterCatalog.Expand(storedRoot);
        if (Path.IsPathFullyQualified(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        throw new InvalidDataException($"Percorso di origine non supportato: {storedRoot}");
    }

    private static string SafeCombine(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("Il backup contiene un percorso assoluto non valido.");
        }

        var fullRoot = Path.GetFullPath(root).TrimEnd('\\') + "\\";
        var destination = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Il backup tenta di scrivere fuori dalla cartella di destinazione.");
        }

        return destination;
    }

    private static void EnsureNoReparsePoints(string root, string destination)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd('\\');
        var parent = Path.GetDirectoryName(Path.GetFullPath(destination));
        while (!string.IsNullOrWhiteSpace(parent)
               && parent.Length > normalizedRoot.Length)
        {
            if (Directory.Exists(parent))
            {
                var attributes = File.GetAttributes(parent);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("La destinazione contiene un collegamento o un punto di analisi non consentito.");
                }
            }

            parent = Path.GetDirectoryName(parent);
        }
    }

    private void BackupExistingFile(
        string path,
        string runId,
        SnapshotApplication application,
        SnapshotComponent component,
        SnapshotFile file)
    {
        var safeApplication = SanitizeSegment(application.DisplayName);
        var safeComponent = SanitizeSegment(component.Kind);
        var rollbackRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWin",
            "Rollback",
            runId,
            safeApplication,
            safeComponent);
        var actualRollback = SafeCombine(rollbackRoot, file.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(actualRollback)!);
        File.Copy(path, actualRollback, true);
    }

    private static string NextAvailableName(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 1; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Impossibile trovare un nome libero per il file.");
    }

    private static void ApplySafeAttributes(string path, FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }
    }

    private static async Task LaunchInstallerAsync(string path, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo;
        if (Path.GetExtension(path).Equals(".msi", StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo("msiexec.exe")
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("/i");
            startInfo.ArgumentList.Add(path);
        }
        else
        {
            startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path)
            };
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Installer non avviato.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"L'installer è terminato con codice {process.ExitCode}.");
        }
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }

    private sealed record InstalledPath(string Path, string Root);
}
