using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MigraWin.Services;

public sealed class LocalSecretStore
{
    private readonly string _directory;

    public LocalSecretStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MigraWin",
            "Schedules");
    }

    public async Task SaveAsync(string profileId, string secret, CancellationToken cancellationToken = default)
    {
        ValidateProfileId(profileId);
        Directory.CreateDirectory(_directory);
        var plaintext = Encoding.UTF8.GetBytes(secret);
        try
        {
            var protectedBytes = Protect(plaintext);
            var path = PathFor(profileId);
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, protectedBytes, cancellationToken);
            File.Move(temp, path, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task<string> LoadAsync(string profileId, CancellationToken cancellationToken = default)
    {
        ValidateProfileId(profileId);
        var path = PathFor(profileId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Credenziale protetta della pianificazione non trovata.", path);
        }

        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var plaintext = Unprotect(protectedBytes);
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public void Delete(string profileId)
    {
        ValidateProfileId(profileId);
        var path = PathFor(profileId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string PathFor(string profileId) => Path.Combine(_directory, profileId + ".credential");

    private static void ValidateProfileId(string profileId)
    {
        if (profileId.Length != 32 || profileId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Identificatore profilo non valido.", nameof(profileId));
        }
    }

    private static byte[] Protect(byte[] plaintext)
    {
        var input = new DataBlob();
        var output = new DataBlob();
        try
        {
            input.Size = plaintext.Length;
            input.Data = Marshal.AllocHGlobal(plaintext.Length);
            Marshal.Copy(plaintext, 0, input.Data, plaintext.Length);
            if (!CryptProtectData(
                    ref input,
                    "MigraWin scheduled backup credential",
                    nint.Zero,
                    nint.Zero,
                    nint.Zero,
                    0x1,
                    out output))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            var result = new byte[output.Size];
            Marshal.Copy(output.Data, result, 0, output.Size);
            return result;
        }
        finally
        {
            if (input.Data != nint.Zero)
            {
                Marshal.FreeHGlobal(input.Data);
            }

            if (output.Data != nint.Zero)
            {
                LocalFree(output.Data);
            }
        }
    }

    private static byte[] Unprotect(byte[] protectedBytes)
    {
        var input = new DataBlob();
        var output = new DataBlob();
        try
        {
            input.Size = protectedBytes.Length;
            input.Data = Marshal.AllocHGlobal(protectedBytes.Length);
            Marshal.Copy(protectedBytes, 0, input.Data, protectedBytes.Length);
            if (!CryptUnprotectData(
                    ref input,
                    nint.Zero,
                    nint.Zero,
                    nint.Zero,
                    nint.Zero,
                    0x1,
                    out output))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            var result = new byte[output.Size];
            Marshal.Copy(output.Data, result, 0, output.Size);
            return result;
        }
        finally
        {
            if (input.Data != nint.Zero)
            {
                Marshal.FreeHGlobal(input.Data);
            }

            if (output.Data != nint.Zero)
            {
                LocalFree(output.Data);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public nint Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string description,
        nint optionalEntropy,
        nint reserved,
        nint prompt,
        uint flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        nint description,
        nint optionalEntropy,
        nint reserved,
        nint prompt,
        uint flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern nint LocalFree(nint memory);
}
