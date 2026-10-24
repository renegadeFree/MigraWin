namespace MigraWin.Services;

public static class ScheduledMaintenance
{
    public static bool IsCleanupInvocation(IReadOnlyList<string> arguments) =>
        arguments.Any(argument => argument.Equals("--remove-schedules", StringComparison.OrdinalIgnoreCase));

    public static async Task<int> RemoveAllSchedulesAsync(
        CancellationToken cancellationToken = default,
        ProfileStore? profileStore = null,
        ScheduleService? scheduleService = null,
        LocalSecretStore? secretStore = null)
    {
        try
        {
            var effectiveProfiles = profileStore ?? new ProfileStore();
            var profiles = await effectiveProfiles.LoadAsync(cancellationToken);
            var schedules = scheduleService ?? new ScheduleService();
            var secrets = secretStore ?? new LocalSecretStore();
            var changed = false;
            foreach (var profile in profiles)
            {
                await schedules.DeleteAsync(profile.Id, cancellationToken);
                secrets.Delete(profile.Id);
                if (profile.ScheduleEnabled)
                {
                    profile.ScheduleEnabled = false;
                    profile.UpdatedAt = DateTimeOffset.Now;
                    changed = true;
                }
            }

            if (changed)
            {
                await effectiveProfiles.SaveAsync(profiles, cancellationToken);
            }

            return 0;
        }
        catch
        {
            return 1;
        }
    }
}
