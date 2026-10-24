using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class BackupService
{
    public event Action<BackupProgress>? ProgressChanged;

    public async Task<(SnapshotManifest Manifest, string? RecoveryKey)> CreateBackupAsync(
        string destination,
        string password,
        IReadOnlyList<InstalledApplication> applications,
        BackupScope scope,
        int keepLastSnapshots,
        string profileName,
        CancellationToken cancellationToken)
    {
        var selected = applications.Where(application => application.IsSelected && !application.IsGame).ToArray();
        if (selected.Length == 0)
        {
            throw new InvalidOperationException("Seleziona almeno un'applicazione.");
        }

        var formatExists = File.Exists(Path.Combine(destination, "format.json"));
        RepositorySession session;
        string? recoveryKey = null;
        if (formatExists)
        {
            session = await RepositorySession.OpenAsync(destination, password, cancellationToken);
        }
        else
        {
            (session, recoveryKey) = await RepositorySession.CreateAsync(destination, password, cancellationToken);
        }

        await using (session)
        {
            var previous = scope is BackupScope.Incremental or BackupScope.Differential
                ? await session.GetLatestSnapshotAsync(cancellationToken)
                : null;
            var manifest = new SnapshotManifest
            {
                SnapshotId = Guid.NewGuid().ToString("N"),
                RepositoryId = session.Format.RepositoryId,
                ParentSnapshotId = previous?.SnapshotId,
                MachineName = Environment.MachineName,
                CreatedBy = Environment.UserName,
                ProfileName = profileName,
                Scope = scope
            };

            var files = BuildFilePlan(
                selected,
                manifest.Warnings);
            var completed = 0;
            var objectIds = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            foreach (var application in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProgressChanged?.Invoke(new BackupProgress
                {
                    Phase = "Acquisizione",
                    Detail = application.DisplayName,
                    Completed = completed,
                    Total = files.Count
                });
                var snapshotApplication = CreateSnapshotApplication(application);
                manifest.Applications.Add(snapshotApplication);
                var applicationPlan = files.Where(file => file.Application == application).ToArray();
                foreach (var group in applicationPlan.GroupBy(file => new
                {
                    file.Kind,
                    file.Root,
                    file.Label,
                    file.Sensitive
                }))
                {
                    snapshotApplication.Components.Add(new SnapshotComponent
                    {
                        Kind = group.Key.Kind,
                        Label = group.Key.Label,
                        Root = group.Key.Root,
                        Sensitive = group.Key.Sensitive
                    });
                }

                foreach (var componentPlan in applicationPlan)
                {
                    if (componentPlan.Sensitive && !application.BackupSensitiveSettings)
                    {
                        manifest.Warnings.Add(
                            $"{application.DisplayName}: esclusa configurazione potenzialmente sensibile {componentPlan.RelativePath}. Abilita esplicitamente i segreti nel dettaglio dell'applicazione.");
                        completed++;
                        continue;
                    }

                    var snapshotComponent = snapshotApplication.Components.First(component =>
                        component.Kind == componentPlan.Kind
                        && component.Root == componentPlan.Root
                        && component.Label == componentPlan.Label);
                    try
                    {
                        var stored = await session.Objects.StoreFileAsync(
                            componentPlan.SourcePath,
                            new Progress<long>(bytes => ProgressChanged?.Invoke(new BackupProgress
                            {
                                Phase = "Acquisizione",
                                Detail = $"{application.DisplayName} · {Path.GetFileName(componentPlan.SourcePath)} · {InstalledApplication.FormatBytes(bytes)}",
                                Completed = completed,
                                Total = files.Count
                            })),
                            cancellationToken);
                        objectIds.TryAdd(stored.ObjectId, 0);
                        var info = new FileInfo(componentPlan.SourcePath);
                        snapshotComponent.Files.Add(new SnapshotFile
                        {
                            RelativePath = componentPlan.RelativePath,
                            ObjectId = stored.ObjectId,
                            Length = stored.Length,
                            LastWriteTime = info.LastWriteTimeUtc,
                            Attributes = info.Attributes
                        });
                        manifest.FileCount++;
                        manifest.LogicalBytes += stored.Length;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        manifest.Warnings.Add($"{application.DisplayName}: {componentPlan.RelativePath}: {exception.Message}");
                    }

                    completed++;
                }
            }

            manifest.Warnings.Add($"{objectIds.Count:N0} oggetti distinti sul disco per {manifest.FileCount:N0} file logici.");
            ProgressChanged?.Invoke(new BackupProgress
            {
                Phase = "Pubblicazione",
                Detail = "Scrittura del manifest cifrato e verifica…",
                Completed = files.Count,
                Total = files.Count
            });
            await session.SaveSnapshotAsync(manifest, cancellationToken);
            await session.VerifySnapshotAsync(manifest, cancellationToken);
            if (keepLastSnapshots > 0)
            {
                var removed = await session.PruneSnapshotsAsync(
                    keepLastSnapshots,
                    manifest.SnapshotId,
                    cancellationToken);
                if (removed > 0)
                {
                    manifest.Warnings.Add($"{removed} copie precedenti rimosse dalla politica di conservazione.");
                }
            }

            return (manifest, recoveryKey);
        }
    }

    private static List<FilePlan> BuildFilePlan(
        IReadOnlyList<InstalledApplication> applications,
        List<string> warnings)
    {
        var result = new List<FilePlan>();
        foreach (var application in applications)
        {
            if (application.BackupInstaller)
            {
                AddSingleFile(application, "package", "Pacchetto installabile", application.InstallerPath, result, warnings);
            }

            if (application.BackupSettings || application.BackupData)
            {
                foreach (var source in AdapterCatalog.GetSources(application))
                {
                    AddSource(application, source, result, warnings);
                }
            }

            if (application.BackupInstalledFiles)
            {
                AddDirectory(
                    application,
                    "installed-files",
                    "Copia archivistica dei file installati",
                    application.InstallLocation,
                    null,
                    false,
                    result,
                    warnings);
            }
        }

        return result;
    }

    private static SnapshotApplication CreateSnapshotApplication(InstalledApplication application)
    {
        return new SnapshotApplication
        {
            InstallationId = application.InstallationId,
            ProductId = application.ProductId,
            DisplayName = application.DisplayName,
            Publisher = application.Publisher,
            Version = application.Version,
            Architecture = application.Architecture,
            Source = application.Source,
            InstallLocation = application.InstallLocation,
            SupportLevel = application.SupportLevel
        };
    }

    private static void AddSource(
        InstalledApplication application,
        SourceSpec source,
        List<FilePlan> result,
        List<string> warnings)
    {
        var expanded = AdapterCatalog.Expand(source.Root);
        var normalized = AdapterCatalog.NormalizeRoot(source.Root);
        var kind = application.SettingsRoots.Contains(source.Root, StringComparer.OrdinalIgnoreCase)
            ? "settings"
            : "data";
        if (File.Exists(expanded))
        {
            AddSingleFile(application, kind, source.Sensitive ? "Impostazioni con possibili segreti" : "Impostazioni", expanded, result, warnings, normalized, source.Sensitive);
            return;
        }

        if (!Directory.Exists(expanded))
        {
            warnings.Add($"{application.DisplayName}: percorso non disponibile {normalized}");
            return;
        }

        AddDirectory(
            application,
            kind,
            source.Sensitive ? "Configurazione sensibile" : kind == "settings" ? "Impostazioni" : "Dati",
            expanded,
            source.Includes,
            source.Sensitive,
            result,
            warnings,
            normalized);
    }

    private static void AddDirectory(
        InstalledApplication application,
        string kind,
        string label,
        string directory,
        IReadOnlyList<string>? includes,
        bool sensitive,
        List<FilePlan> result,
        List<string> warnings,
        string? normalizedRoot = null)
    {
        if (!Directory.Exists(directory))
        {
            warnings.Add($"{application.DisplayName}: cartella non disponibile {directory}");
            return;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false
        };
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", options))
            {
                if (IsExcluded(application, file))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(directory, file);
                if (!AdapterCatalog.Matches(relative, includes))
                {
                    continue;
                }

                var sensitiveFile = sensitive || LooksPotentiallySensitive(file);
                result.Add(new FilePlan(
                    application,
                    kind,
                    label,
                    normalizedRoot ?? AdapterCatalog.NormalizeRoot(directory),
                    file,
                    relative,
                    sensitiveFile));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"{application.DisplayName}: impossibile leggere {directory}: {exception.Message}");
        }
    }

    private static void AddSingleFile(
        InstalledApplication application,
        string kind,
        string label,
        string path,
        List<FilePlan> result,
        List<string> warnings,
        string? normalizedRoot = null,
        bool sensitive = false)
    {
        if (!File.Exists(path))
        {
            warnings.Add($"{application.DisplayName}: file non disponibile {path}");
            return;
        }

        if (IsExcluded(application, path))
        {
            return;
        }

        var storedRoot = normalizedRoot ?? "${Packages}";
        if (storedRoot != "${Packages}")
        {
            var expandedParent = Path.GetDirectoryName(AdapterCatalog.Expand(storedRoot));
            storedRoot = string.IsNullOrWhiteSpace(expandedParent)
                ? AdapterCatalog.NormalizeRoot(Path.GetDirectoryName(path)!)
                : AdapterCatalog.NormalizeRoot(expandedParent);
        }

        var sensitiveFile = sensitive || LooksPotentiallySensitive(path);
        result.Add(new FilePlan(
            application,
            kind,
            label,
            storedRoot,
            path,
            Path.GetFileName(path),
            sensitiveFile));
    }

    private static bool IsExcluded(InstalledApplication application, string path)
    {
        var fullPath = Path.GetFullPath(path);
        return application.ExcludedDataRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(AdapterCatalog.Expand)
            .Select(Path.GetFullPath)
            .Any(root =>
                fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksPotentiallySensitive(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("credential", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("password", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith(".env", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(path);
        if (!new[]
            {
                ".json", ".xml", ".yaml", ".yml", ".ini", ".config", ".conf", ".toml",
                ".properties", ".txt", ".ps1", ".cmd", ".bat", ".env"
            }.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return false;
            }

            using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[64 * 1024];
            var count = reader.ReadBlock(buffer, 0, buffer.Length);
            if (count == 0 || buffer.AsSpan(0, count).Contains('\0'))
            {
                return false;
            }

            var sample = new string(buffer, 0, count);
            return Regex.IsMatch(
                       sample,
                       @"(?im)(password|passwd|secret|token|api[_-]?key|client[_-]?secret)[""']?\s*[:=]\s*[""']?[^\s""'<>]{3,}",
                       RegexOptions.CultureInvariant)
                   || Regex.IsMatch(
                       sample,
                       @"-----BEGIN (RSA |OPENSSH |EC )?PRIVATE KEY-----",
                       RegexOptions.CultureInvariant);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record FilePlan(
        InstalledApplication Application,
        string Kind,
        string Label,
        string Root,
        string SourcePath,
        string RelativePath,
        bool Sensitive);
}
