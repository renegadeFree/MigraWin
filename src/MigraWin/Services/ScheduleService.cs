using System.Diagnostics;
using MigraWin.Models;

namespace MigraWin.Services;

public sealed class ScheduleService
{
    public const string TaskPrefix = "MigraWin-Backup-";

    public async Task CreateDailyAsync(
        BackupProfile profile,
        string time,
        CancellationToken cancellationToken = default)
    {
        if (!TimeOnly.TryParseExact(time, "HH:mm", out var scheduleTime))
        {
            throw new FormatException("L'orario deve usare il formato HH:mm.");
        }

        var taskName = TaskName(profile.Id);
        var command = BuildCommand(profile.Id);
        await RunSchtasksAsync(
            ["/Create", "/TN", taskName, "/TR", command, "/SC", "DAILY", "/ST", scheduleTime.ToString("HH:mm"),
             "/RL", "LIMITED", "/F"],
            cancellationToken);
    }

    public async Task DeleteAsync(string profileId, CancellationToken cancellationToken = default)
    {
        await RunSchtasksAsync(
            ["/Delete", "/TN", TaskName(profileId), "/F"],
            cancellationToken,
            allowMissing: true);
    }

    public async Task<bool> ExistsAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var result = await RunSchtasksAsync(
            ["/Query", "/TN", TaskName(profileId), "/FO", "LIST"],
            cancellationToken,
            allowMissing: true);
        return result.ExitCode == 0;
    }

    public static string TaskName(string profileId)
    {
        if (profileId.Length != 32 || profileId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Identificatore profilo non valido.", nameof(profileId));
        }

        return TaskPrefix + profileId[..12];
    }

    private static string BuildCommand(string profileId)
    {
        var processPath = Environment.ProcessPath
                          ?? throw new InvalidOperationException("Percorso dell'eseguibile non disponibile.");
        var executableName = Path.GetFileNameWithoutExtension(processPath);
        var arguments = $"--scheduled-backup --profile \"{profileId}\"";
        if (executableName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var assembly = Path.Combine(AppContext.BaseDirectory, "MigraWin.dll");
            return $"\"{processPath}\" \"{assembly}\" {arguments}";
        }

        return $"\"{processPath}\" {arguments}";
    }

    private static async Task<ProcessResult> RunSchtasksAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool allowMissing = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Utilità di pianificazione non disponibile.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0 && !allowMissing)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(standardError) ? standardOutput.Trim() : standardError.Trim());
        }

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
