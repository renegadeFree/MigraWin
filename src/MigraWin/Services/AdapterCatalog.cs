using System.Text.RegularExpressions;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed record SourceSpec(string Root, IReadOnlyList<string>? Includes = null, bool Sensitive = false);

public sealed record ApplicationAdapter(
    string Id,
    string Name,
    string Match,
    string OfficialUrl,
    IReadOnlyList<SourceSpec> Settings,
    IReadOnlyList<SourceSpec> Data,
    SupportLevel Support = SupportLevel.OnlineReinstall);

public static partial class AdapterCatalog
{
    private static readonly ApplicationAdapter[] Adapters =
    [
        new("vscode", "Visual Studio Code", "Visual Studio Code|^Code$",
            "https://code.visualstudio.com/download",
            [new(@"%APPDATA%\Code\User", ["settings.json", "keybindings.json", "tasks.json", "snippets/**"])],
            [new(@"%USERPROFILE%\.vscode\extensions", ["extensions.json"])]),
        new("windows-terminal", "Windows Terminal", "Windows Terminal",
            "https://github.com/microsoft/terminal/releases",
            [new(@"%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState", ["settings.json"])],
            []),
        new("git", "Git", @"\bGit\b|Git for Windows",
            "https://git-scm.com/download/win",
            [new(@"%USERPROFILE%\.gitconfig"), new(@"%USERPROFILE%\.config\git")],
            []),
        new("powershell", "PowerShell", "PowerShell",
            "https://github.com/PowerShell/PowerShell/releases",
            [new(@"%USERPROFILE%\Documents\PowerShell", ["Microsoft.PowerShell_profile.ps1", "Profile.ps1", "Modules/**"])],
            []),
        new("notepad-plus-plus", "Notepad++", "Notepad\\+\\+",
            "https://notepad-plus-plus.org/downloads/",
            [new(@"%APPDATA%\Notepad++", ["config.xml", "shortcuts.xml", "plugins/**"])],
            []),
        new("obs", "OBS Studio", "OBS Studio",
            "https://obsproject.com/download",
            [new(@"%APPDATA%\obs-studio\basic\profiles", ["**"]),
             new(@"%APPDATA%\obs-studio\basic\scenes", ["**"])],
            []),
        new("paint-dotnet", "Paint.NET", "Paint\\.NET",
            "https://www.getpaint.net/download.html",
            [new(@"%USERPROFILE%\Documents\Paint.NET User Files", ["**"])],
            []),
        new("libreoffice", "LibreOffice", "LibreOffice",
            "https://www.libreoffice.org/download/download-libreoffice/",
            [new(@"%APPDATA%\LibreOffice\4\user", ["registrymodifications.xcu", "config/**"], true)],
            []),
        new("filezilla", "FileZilla", "FileZilla",
            "https://filezilla-project.org/download.php?type=client",
            [new(@"%APPDATA%\FileZilla", ["filezilla.xml", "recentservers.xml", "sitemanager.xml"], true)],
            []),
        new("sublime", "Sublime Text", "Sublime Text",
            "https://www.sublimetext.com/download",
            [new(@"%APPDATA%\Sublime Text\Packages\User", ["**"])],
            []),
        new("personal-enter-rename", "Enter for rename", @"^Enter for rename(?:\s|$)",
            "https://nilesoft.org/",
            [new(@"%ProgramFiles%\Nilesoft Shell\imports\file-manage.nss"),
             new(@"%ProgramFiles%\Nilesoft Shell\imports\scripts\NewFolderWithSelection.ps1"),
             new(@"%USERPROFILE%\shell.nss")],
            []),
        new("personal-pm2", "PM2", @"^PM2(?:\s|$)",
            "https://pm2.keymetrics.io/",
            [],
            [new(@"%APPDATA%\npm\pm2"),
             new(@"%APPDATA%\npm\pm2.cmd"),
             new(@"%APPDATA%\npm\pm2.ps1"),
             new(@"%USERPROFILE%\startmenu_backup_pm2.js"),
             new(@"%USERPROFILE%\.pm2",
                 ["dump.pm2", "dump.pm2.bak", "module_conf.json"], true)]),
        new("personal-n8n", "n8n", @"^n8n(?:\s|$)",
            "https://n8n.io/",
            [],
            [new(@"%APPDATA%\npm\n8n"),
             new(@"%APPDATA%\npm\n8n.cmd"),
             new(@"%APPDATA%\npm\n8n.ps1"),
             new(@"%USERPROFILE%\n8n-config.js"),
             new(@"%USERPROFILE%\avvia_n8n.vbs"),
             new(@"%USERPROFILE%\.n8n",
                 ["config", "database.sqlite", "database.sqlite-shm", "database.sqlite-wal",
                  "storage/**", "nodes/package.json", "nodes/package-lock.json"], true)]),
        new("personal-pi-agent", "Pi Agent", @"^Pi Agent(?:\s|$)",
            "https://github.com/earendil-works/pi",
            [],
            [new(@"%APPDATA%\npm\pi"),
             new(@"%APPDATA%\npm\pi.cmd"),
             new(@"%APPDATA%\npm\pi.ps1"),
             new(@"%USERPROFILE%\.pi\agent",
                 ["settings.json", "models.json", "models-store.json"], true)]),
        new("personal-codex-proxy", "Proxy DeepSeek per Codex", @"^Proxy DeepSeek per Codex(?:\s|$)",
            string.Empty,
            [],
            [new(@"%USERPROFILE%\.codex\proxy\codex-proxy.py"),
             new(@"%USERPROFILE%\.codex\proxy\lmstudio-sync.py"),
             new(@"%USERPROFILE%\.codex\proxy\base-catalog.json"),
             new(@"%USERPROFILE%\.codex\proxy\lmstudio-generated.json"),
             new(@"%USERPROFILE%\.codex\proxy\lmstudio-map.json"),
             new(@"%USERPROFILE%\.codex\deepseek.config.toml", null, true)],
            SupportLevel.Archivable),
    ];

