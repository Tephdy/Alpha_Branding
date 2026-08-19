using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Alpha.Branding.Services;

public static class WindowThemeHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void EnableDarkTitleBar(Window window)
    {
        if (window.IsLoaded)
        {
            ApplyTheme(window);
        }
        else
        {
            window.SourceInitialized += (s, e) => ApplyTheme(window);
        }
    }

    private static void ApplyTheme(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int trueValue = 1;
        // Enable Immersive Dark Mode on Windows 10 (1809+) and Windows 11
        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueValue, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref trueValue, sizeof(int));
        }

        // Set custom dark caption background (#0A0A0A in COLORREF format 0x00BBGGRR)
        int captionColor = 0x000A0A0A;
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

        // Set caption text color (#E2C285 gold in COLORREF format: B=0x85, G=0xC2, R=0xE2)
        int textColor = 0x0085C2E2;
        DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
    }
}
