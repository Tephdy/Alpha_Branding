using System.Windows;
using System.IO;

namespace Alpha.Branding;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        DispatcherUnhandledException += (s, e) =>
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup_error.log"), e.Exception.ToString());
            MessageBox.Show(e.Exception.ToString(), "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup_error.log"), ex.ToString());
            }
        };
    }
}


