using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace MigraWin.Services;

public sealed record AvailableUpdate(
    Version Version,
    string TagName,
    string PageUrl,
    string Notes,
    string InstallerUrl,
    string InstallerName,
    string ChecksumsUrl);

public sealed class UpdateService
{
    public const string DefaultOwner = "renegadeFree";
    public const string DefaultRepository = "MigraWin";
    private readonly HttpClient _client;
    private readonly string _owner;
    private readonly string _repository;
    private readonly string _updateDirectory;

    public UpdateService(
        HttpClient? client = null,
        string owner = DefaultOwner,
        string repository = DefaultRepository,
        string? updateDirectory = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _owner = owner;
        _repository = repository;
        _updateDirectory = updateDirectory ?? DefaultUpdateDirectory();
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
        {
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MigraWin", CurrentVersion.ToString()));
        }

        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? new Version(version.Major, version.Minor, Math.Max(0, version.Build))
            : new Version(1, 0, 0);

    public async Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = $"https://api.github.com/repos/{_owner}/{_repository}/releases/latest";
        using var response = await _client.GetAsync(endpoint, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseRelease(document.RootElement);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        throw new HttpRequestException(
            $"GitHub ha risposto con {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    public async Task<string> DownloadInstallerAsync(
        AvailableUpdate update,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_updateDirectory);
        var targetPath = Path.Combine(_updateDirectory, update.InstallerName);
        var temporaryPath = targetPath + ".download";
        try
        {
            var checksums = await _client.GetStringAsync(update.ChecksumsUrl, cancellationToken);
            var expectedHash = ParseExpectedHash(checksums, update.InstallerName);
            using var response = await _client.GetAsync(
                update.InstallerUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > 512L * 1024 * 1024)
            {
                throw new InvalidDataException("Il pacchetto di aggiornamento supera il limite di sicurezza.");
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];
            long total = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash.AppendData(buffer, 0, read);
                total += read;
                if (contentLength is > 0)
                {
                    progress?.Report((double)total / contentLength.Value);
                }
            }

            await target.FlushAsync(cancellationToken);
            target.Flush(true);
            await target.DisposeAsync();
            var actualHash = Convert.ToHexString(hash.GetHashAndReset());
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actualHash),
                    Convert.FromHexString(expectedHash)))
            {
                throw new CryptographicException("L'hash dell'installer scaricato non corrisponde alla release.");
            }

            File.Move(temporaryPath, targetPath, true);
            return targetPath;
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    internal AvailableUpdate? ParseRelease(JsonElement release, Version? currentVersion = null)
    {
        var tag = release.GetProperty("tag_name").GetString() ?? string.Empty;
        if (!TryParseVersion(tag, out var version)
            || version <= (currentVersion ?? CurrentVersion)
            || release.TryGetProperty("draft", out var draft) && draft.GetBoolean()
            || release.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean())
        {
            return null;
        }

        var assets = release.GetProperty("assets").EnumerateArray()
            .Select(asset => new
            {
                Name = asset.GetProperty("name").GetString() ?? string.Empty,
                Url = asset.GetProperty("browser_download_url").GetString() ?? string.Empty
            })
            .ToArray();
        var installer = assets.FirstOrDefault(asset =>
            asset.Name.StartsWith("MigraWin-", StringComparison.OrdinalIgnoreCase)
            && asset.Name.EndsWith("-x64.msi", StringComparison.OrdinalIgnoreCase));
        var checksums = assets.FirstOrDefault(asset =>
            asset.Name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
        if (installer is null || checksums is null
            || !Uri.TryCreate(installer.Url, UriKind.Absolute, out var installerUri)
            || installerUri.Scheme != Uri.UriSchemeHttps
            || !Uri.TryCreate(checksums.Url, UriKind.Absolute, out var checksumUri)
            || checksumUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return new AvailableUpdate(
            version,
            tag,
            release.TryGetProperty("html_url", out var page) ? page.GetString() ?? string.Empty : string.Empty,
            release.TryGetProperty("body", out var notes) ? notes.GetString() ?? string.Empty : string.Empty,
            installer.Url,
            installer.Name,
            checksums.Url);
    }

    internal static string ParseExpectedHash(string checksums, string installerName)
    {
        foreach (var line in checksums.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2
                && parts[^1].TrimStart('*').Equals(installerName, StringComparison.OrdinalIgnoreCase)
                && parts[0].Length == 64)
            {
                return parts[0].ToUpperInvariant();
            }
        }

        throw new InvalidDataException($"Hash SHA-256 non trovato per {installerName}.");
    }

    internal static string DefaultUpdateDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = string.IsNullOrWhiteSpace(userProfile)
            ? string.Empty
            : Path.Combine(userProfile, "Downloads");
        return Path.Combine(
            Directory.Exists(downloads)
                ? downloads
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWinUpdates");
    }

    private static bool TryParseVersion(string tag, out Version version)
    {
        var normalized = tag.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        return Version.TryParse(normalized, out version!);
    }
}
