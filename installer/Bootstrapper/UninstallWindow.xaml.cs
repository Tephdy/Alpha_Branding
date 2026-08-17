using System.Windows;

namespace Alpha.Branding.Bootstrapper;

public partial class UninstallWindow : Window
{
    public UninstallWindow()
    {
        InitializeComponent();
        WindowThemeHelper.EnableDarkTitleBar(this);
    }

    private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        ReadyPanel.Visibility = Visibility.Collapsed;
        UninstallingPanel.Visibility = Visibility.Visible;
        BtnUninstall.IsEnabled = false;
        BtnCancel.IsEnabled = false;

        var progress = new Progress<(double Progress, string Status)>(update =>
        {
            UninstallProgressBar.Value = update.Progress;
            TxtStatus.Text = update.Status;
        });

        try
        {
            await InstallerService.UninstallAsync(progress);

            UninstallingPanel.Visibility = Visibility.Collapsed;
            FinishedPanel.Visibility = Visibility.Visible;
            BtnUninstall.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnClose.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            UninstallingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            TxtErrorMessage.Text = ex.Message;
            BtnUninstall.Visibility = Visibility.Collapsed;
            BtnCancel.Content = "Close";
            BtnCancel.IsEnabled = true;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
