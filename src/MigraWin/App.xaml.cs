using System.Windows;
using MigraWin.Services;

namespace MigraWin;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (ScheduledMaintenance.IsCleanupInvocation(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(await ScheduledMaintenance.RemoveAllSchedulesAsync());
            return;
        }

        if (ScheduledBackupRunner.IsScheduledInvocation(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var profileId = ScheduledBackupRunner.GetArgumentValue(e.Args, "--profile");
            if (string.IsNullOrWhiteSpace(profileId))
            {
                Shutdown(2);
                return;
            }

            var result = await ScheduledBackupRunner.RunAsync(profileId);
            Shutdown(result.ExitCode);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
