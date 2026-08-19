using System.Windows;

namespace Alpha.Branding.Bootstrapper;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool isUninstall = e.Args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase));
        bool isSilent = e.Args.Any(a =>
            string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "/s", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-s", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-q", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--quiet", StringComparison.OrdinalIgnoreCase));

        if (isSilent)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                if (isUninstall)
                {
                    await InstallerService.UninstallAsync();
                }
                else
                {
                    await InstallerService.InstallAsync(InstallerService.InstallDirectory);
                }
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Shutdown(1);
            }
            return;
        }

        if (isUninstall)
        {
            var win = new UninstallWindow();
            win.Show();
        }
        else
        {
            var win = new InstallWindow();
            win.Show();
        }
    }
}
