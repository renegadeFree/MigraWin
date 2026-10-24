using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class InventoryService
{
    private static readonly string[] GameMarkers =
    [
        "steam", "epic games", "battle.net", "riot client", "riot games", "ubisoft connect",
        "ea app", "xbox", "minecraft launcher", "roblox", "gog galaxy", "itch.io"
    ];

    public event Action<string>? StatusChanged;

    public async Task<IReadOnlyList<InstalledApplication>> ScanAsync(
        bool includeStoreApps,
        CancellationToken cancellationToken = default)
    {
        StatusChanged?.Invoke("Lettura del registro delle applicazioni…");
        var apps = await Task.Run(() => ScanRegistry(cancellationToken), cancellationToken);
        if (includeStoreApps)
        {
            StatusChanged?.Invoke("Lettura delle applicazioni Microsoft Store…");
            apps.AddRange(await ScanStoreAppsAsync(cancellationToken));
        }

        foreach (var app in apps)
        {
            AdapterCatalog.Apply(app);
        }

        StatusChanged?.Invoke("Rimozione dei duplicati e classificazione…");
        return apps
            .Where(app => !string.IsNullOrWhiteSpace(app.DisplayName))
            .GroupBy(app => app.InstallationId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(app => app.IsGame)
            .ThenBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static List<InstalledApplication> ScanRegistry(CancellationToken cancellationToken)
    {
        var results = new List<InstalledApplication>();
        var locations = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, "Macchina · 64 bit"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, "Macchina · 32 bit"),
            (RegistryHive.CurrentUser, RegistryView.Registry64, "Utente · 64 bit"),
            (RegistryHive.CurrentUser, RegistryView.Registry32, "Utente · 32 bit")
        };

        foreach (var (hive, view, label) in locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null)
            {
                continue;
            }

            foreach (var subKeyName in uninstall.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var key = uninstall.OpenSubKey(subKeyName);
                if (key is null || Convert.ToInt32(key.GetValue("SystemComponent", 0)) == 1)
                {
                    continue;
                }

                var displayName = key.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName)
                    || displayName.StartsWith("KB", StringComparison.OrdinalIgnoreCase)
                    || displayName.Contains("Update for", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var publisher = key.GetValue("Publisher") as string ?? "Origine sconosciuta";
                var installLocation = key.GetValue("InstallLocation") as string ?? string.Empty;
                var productUrl = FirstWebUrl(
                    key.GetValue("URLInfoAbout") as string,
                    key.GetValue("HelpLink") as string,
                    key.GetValue("URLUpdateInfo") as string);
                var identity = $"{hive}\\{view}\\{subKeyName}";
                var estimatedSizeKb = Convert.ToInt64(key.GetValue("EstimatedSize", 0L));
                var game = IsLikelyGame(displayName, publisher, installLocation);
                var entry = new StringBuilder($"{hive}|{view}|{subKeyName}").ToString();
                results.Add(new InstalledApplication
                {
                    InstallationId = Hash(entry)[..24],
                    ProductId = Hash($"{publisher}|{displayName}".ToUpperInvariant())[..24],
                    DisplayName = displayName.Trim(),
                    Publisher = publisher.Trim(),
                    Version = (key.GetValue("DisplayVersion") as string ?? "Non disponibile").Trim(),
                    Architecture = view == RegistryView.Registry32 ? "x86" : "x64",
                    Source = "Registro Windows",
                    SourceDetail = label,
                    InstallLocation = Environment.ExpandEnvironmentVariables(installLocation),
                    DisplayIcon = key.GetValue("DisplayIcon") as string ?? string.Empty,
                    ProductUrl = productUrl,
                    UninstallString = key.GetValue("UninstallString") as string ?? string.Empty,
                    EstimatedSize = Math.Max(0, estimatedSizeKb) * 1024,
                    IsGame = game,
                    SupportLevel = game ? SupportLevel.Detected : SupportLevel.Detected
                });
            }
        }

        return results;
    }

    private async Task<List<InstalledApplication>> ScanStoreAppsAsync(CancellationToken cancellationToken)
    {
        var script = """
            Get-AppxPackage | ForEach-Object {
              [pscustomobject]@{
                Name = $_.Name
                PublisherId = $_.PublisherId
                Version = $_.Version.ToString()
                Architecture = switch ([int]$_.Architecture) {
                  0 { 'x86' }
                  5 { 'ARM' }
                  9 { 'x64' }
                  11 { 'Neutral' }
                  12 { 'ARM64' }
                  default { $_.Architecture.ToString() }
                }
                InstallLocation = $_.InstallLocation
                PackageFamilyName = $_.PackageFamilyName
                IsFramework = $_.IsFramework
              }
            } | ConvertTo-Json -Compress
            """;
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return [];
        }

        var json = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        var elements = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray()
            : new[] { document.RootElement }.AsEnumerable();
        var results = new List<InstalledApplication>();
        foreach (var element in elements)
        {
            if (element.TryGetProperty("IsFramework", out var framework) && framework.GetBoolean())
            {
                continue;
            }

            var name = element.GetProperty("Name").GetString() ?? "Pacchetto Store";
            var publisher = element.TryGetProperty("PublisherId", out var published) ? published.GetString() ?? "Store" : "Store";
            var location = element.TryGetProperty("InstallLocation", out var install) ? install.GetString() ?? string.Empty : string.Empty;
            var familyName = element.TryGetProperty("PackageFamilyName", out var family) ? family.GetString() ?? string.Empty : string.Empty;
            var displayName = FriendlyPackageName(name);
            results.Add(new InstalledApplication
            {
                InstallationId = Hash($"store|{publisher}|{name}|{Environment.UserName}".ToUpperInvariant())[..24],
                ProductId = Hash($"store|{publisher}|{name}".ToUpperInvariant())[..24],
                DisplayName = displayName,
                Publisher = publisher,
                Version = element.GetProperty("Version").GetString() ?? "Non disponibile",
                Architecture = element.GetProperty("Architecture").ToString(),
                Source = "Microsoft Store",
                SourceDetail = "Pacchetto MSIX installato per utente",
                InstallLocation = location,
                PackageFamilyName = familyName,
                ProductUrl = string.IsNullOrWhiteSpace(familyName)
                    ? string.Empty
                    : $"ms-windows-store://pdp/?PFN={Uri.EscapeDataString(familyName)}",
                IsGame = IsLikelyGame(displayName, publisher, location),
                SupportLevel = SupportLevel.OnlineReinstall
            });
        }

        return results;
    }

    public static bool IsLikelyGame(string name, string publisher, string installLocation)
    {
        var text = $"{name} {publisher} {installLocation}".ToLowerInvariant();
        return GameMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal));
    }

    private static string FriendlyPackageName(string packageName)
    {
        var value = packageName.Trim();
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Microsoft.WindowsTerminal"] = "Windows Terminal",
            ["Microsoft.WindowsNotepad"] = "Blocco note",
            ["Microsoft.Paint"] = "Paint",
            ["Microsoft.ScreenSketch"] = "Strumento di cattura",
            ["Microsoft.Photos"] = "Foto",
            ["Microsoft.WindowsCalculator"] = "Calcolatrice",
            ["Microsoft.WindowsStore"] = "Microsoft Store"
        };
        return known.TryGetValue(value, out var friendly) ? friendly : value.Replace('.', ' ');
    }

    private static string FirstWebUrl(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate =>
            Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https") ?? string.Empty;

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
