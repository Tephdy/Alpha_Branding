using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Alpha.Branding.Bootstrapper;

public partial class InstallWindow : Window
{
    private readonly string _targetDirectory;

    public InstallWindow()
    {
        InitializeComponent();
        WindowThemeHelper.EnableDarkTitleBar(this);
        _targetDirectory = InstallerService.InstallDirectory;
        TxtInstallPath.Text = _targetDirectory;
        string version = InstallerService.GetPayloadVersion();
        TxtVersionInfo.Text = $"Version: {version}";
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        ReadyPanel.Visibility = Visibility.Collapsed;
        InstallingPanel.Visibility = Visibility.Visible;
        BtnInstall.IsEnabled = false;
        BtnCancel.IsEnabled = false;

        var progress = new Progress<(double Progress, string Status)>(update =>
        {
            InstallProgressBar.Value = update.Progress;
            TxtStatus.Text = update.Status;
        });

        try
        {
            await InstallerService.InstallAsync(_targetDirectory, progress);

            InstallingPanel.Visibility = Visibility.Collapsed;
            FinishedPanel.Visibility = Visibility.Visible;
            BtnInstall.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnFinish.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            InstallingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            TxtErrorMessage.Text = ex.Message;
            BtnInstall.Visibility = Visibility.Collapsed;
            BtnCancel.Content = "Close";
            BtnCancel.IsEnabled = true;
        }
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        if (ChkLaunchApp.IsChecked == true)
        {
            string appPath = InstallerService.AppExecutablePath;
            if (File.Exists(appPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(appPath)
                    {
                        WorkingDirectory = _targetDirectory,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
