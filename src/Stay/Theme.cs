using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Stay;

/// <summary>Follows the Windows light or dark setting: swaps the brush palette and asks DWM for a matching title bar.</summary>
public static class Theme
{
    public static bool IsDark { get; private set; }

    public static void Apply(Application app, bool? force = null)
    {
        IsDark = force ?? SystemPrefersDark();
        if (!IsDark) return;
        var dark = new Dictionary<string, string>
        {
            ["Ground"] = "#1B1B20", ["Card"] = "#26262C", ["Line"] = "#36363E", ["Ink"] = "#F2F2F5", ["Muted"] = "#A2A2AB",
            ["Accent"] = "#4F86D8", ["AccentInk"] = "#FFFFFF",
            ["LoudBg"] = "#4A2323", ["LoudInk"] = "#F3A5A5", ["ShutBg"] = "#1F3A28", ["ShutInk"] = "#9BDBAE",
            ["PartlyBg"] = "#443417", ["PartlyInk"] = "#F0C56B", ["NavBg"] = "#202026", ["NavSelected"] = "#2E3A50",
        };
        foreach (var (key, hex) in dark)
            app.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
    }

    private static bool SystemPrefersDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Call once the window has a handle.</summary>
    public static void DecorateTitleBar(Window w)
    {
        if (!IsDark) return;
        try
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            int on = 1;
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        }
        catch { }
    }
}
