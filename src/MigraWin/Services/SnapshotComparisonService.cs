using System.Text;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class SnapshotChange
{
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required string Glyph { get; init; }
    public required string Tone { get; init; }
}

public sealed class SnapshotComparisonResult
{
    public required SnapshotSummary From { get; init; }
    public required SnapshotSummary To { get; init; }
    public List<SnapshotChange> Changes { get; init; } = [];
    public int ApplicationsAdded { get; set; }
    public int ApplicationsRemoved { get; set; }
    public int ApplicationsChanged { get; set; }
    public int FilesAdded { get; set; }
    public int FilesRemoved { get; set; }
    public int FilesChanged { get; set; }
    public string ChangeSummary =>
        $"{ApplicationsAdded + ApplicationsRemoved + ApplicationsChanged} variazioni applicative · "
        + $"{FilesAdded + FilesRemoved + FilesChanged:N0} variazioni nei file";
}

public sealed class SnapshotComparisonService
{
    public async Task<SnapshotComparisonResult> CompareAsync(
        RepositorySession repository,
        SnapshotSummary from,
        SnapshotSummary to,
        CancellationToken cancellationToken)
    {
        var fromManifest = await repository.LoadSnapshotAsync(from.SnapshotId, cancellationToken);
        var toManifest = await repository.LoadSnapshotAsync(to.SnapshotId, cancellationToken);
        var result = new SnapshotComparisonResult { From = from, To = to };
        var before = fromManifest.Applications
            .ToDictionary(application => application.InstallationId, StringComparer.Ordinal);
        var after = toManifest.Applications
            .ToDictionary(application => application.InstallationId, StringComparer.Ordinal);

        foreach (var application in after.Values.Where(application => !before.ContainsKey(application.InstallationId)))
        {
            result.ApplicationsAdded++;
            result.Changes.Add(Change(
                "application-added",
                application.DisplayName,
                $"Nuova applicazione · versione {application.Version}",
                "\uE710",
                "success"));
        }

        foreach (var application in before.Values.Where(application => !after.ContainsKey(application.InstallationId)))
        {
            result.ApplicationsRemoved++;
            result.Changes.Add(Change(
                "application-removed",
                application.DisplayName,
                $"Applicazione non più presente · versione {application.Version}",
                "\uE74D",
                "danger"));
        }

        foreach (var application in after.Values.Where(application => before.ContainsKey(application.InstallationId)))
        {
            var previous = before[application.InstallationId];
            if (!string.Equals(previous.Version, application.Version, StringComparison.OrdinalIgnoreCase)
                || previous.SupportLevel != application.SupportLevel
                || !string.Equals(previous.Architecture, application.Architecture, StringComparison.OrdinalIgnoreCase))
            {
                result.ApplicationsChanged++;
                result.Changes.Add(Change(
                    "application-changed",
                    application.DisplayName,
                    $"{previous.Version} ({previous.SupportLabel}) → {application.Version} ({application.SupportLabel})",
                    "\uE8AB",
                    "warning"));
            }

            CompareFiles(previous, application, result);
        }

        if (result.Changes.Count == 0)
        {
            result.Changes.Add(Change(
                "no-change",
                "Nessuna differenza rilevata",
                "Applicazioni, versioni, componenti e contenuti coincidono nel perimetro confrontato.",
                "\uE73E",
                "success"));
        }

        return result;
    }

    public static async Task<string> WriteReportAsync(
        SnapshotComparisonResult comparison,
        string? directory,
        CancellationToken cancellationToken)
    {
        var outputDirectory = directory ?? ReportService.ReportsDirectory;
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(
            outputDirectory,
            $"Confronto-{DateTime.Now:yyyyMMdd-HHmmss}-{comparison.From.SnapshotId[..6]}-{comparison.To.SnapshotId[..6]}.md");
        var text = new StringBuilder()
            .AppendLine("# Confronto snapshot MigraWin")
            .AppendLine()
            .AppendLine($"- Da: {comparison.From.ProfileName} · {comparison.From.DateLabel}")
            .AppendLine($"- A: {comparison.To.ProfileName} · {comparison.To.DateLabel}")
            .AppendLine($"- Applicazioni aggiunte: {comparison.ApplicationsAdded}")
            .AppendLine($"- Applicazioni rimosse: {comparison.ApplicationsRemoved}")
            .AppendLine($"- Applicazioni modificate: {comparison.ApplicationsChanged}")
            .AppendLine($"- File aggiunti: {comparison.FilesAdded:N0}")
            .AppendLine($"- File rimossi: {comparison.FilesRemoved:N0}")
            .AppendLine($"- File modificati: {comparison.FilesChanged:N0}")
            .AppendLine()
            .AppendLine("## Variazioni")
            .AppendLine();
        foreach (var change in comparison.Changes)
        {
            text.AppendLine($"- **{change.Title}** — {change.Detail}");
        }

        await File.WriteAllTextAsync(path, text.ToString(), Encoding.UTF8, cancellationToken);
        return path;
    }

    private static void CompareFiles(
        SnapshotApplication previous,
        SnapshotApplication current,
        SnapshotComparisonResult result)
    {
        var before = previous.Components
            .SelectMany(component => component.Files.Select(file => new
            {
                Key = $"{component.Kind}|{component.Root}|{file.RelativePath}",
                File = file
            }))
            .ToDictionary(item => item.Key, item => item.File, StringComparer.OrdinalIgnoreCase);
        var after = current.Components
            .SelectMany(component => component.Files.Select(file => new
            {
                Key = $"{component.Kind}|{component.Root}|{file.RelativePath}",
                File = file
            }))
            .ToDictionary(item => item.Key, item => item.File, StringComparer.OrdinalIgnoreCase);

        var added = after.Keys.Count(key => !before.ContainsKey(key));
        var removed = before.Keys.Count(key => !after.ContainsKey(key));
        var changed = after.Count(pair =>
            before.TryGetValue(pair.Key, out var old)
            && (!string.Equals(old.ObjectId, pair.Value.ObjectId, StringComparison.Ordinal)
                || old.Length != pair.Value.Length));
        result.FilesAdded += added;
        result.FilesRemoved += removed;
        result.FilesChanged += changed;
        if (added + removed + changed > 0)
        {
            result.Changes.Add(Change(
                "file-changes",
                current.DisplayName,
                $"{added} aggiunti · {removed} rimossi · {changed} modificati",
                "\uE8B7",
                "info"));
        }
    }

    private static SnapshotChange Change(
        string kind,
        string title,
        string detail,
        string glyph,
        string tone) =>
        new()
        {
            Kind = kind,
            Title = title,
            Detail = detail,
            Glyph = glyph,
            Tone = tone
        };
}
