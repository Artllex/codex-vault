using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WindowsSecretManager.App;

internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static void Enable(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var enabled = 1;
            _ = DwmSetWindowAttribute(new WindowInteropHelper(window).Handle,
                DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
