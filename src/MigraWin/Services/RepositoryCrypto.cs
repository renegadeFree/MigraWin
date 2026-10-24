using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MigraWin.Services;

public sealed class RepositoryFormat
{
    public int SchemaVersion { get; set; } = 1;
    public string RepositoryId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public List<KeySlot> KeySlots { get; set; } = [];
}

public sealed class KeySlot
{
    public required string Kind { get; set; }
    public required string Salt { get; set; }
    public int Iterations { get; set; }
    public required string Nonce { get; set; }
    public required string WrappedKey { get; set; }
    public required string Tag { get; set; }
}

internal sealed class RepositoryKeys : IDisposable
{
    public required byte[] Master { get; init; }
    public required byte[] Content { get; init; }
    public required byte[] Deduplication { get; init; }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Master);
        CryptographicOperations.ZeroMemory(Content);
        CryptographicOperations.ZeroMemory(Deduplication);
    }
}

internal static class RepositoryCrypto
{
    private const int KdfIterations = 600_000;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static (RepositoryFormat Format, string RecoveryKey) Create(string password)
    {
        var master = RandomNumberGenerator.GetBytes(KeySize);
        var recoveryKey = Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        try
        {
            var format = new RepositoryFormat();
            format.KeySlots.Add(WrapMaster(master, password, "password", format.RepositoryId));
            format.KeySlots.Add(WrapMaster(master, recoveryKey, "recovery", format.RepositoryId));
            return (format, recoveryKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(master);
        }
    }

    public static RepositoryKeys Unlock(RepositoryFormat format, string secret)
    {
        if (format.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Versione repository {format.SchemaVersion} non supportata.");
        }

        foreach (var slot in format.KeySlots)
        {
            try
            {
                var master = UnwrapMaster(slot, secret, format.RepositoryId);
                var content = HKDF.DeriveKey(
                    HashAlgorithmName.SHA256,
                    master,
                    KeySize,
                    Encoding.UTF8.GetBytes(format.RepositoryId),
                    Encoding.UTF8.GetBytes("MigraWin/content/v1"));
                var deduplication = HKDF.DeriveKey(
                    HashAlgorithmName.SHA256,
                    master,
                    KeySize,
                    Encoding.UTF8.GetBytes(format.RepositoryId),
                    Encoding.UTF8.GetBytes("MigraWin/dedup/v1"));
                return new RepositoryKeys
                {
                    Master = master,
                    Content = content,
                    Deduplication = deduplication
                };
            }
            catch (CryptographicException)
            {
                // Try the other recovery path.
            }
            catch (FormatException)
            {
                // A malformed recovery key is equivalent to a wrong key.
            }
        }

        throw new UnauthorizedAccessException("Password o chiave di recupero non valida.");
    }

    public static async Task ChangePasswordAsync(
        RepositoryFormat format,
        string path,
        string currentSecret,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 10)
        {
            throw new ArgumentException("La nuova password deve contenere almeno 10 caratteri.", nameof(newPassword));
        }

        using var keys = Unlock(format, currentSecret);
        var passwordSlot = WrapMaster(keys.Master, newPassword, "password", format.RepositoryId);
        var existingIndex = format.KeySlots.FindIndex(slot => slot.Kind == "password");
        if (existingIndex >= 0)
        {
            format.KeySlots[existingIndex] = passwordSlot;
        }
        else
        {
            format.KeySlots.Add(passwordSlot);
        }

        await WriteFormatAsync(path, format, cancellationToken);
    }

    public static async Task WriteFormatAsync(string path, RepositoryFormat format, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(format, JsonDefaults.Options);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(temp, json, cancellationToken);
        File.Move(temp, path, true);
    }

