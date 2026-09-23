using System;
using System.Windows.Media;
using Microsoft.Win32;

namespace OpenUsage.Windows.Services;

/// <summary>
/// The popup's palette, following Windows' app light/dark mode
/// (Settings → Personalization → Colors → "Choose your app mode").
/// </summary>
public sealed class Theme
{
    public bool IsDark { get; }
    public Brush Background { get; }
    public Brush Border { get; }
    public Brush TextPrimary { get; }
    public Brush TextSecondary { get; }
    public Brush Track { get; }
    public Brush Divider { get; }
    public Brush Hover { get; }
    public Brush Accent { get; }
    public Brush Warning { get; }
    public Brush Critical { get; }
    public Brush Tick { get; }

    private Theme(bool dark)
    {
        IsDark = dark;
        Background = Solid(dark ? "#FF202124" : "#FFFBFBFC");
        Border = Solid(dark ? "#FF3A3B3F" : "#FFDADCE0");
        TextPrimary = Solid(dark ? "#FFF1F2F4" : "#FF1B1C1F");
        TextSecondary = Solid(dark ? "#FF9EA3AB" : "#FF6B7078");
        Track = Solid(dark ? "#FF34363B" : "#FFE6E8EC");
        Divider = Solid(dark ? "#FF2E3034" : "#FFECEEF1");
        Hover = Solid(dark ? "#FF2C2E32" : "#FFEEF0F3");
        Accent = Solid("#FF2B7EFF");
        Warning = Solid("#FFF5A524");
        Critical = Solid("#FFF0443A");
        Tick = Solid(dark ? "#CCF1F2F4" : "#AA1B1C1F");
    }

    public static Theme Current { get; private set; } = new(ReadSystemDarkMode());

    /// <summary>Re-reads the Windows setting; returns true when the mode changed.</summary>
    public static bool Reload()
    {
        var dark = ReadSystemDarkMode();
        if (dark == Current.IsDark)
        {
            return false;
        }
        Current = new Theme(dark);
        return true;
    }

    public Brush Severity(string? severity) => severity switch
    {
        "warning" => Warning,
        "critical" => Critical,
        "none" => Track,
        _ => Accent,
    };

    private static bool ReadSystemDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        // AppsUseLightTheme: 0 = dark, 1 = light. Missing (older builds) means light.
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    private static Brush Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
