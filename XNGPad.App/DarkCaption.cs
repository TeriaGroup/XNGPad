using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace XNGPad.App;

internal static class DarkCaption
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeOld = 19;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var dark = 1;
        _ = DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, UseImmersiveDarkModeOld, ref dark, sizeof(int));
        var caption = 0x001A1716;
        _ = DwmSetWindowAttribute(hwnd, CaptionColor, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, BorderColor, ref caption, sizeof(int));
        var text = 0x00EFEDED;
        _ = DwmSetWindowAttribute(hwnd, TextColor, ref text, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
