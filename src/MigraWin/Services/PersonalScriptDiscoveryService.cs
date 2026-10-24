using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class PersonalScriptDiscoveryService
{
    private static readonly string[] ScriptExtensions =
        [".ps1", ".cmd", ".bat", ".vbs", ".py", ".js", ".mjs", ".cjs"];

    private static readonly Dictionary<string, string> FriendlyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["check_shadows.ps1"] = "Controlla ombre desktop",
            ["copy_from_shadow.ps1"] = "Copia dagli shadow",
            ["delete_openclaw_task.ps1"] = "Rimuovi attività OpenClaw",
            ["extract_start.ps1"] = "Estrai menu Start",
            ["pin_automation.ps1"] = "Automazione pin menu Start",
            ["pin_layout_method.ps1"] = "Layout pin menu Start",
            ["pin_to_start.ps1"] = "Pin al menu Start",
            ["pin_to_start_auto.ps1"] = "Pin automatico menu Start",
            ["pin_vbs.vbs"] = "Pin menu Start VBScript",
            ["RenderVideo.ps1"] = "Render video",
            ["restore_start_menu.ps1"] = "Ripristina menu Start",
            ["startmenu_backup.ps1"] = "Backup menu Start",
            ["startmenu_startup.bat"] = "Avvio backup menu Start",
            ["traduci-selezione.vbs"] = "Traduci selezione"
        };

    private readonly string _userProfile;
    private readonly string _appData;
    private readonly string _programFiles;

    public PersonalScriptDiscoveryService(
        string? userProfile = null,
        string? appData = null,
        string? programFiles = null)
    {
        _userProfile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _appData = appData ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _programFiles = programFiles ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    }

    public IReadOnlyList<InstalledApplication> Scan(CancellationToken cancellationToken = default)
    {
        var applications = new List<InstalledApplication>();
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddEnterForRename(applications, claimed);
        AddNpmTool(
            applications,
            claimed,
            "pm2",
            "PM2",
            "Process manager Node.js",
            "https://pm2.keymetrics.io/",
            Path.Combine(_appData, "npm", "node_modules", "pm2"));
        AddNpmTool(
            applications,
            claimed,
            "n8n",
            "n8n",
            "Automazione workflow",
            "https://n8n.io/",
            Path.Combine(_appData, "npm", "node_modules", "n8n"),
            $"{(IsN8nManagedByPm2() ? "Avvio PM2 rilevato" : "Avvio PM2 non rilevato")}. " +
            "Arresta n8n prima del backup per copiare il database in modo coerente.",
            copyInstalledFiles: true);
        AddNpmTool(
            applications,
            claimed,
            "pi-agent",
            "Pi Agent",
            "Agente di programmazione",
            "https://github.com/earendil-works/pi",
            Path.Combine(_appData, "npm", "node_modules", "@earendil-works", "pi-coding-agent"));
        AddCodexProxy(applications, claimed);
        AddRootScripts(applications, claimed, cancellationToken);

        return applications
            .OrderBy(application => application.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private void AddEnterForRename(List<InstalledApplication> applications, HashSet<string> claimed)
    {
        var scriptRoot = Path.Combine(_programFiles, "Nilesoft Shell", "imports", "scripts");
        var script = Path.Combine(scriptRoot, "NewFolderWithSelection.ps1");
        var menu = Path.Combine(_programFiles, "Nilesoft Shell", "imports", "file-manage.nss");
        var userShell = Path.Combine(_userProfile, "shell.nss");
        if (!File.Exists(script) && !File.Exists(menu) && !File.Exists(userShell))
        {
            return;
        }

        var app = CreateApplication(
            "personal-enter-rename",
            "Enter for rename",
            "Script personale · Nilesoft Shell",
            "https://nilesoft.org/",
            string.Empty,
            "Integrazione Esplora file per creare una cartella dalla selezione e avviarne la rinomina.",
            File.GetLastWriteTime(File.Exists(script) ? script : menu).ToString("dd/MM/yyyy"));
        applications.Add(app);
        ClaimFiles(claimed, script, menu, userShell);
    }

    private void AddNpmTool(
        List<InstalledApplication> applications,
        HashSet<string> claimed,
        string id,
        string displayName,
        string description,
        string productUrl,
        string packageDirectory,
        string? note = null,
        bool copyInstalledFiles = false)
    {
        var wrapper = Path.Combine(_appData, "npm", id == "pi-agent" ? "pi.cmd" : id + ".cmd");
        if (!Directory.Exists(packageDirectory) && !File.Exists(wrapper))
        {
            return;
        }

        applications.Add(CreateApplication(
            "personal-" + id,
            displayName,
            "Script personale · npm",
            productUrl,
            Directory.Exists(packageDirectory) ? packageDirectory : string.Empty,
            description + " con configurazione locale." + (note is null ? string.Empty : " " + note),
            ReadPackageVersion(packageDirectory),
            estimatedSize: DirectorySize(packageDirectory),
            backupInstalledFiles: copyInstalledFiles));
        ClaimFiles(
            claimed,
            Path.Combine(_userProfile, "n8n-config.js"),
            Path.Combine(_userProfile, "avvia_n8n.vbs"),
            Path.Combine(_userProfile, "startmenu_backup_pm2.js"));
    }

    private void AddCodexProxy(List<InstalledApplication> applications, HashSet<string> claimed)
    {
        var proxyDirectory = Path.Combine(_userProfile, ".codex", "proxy");
        var proxy = Path.Combine(proxyDirectory, "codex-proxy.py");
        if (!File.Exists(proxy))
        {
            return;
        }

        applications.Add(CreateApplication(
            "personal-codex-deepseek-proxy",
            "Proxy DeepSeek per Codex",
            "Script personale · Python",
            string.Empty,
            string.Empty,
            "Proxy locale e cataloghi per usare DeepSeek e altri provider in Codex.",
            File.GetLastWriteTime(proxy).ToString("dd/MM/yyyy")));
        ClaimFiles(claimed, proxy);
    }

    private void AddRootScripts(
        List<InstalledApplication> applications,
        HashSet<string> claimed,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_userProfile))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_userProfile, "*", SearchOption.TopDirectoryOnly)
                     .Where(path => ScriptExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (claimed.Contains(path) || IsKnownConfigurationScript(path))
            {
                continue;
            }

            var app = CreateApplication(
                "personal-script|" + path,
                FriendlyNames.GetValueOrDefault(Path.GetFileName(path), Prettify(Path.GetFileNameWithoutExtension(path))),
                "Script personale",
                string.Empty,
                string.Empty,
                path,
                version: "Locale",
                architecture: ScriptArchitecture(Path.GetExtension(path)),
                estimatedSize: new FileInfo(path).Length);
            app.CustomDataRoots.Add(path);
            applications.Add(app);
        }
    }

    private static InstalledApplication CreateApplication(
        string identity,
        string displayName,
        string publisher,
        string productUrl,
        string installLocation,
        string sourceDetail,
        string version = "Locale",
        string architecture = "Script",
        long estimatedSize = 0,
        bool backupInstalledFiles = false)
    {
        return new InstalledApplication
        {
            InstallationId = Hash(identity)[..24],
            ProductId = Hash(displayName + "|" + identity)[..24],
            DisplayName = displayName,
            Publisher = publisher,
            Version = version,
            Architecture = architecture,
            Source = "Script personale",
            SourceDetail = sourceDetail,
            InstallLocation = installLocation,
            ProductUrl = productUrl,
            SupportLevel = SupportLevel.Archivable,
            BackupData = true,
            BackupSettings = true,
            BackupInstalledFiles = backupInstalledFiles,
            EstimatedSize = estimatedSize
        };
    }

    private bool IsN8nManagedByPm2()
    {
        var dump = Path.Combine(_userProfile, ".pm2", "dump.pm2");
        try
        {
            return File.Exists(dump)
                   && System.Text.RegularExpressions.Regex.IsMatch(
                       File.ReadAllText(dump),
                       @"""name""\s*:\s*""n8n""",
                       System.Text.RegularExpressions.RegexOptions.IgnoreCase
                       | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }
        catch
        {
            return false;
        }
    }

    private static long DirectorySize(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        try
        {
            return new DirectoryInfo(directory)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }

    private static string ReadPackageVersion(string packageDirectory)
    {
        var packageFile = Path.Combine(packageDirectory, "package.json");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(packageFile));
            return document.RootElement.TryGetProperty("version", out var version)
                ? version.GetString() ?? "Locale"
                : "Locale";
        }
        catch
        {
            return "Locale";
        }
    }

    private static string ScriptArchitecture(string extension) => extension.ToLowerInvariant() switch
    {
        ".ps1" => "PowerShell",
        ".py" => "Python",
        ".js" or ".mjs" or ".cjs" => "Node.js",
        ".vbs" => "VBScript",
        ".bat" or ".cmd" => "Batch",
        _ => "Script"
    };

    private static string Prettify(string value)
    {
        var text = string.Join(' ', value
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Trim();
        return text.Length == 0 ? "Script personale" : char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static bool IsKnownConfigurationScript(string path) =>
        Path.GetFileName(path) is "n8n-config.js" or "avvia_n8n.vbs" or "startmenu_backup_pm2.js";

    private static void ClaimFiles(HashSet<string> claimed, params string[] files)
    {
        foreach (var file in files)
        {
            claimed.Add(Path.GetFullPath(file));
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