    public static ApplicationAdapter? Find(string name, string publisher, string installLocation)
    {
        var haystack = $"{name} {publisher} {installLocation}";
        return Adapters.FirstOrDefault(adapter =>
            Regex.IsMatch(haystack, adapter.Match, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }

    public static void Apply(InstalledApplication app)
    {
        var adapter = Find(app.DisplayName, app.Publisher, app.InstallLocation);
        if (adapter is null)
        {
            return;
        }

        app.SettingsRoots.AddRange(adapter.Settings.Select(spec => spec.Root));
        app.DataRoots.AddRange(adapter.Data.Select(spec => spec.Root));
        if (string.IsNullOrWhiteSpace(app.ProductUrl))
        {
            app.ProductUrl = adapter.OfficialUrl;
        }

        if (app.SupportLevel < adapter.Support)
        {
            app.SupportLevel = adapter.Support;
        }
    }

    public static IReadOnlyList<SourceSpec> GetSources(InstalledApplication app)
    {
        var adapter = Find(app.DisplayName, app.Publisher, app.InstallLocation);
        var result = new List<SourceSpec>();
        if (app.BackupSettings && adapter is not null)
        {
            result.AddRange(adapter.Settings);
        }

        if (app.BackupData)
        {
            if (adapter is not null)
            {
                result.AddRange(adapter.Data);
            }

            result.AddRange(app.CustomDataRoots.Select(root => new SourceSpec(root)));
        }

        return result;
    }

    public static string NormalizeRoot(string path)
    {
        var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        foreach (var (folder, token) in KnownFolders())
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            var normalized = Path.GetFullPath(folder).TrimEnd('\\');
            if (full.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return token;
            }

            if (full.StartsWith(normalized + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return token + full[normalized.Length..];
            }
        }

        return full;
    }

    public static string Expand(string path)
    {
        var expanded = path;
        foreach (var (folder, token) in KnownFolders())
        {
            expanded = expanded.Replace(token, folder, StringComparison.OrdinalIgnoreCase);
        }

        return Environment.ExpandEnvironmentVariables(expanded);
    }

    public static bool Matches(string relativePath, IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return true;
        }

        var candidate = relativePath.Replace('/', '\\');
        return patterns.Any(pattern => GlobMatches(candidate, pattern.Replace('/', '\\')));
    }

    private static IEnumerable<(string Folder, string Token)> KnownFolders()
    {
        yield return (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "${LocalAppData}");
        yield return (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "${AppData}");
        yield return (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "${UserDocuments}");
        yield return (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "${UserProfile}");
    }

    private static bool GlobMatches(string path, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", @".*")
            .Replace(@"\*", @"[^\\]*")
            .Replace(@"\?", @"[^\\]") + "$";
        return Regex.IsMatch(path, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