    public static async Task<RepositoryFormat> ReadFormatAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var format = await JsonSerializer.DeserializeAsync<RepositoryFormat>(stream, JsonDefaults.Options, cancellationToken);
        return format ?? throw new InvalidDataException("Intestazione del repository non valida.");
    }

    public static async Task WriteEncryptedManifestAsync(
        string path,
        ReadOnlyMemory<byte> plaintext,
        RepositoryKeys keys,
        string repositoryId,
        string snapshotId,
        CancellationToken cancellationToken)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(keys.Content, TagSize))
        {
            aes.Encrypt(nonce, plaintext.Span, ciphertext, tag, ManifestAad(repositoryId, snapshotId));
        }

        await using var output = new FileStream(path + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await output.WriteAsync("MWM1"u8.ToArray(), cancellationToken);
        await output.WriteAsync(nonce, cancellationToken);
        await output.WriteAsync(BitConverter.GetBytes(plaintext.Length), cancellationToken);
        await output.WriteAsync(tag, cancellationToken);
        await output.WriteAsync(ciphertext, cancellationToken);
        await output.FlushAsync(cancellationToken);
        output.Flush(true);
        await output.DisposeAsync();
        File.Move(path + ".tmp", path, true);
    }

    public static async Task<byte[]> ReadEncryptedManifestAsync(
        string path,
        RepositoryKeys keys,
        string repositoryId,
        string snapshotId,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var magic = new byte[4];
        if (await input.ReadAsync(magic, cancellationToken) != magic.Length || !magic.AsSpan().SequenceEqual("MWM1"u8))
        {
            throw new InvalidDataException("Manifest cifrato non riconosciuto.");
        }

        var nonce = new byte[NonceSize];
        var lengthBytes = new byte[sizeof(int)];
        var tag = new byte[TagSize];
        await input.ReadExactlyAsync(nonce, cancellationToken);
        await input.ReadExactlyAsync(lengthBytes, cancellationToken);
        await input.ReadExactlyAsync(tag, cancellationToken);
        var length = BitConverter.ToInt32(lengthBytes);
        if (length < 0 || length > 256 * 1024 * 1024)
        {
            throw new InvalidDataException("Dimensione del manifest non valida.");
        }

        var ciphertext = new byte[length];
        var plaintext = new byte[length];
        await input.ReadExactlyAsync(ciphertext, cancellationToken);
        if (input.Position != input.Length)
        {
            throw new InvalidDataException("Il manifest contiene dati inattesi.");
        }

        using var aes = new AesGcm(keys.Content, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, ManifestAad(repositoryId, snapshotId));
        return plaintext;
    }

    private static KeySlot WrapMaster(byte[] master, string secret, string kind, string repositoryId)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            secret,
            salt,
            KdfIterations,
            HashAlgorithmName.SHA256,
            KeySize);
        var wrapped = new byte[KeySize];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, master, wrapped, tag, SlotAad(repositoryId, kind));
            return new KeySlot
            {
                Kind = kind,
                Salt = Convert.ToBase64String(salt),
                Iterations = KdfIterations,
                Nonce = Convert.ToBase64String(nonce),
                WrappedKey = Convert.ToBase64String(wrapped),
                Tag = Convert.ToBase64String(tag)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] UnwrapMaster(KeySlot slot, string secret, string repositoryId)
    {
        var salt = Convert.FromBase64String(slot.Salt);
        var nonce = Convert.FromBase64String(slot.Nonce);
        var wrapped = Convert.FromBase64String(slot.WrappedKey);
        var tag = Convert.FromBase64String(slot.Tag);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            secret,
            salt,
            slot.Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        var master = new byte[KeySize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, wrapped, tag, master, SlotAad(repositoryId, slot.Kind));
            return master;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(master);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] SlotAad(string repositoryId, string kind) =>
        Encoding.UTF8.GetBytes($"MigraWin/keyslot/1|{repositoryId}|{kind}");

    private static byte[] ManifestAad(string repositoryId, string snapshotId) =>
        Encoding.UTF8.GetBytes($"MigraWin/manifest/1|{repositoryId}|{snapshotId}");

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
