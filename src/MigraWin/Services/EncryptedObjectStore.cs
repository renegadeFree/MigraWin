using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace MigraWin.Services;

public sealed record StoredObject(string ObjectId, long Length);

internal sealed class EncryptedObjectStore(string root, RepositoryKeys keys)
{
    private const int ChunkSize = 4 * 1024 * 1024;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly string _objectsRoot = Path.Combine(root, "objects");

    public string PathFor(string objectId)
    {
        if (objectId.Length < 4 || objectId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("Identificatore di oggetto non valido.");
        }

        return Path.Combine(_objectsRoot, objectId[..2], objectId + ".mwo");
    }

    public async Task<StoredObject> StoreFileAsync(
        string sourcePath,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var before = new FileInfo(sourcePath);
        if (!before.Exists)
        {
            throw new FileNotFoundException("File da acquisire non trovato.", sourcePath);
        }

        Directory.CreateDirectory(Path.Combine(_objectsRoot, ".tmp"));
        var temporary = Path.Combine(_objectsRoot, ".tmp", Guid.NewGuid().ToString("N") + ".tmp");
        var fileId = RandomNumberGenerator.GetBytes(16);
        var headerMagic = "MWO1"u8.ToArray();
        long plaintextLength = 0;

        try
        {
            await using var input = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var digest = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, keys.Deduplication);
            using var aes = new AesGcm(keys.Content, TagSize);

            await output.WriteAsync(headerMagic, cancellationToken);
            await output.WriteAsync(fileId, cancellationToken);
            await output.WriteAsync(BitConverter.GetBytes(ChunkSize), cancellationToken);
            await output.WriteAsync(BitConverter.GetBytes(before.Length), cancellationToken);

            var buffer = new byte[ChunkSize];
            var ciphertext = new byte[ChunkSize];
            var tag = new byte[TagSize];
            var chunkIndex = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await input.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                digest.AppendData(buffer.AsSpan(0, read));
                var nonce = CreateNonce(fileId, chunkIndex);
                var aad = CreateChunkAad(fileId, chunkIndex, read, before.Length, false);
                aes.Encrypt(nonce, buffer.AsSpan(0, read), ciphertext.AsSpan(0, read), tag, aad);
                await output.WriteAsync(BitConverter.GetBytes(read), cancellationToken);
                await output.WriteAsync(tag, cancellationToken);
                await output.WriteAsync(ciphertext.AsMemory(0, read), cancellationToken);
                plaintextLength += read;
                chunkIndex++;
                progress?.Report(plaintextLength);
            }

            var after = new FileInfo(sourcePath);
            if (!after.Exists
                || after.Length != before.Length
                || after.LastWriteTimeUtc != before.LastWriteTimeUtc
                || plaintextLength != before.Length)
            {
                throw new IOException("Il file è cambiato durante l'acquisizione. Operazione annullata per questo elemento.");
            }

            await output.FlushAsync(cancellationToken);
            output.Flush(true);
            var objectId = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            var finalPath = PathFor(objectId);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            await output.DisposeAsync();
            try
            {
                File.Move(temporary, finalPath, false);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                File.Delete(temporary);
            }

            return new StoredObject(objectId, plaintextLength);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    public async Task RestoreFileAsync(
        string objectId,
        string destinationPath,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var objectPath = PathFor(objectId);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var temporary = destinationPath + ".migrawin-" + Guid.NewGuid().ToString("N");
        try
        {
            await using var input = new FileStream(
                objectPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var digest = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, keys.Deduplication);
            using var aes = new AesGcm(keys.Content, TagSize);

            var magic = new byte[4];
            await input.ReadExactlyAsync(magic, cancellationToken);
            if (!magic.AsSpan().SequenceEqual("MWO1"u8))
            {
                throw new InvalidDataException("Oggetto cifrato non riconosciuto.");
            }

            var fileId = new byte[16];
            var chunkSizeBytes = new byte[sizeof(int)];
            var lengthBytes = new byte[sizeof(long)];
            await input.ReadExactlyAsync(fileId, cancellationToken);
            await input.ReadExactlyAsync(chunkSizeBytes, cancellationToken);
            await input.ReadExactlyAsync(lengthBytes, cancellationToken);
            var storedChunkSize = BitConverter.ToInt32(chunkSizeBytes);
            var expectedLength = BitConverter.ToInt64(lengthBytes);
            if (storedChunkSize != ChunkSize || expectedLength < 0)
            {
                throw new InvalidDataException("Intestazione dell'oggetto non valida.");
            }

            long restored = 0;
            var chunkIndex = 0;
            while (restored < expectedLength)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunkLengthBytes = new byte[sizeof(int)];
                var tag = new byte[TagSize];
                await input.ReadExactlyAsync(chunkLengthBytes, cancellationToken);
                await input.ReadExactlyAsync(tag, cancellationToken);
                var chunkLength = BitConverter.ToInt32(chunkLengthBytes);
                if (chunkLength <= 0 || chunkLength > ChunkSize || restored + chunkLength > expectedLength)
                {
                    throw new InvalidDataException("Dimensione del blocco cifrato non valida.");
                }

                var ciphertext = new byte[chunkLength];
                var plaintext = new byte[chunkLength];
                await input.ReadExactlyAsync(ciphertext, cancellationToken);
                var nonce = CreateNonce(fileId, chunkIndex);
                var aad = CreateChunkAad(fileId, chunkIndex, chunkLength, expectedLength, false);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
                digest.AppendData(plaintext);
                await output.WriteAsync(plaintext, cancellationToken);
                restored += chunkLength;
                chunkIndex++;
                progress?.Report(restored);
            }

            if (input.Position != input.Length)
            {
                throw new InvalidDataException("L'oggetto contiene dati inattesi.");
            }

            var calculated = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(calculated),
                    Encoding.ASCII.GetBytes(objectId)))
            {
                throw new CryptographicException("Il contenuto decifrato non corrisponde all'identità attesa.");
            }

            await output.FlushAsync(cancellationToken);
            output.Flush(true);
            await output.DisposeAsync();
            File.Move(temporary, destinationPath, true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    public async Task<long> VerifyFileAsync(string objectId, CancellationToken cancellationToken)
    {
        var temp = Path.Combine(Path.GetTempPath(), "MigraWin-verify-" + Guid.NewGuid().ToString("N"));
        try
        {
            await RestoreFileAsync(objectId, temp, null, cancellationToken);
            return new FileInfo(temp).Length;
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static byte[] CreateNonce(byte[] fileId, int chunkIndex)
    {
        var nonce = new byte[NonceSize];
        fileId.AsSpan(0, 8).CopyTo(nonce);
        BinaryPrimitives.WriteInt32BigEndian(nonce.AsSpan(8), chunkIndex);
        return nonce;
    }

    private static byte[] CreateChunkAad(byte[] fileId, int chunkIndex, int chunkLength, long totalLength, bool final)
    {
        var aad = new byte[16 + sizeof(int) + sizeof(int) + sizeof(long) + 1];
        fileId.CopyTo(aad, 0);
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(16), chunkIndex);
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(20), chunkLength);
        BinaryPrimitives.WriteInt64BigEndian(aad.AsSpan(24), totalLength);
        aad[^1] = final ? (byte)1 : (byte)0;
        return aad;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
