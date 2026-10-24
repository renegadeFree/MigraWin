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
    IReadOnlyList<SourceSpec> Data);

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
        new("git", "Git", "^Git$|Git for Windows",
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
            [])
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
        if (app.SupportLevel < SupportLevel.OnlineReinstall)
        {
            app.SupportLevel = SupportLevel.OnlineReinstall;
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
