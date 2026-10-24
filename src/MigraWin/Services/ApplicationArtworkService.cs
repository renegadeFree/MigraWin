using System.Collections.Concurrent;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class ApplicationArtworkService
{
    private const int MaxHtmlBytes = 1_048_576;
    private const int MaxImageBytes = 8_388_608;
    private const int MaxProductImages = 3;
    private readonly HttpClient _client;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<ImageSource>>>> _officialLoads = new();

    public ApplicationArtworkService(HttpClient? client = null, string? cacheDirectory = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWin",
            "Artwork");
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
        {
            _client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("MigraWin", UpdateService.CurrentVersion.ToString()));
        }
    }

    public static ImageSource? LoadLocalIcon(InstalledApplication application)
    {
        var path = ResolveLocalIconPath(application);
        if (path is null)
        {
            return null;
        }

        try
        {
            using var icon = Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase)
                ? new Icon(path, 64, 64)
                : Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }

            var image = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ImageSource>> LoadOfficialProductImagesAsync(
        InstalledApplication application,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(application.ProductUrl, UriKind.Absolute, out var pageUri)
            || pageUri.Scheme is not ("http" or "https"))
        {
            return [];
        }

        var cacheKey = Hash($"product-v1|{application.ProductId}|{application.Version}|{pageUri.AbsoluteUri}");
        var load = _officialLoads.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<IReadOnlyList<ImageSource>>>(
                () => LoadOfficialProductImagesCoreAsync(pageUri, cacheKey)));
        return await load.Value.WaitAsync(cancellationToken);
    }

    public static IReadOnlyList<Uri> ParseProductImageCandidates(string html, Uri pageUri)
    {
        var candidates = new List<Uri>();
        foreach (Match tag in Regex.Matches(html, @"(?is)<meta\b[^>]*>"))
        {
            var attributes = Regex.Matches(tag.Value, @"(?is)([a-z][\w:-]*)\s*=\s*[""']([^""']+)[""']")
                .ToDictionary(
                    match => match.Groups[1].Value,
                    match => WebUtility.HtmlDecode(match.Groups[2].Value),
                    StringComparer.OrdinalIgnoreCase);
            attributes.TryGetValue("property", out var property);
            attributes.TryGetValue("name", out var name);
            attributes.TryGetValue("content", out var content);
            if (content is not null
                && (property is "og:image" or "og:image:url" or "twitter:image"
                    || name is "og:image" or "og:image:url" or "twitter:image"))
            {
                AddCandidate(pageUri, content, candidates);
            }
        }

        return candidates.Distinct().Take(MaxProductImages).ToArray();
    }

    public static string? ParseDisplayIconPath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var value = Environment.ExpandEnvironmentVariables(displayIcon.Trim());
        if (value.StartsWith('"'))
        {
            var closingQuote = value.IndexOf('"', 1);
            value = closingQuote > 1 ? value[1..closingQuote] : value.Trim('"');
        }
        else
        {
            value = Regex.Replace(value, @",\s*-?\d+\s*$", string.Empty).Trim();
        }

        return File.Exists(value) ? Path.GetFullPath(value) : null;
    }

    private async Task<IReadOnlyList<ImageSource>> LoadOfficialProductImagesCoreAsync(
        Uri pageUri,
        string cacheKey)
    {
        var cacheDirectory = Path.Combine(_cacheDirectory, cacheKey);
        try
        {
            if (Directory.Exists(cacheDirectory))
            {
                return Directory.EnumerateFiles(cacheDirectory, "*.img")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => DecodeImage(File.ReadAllBytes(path)))
                    .Where(image => image is not null)
                    .Cast<ImageSource>()
                    .ToArray();
            }

            var html = await DownloadTextAsync(pageUri);
            var images = new List<ImageSource>();
            foreach (var imageUri in ParseProductImageCandidates(html, pageUri))
            {
                var bytes = await DownloadImageAsync(imageUri);
                var image = bytes is null ? null : DecodeImage(bytes);
                if (image is null || bytes is null)
                {
                    continue;
                }

                Directory.CreateDirectory(cacheDirectory);
                await File.WriteAllBytesAsync(
                    Path.Combine(cacheDirectory, $"{images.Count:00}.img"),
                    bytes);
                images.Add(image);
            }

            return images;
        }
        catch
        {
            // Official product imagery is optional and never replaces the installed icon.
        }

        return [];
    }

    private async Task<string> DownloadTextAsync(Uri uri)
    {
        using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        var bytes = await ReadLimitedAsync(stream, MaxHtmlBytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private async Task<byte[]?> DownloadImageAsync(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode
            || response.Content.Headers.ContentLength > MaxImageBytes
            || response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == false)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await ReadLimitedAsync(stream, MaxImageBytes);
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, int limit)
    {
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            if (output.Length + read > limit)
            {
                throw new InvalidDataException("La risorsa remota supera il limite consentito.");
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static ImageSource? DecodeImage(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveLocalIconPath(InstalledApplication application)
    {
        var displayIcon = ParseDisplayIconPath(application.DisplayIcon);
        if (displayIcon is not null)
        {
            return displayIcon;
        }

        var manifestIcon = ResolveStoreManifestIcon(application.InstallLocation);
        if (manifestIcon is not null)
        {
            return manifestIcon;
        }

        return FindLikelyExecutable(application);
    }

    private static string? ResolveStoreManifestIcon(string installLocation)
    {
        if (string.IsNullOrWhiteSpace(installLocation))
        {
            return null;
        }

        var manifestPath = Path.Combine(installLocation, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(manifestPath);
            var attributes = document.Descendants()
                .SelectMany(element => element.Attributes())
                .Where(attribute => attribute.Name.LocalName is
                    "Square310x310Logo" or "Square150x150Logo" or "Square44x44Logo" or "Logo")
                .GroupBy(attribute => attribute.Name.LocalName)
                .ToDictionary(group => group.Key, group => group.First().Value)
                .OrderBy(pair => pair.Key switch
                {
                    "Square310x310Logo" => 0,
                    "Square150x150Logo" => 1,
                    "Square44x44Logo" => 2,
                    _ => 3
                });

            foreach (var (_, relativePath) in attributes)
            {
                var normalized = relativePath.Replace('/', '\\');
                var fullPath = Path.GetFullPath(Path.Combine(installLocation, normalized));
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }

                var directory = Path.GetDirectoryName(fullPath);
                var baseName = Path.GetFileNameWithoutExtension(fullPath);
                var extension = Path.GetExtension(fullPath);
                if (directory is null || !Directory.Exists(directory))
                {
                    continue;
                }

                var scaled = Directory.EnumerateFiles(directory, baseName + "*" + extension)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (scaled is not null)
                {
                    return scaled;
                }
            }
        }
        catch
        {
            // WindowsApps permissions or a malformed package manifest must not break inventory.
        }

        return null;
    }

    private static string? FindLikelyExecutable(InstalledApplication application)
    {
        if (string.IsNullOrWhiteSpace(application.InstallLocation)
            || !Directory.Exists(application.InstallLocation))
        {
            return null;
        }

        try
        {
            var name = application.DisplayName.ToLowerInvariant();
            return Directory.EnumerateFiles(application.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(path => !Regex.IsMatch(
                    Path.GetFileName(path),
                    @"(?i)(unins|uninstall|setup|update|service|crash|report)"))
                .OrderByDescending(path =>
                {
                    var executable = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    return name.Contains(executable, StringComparison.Ordinal)
                           || executable.Contains(name, StringComparison.Ordinal);
                })
                .ThenBy(path => path.Length)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static bool AddCandidate(
        Uri pageUri,
        string value,
        List<Uri> candidates)
    {
        if (!Uri.TryCreate(pageUri, value, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        candidates.Add(uri);
        return true;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
