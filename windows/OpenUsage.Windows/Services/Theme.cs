using System.Windows.Media;
using Microsoft.Win32;

namespace OpenUsage.Windows.Services;

/// <summary>
/// The panel's palette: the macOS system colors the Mac app uses (text background, grouped-card
/// fill, label colors, system blue/yellow/red), in light and dark, following Windows' app mode
/// (Settings, Personalization, Colors, "Choose your app mode").
/// </summary>
public sealed class Theme
{
    public bool IsDark { get; }
    /// <summary>The panel page behind the cards (macOS textBackgroundColor).</summary>
    public Brush Tray { get; }
    /// <summary>A grouped card lifted off the page (the page plus .fill.quaternary).</summary>
    public Brush Card { get; }
    public Brush PanelBorder { get; }
    public Brush TextPrimary { get; }
    public Brush TextSecondary { get; }
    public Brush TextTertiary { get; }
    /// <summary>Meter track and other quaternary fills.</summary>
    public Brush Track { get; }
    /// <summary>The period picker's capsule (quinary fill).</summary>
    public Brush Segmented { get; }
    public Brush SegmentSelected { get; }
    public Brush ButtonFill { get; }
    public Brush ButtonHover { get; }
    public Brush ChromeFill { get; }
    public Brush ChromeBorder { get; }
    public Brush FooterFill { get; }
    public Brush Separator { get; }
    public Brush Hover { get; }
    public Brush Blue { get; }
    public Brush Yellow { get; }
    public Brush Red { get; }
    public Brush Orange { get; }
    public Brush Tick { get; }
    public Brush SwitchOff { get; }
    public Brush ScrollThumb { get; }

    private Theme(bool dark)
    {
        IsDark = dark;
        Tray = Solid(dark ? "#1E1E1E" : "#FFFFFF");
        Card = Solid(dark ? "#2A2A2A" : "#F6F6F6");
        PanelBorder = Solid(dark ? "#3C3C3C" : "#D4D4D4");
        TextPrimary = Solid(dark ? "#E8E8E8" : "#262626");
        TextSecondary = Solid(dark ? "#9C9C9C" : "#808080");
        TextTertiary = Solid(dark ? "#666666" : "#B8B8B8");
        Track = Solid(dark ? "#3E3E3E" : "#E1E1E1");
        Segmented = Solid(dark ? "#353535" : "#EBEBEB");
        SegmentSelected = Solid(dark ? "#1E1E1E" : "#FFFFFF");
        ButtonFill = Solid(dark ? "#3A3A3A" : "#E6E6E6");
        ButtonHover = Solid(dark ? "#454545" : "#DCDCDC");
        ChromeFill = Solid(dark ? "#2E2E2E" : "#FFFFFF");
        ChromeBorder = Solid(dark ? "#474747" : "#D9D9D9");
        FooterFill = Solid(dark ? "#242424" : "#F9F9F9");
        Separator = Solid(dark ? "#353535" : "#E6E6E6");
        Hover = Solid(dark ? "#343434" : "#ECECEC");
        Blue = Solid(dark ? "#0A84FF" : "#007AFF");
        Yellow = Solid(dark ? "#FFD60A" : "#FFCC00");
        Red = Solid(dark ? "#FF453A" : "#FF3B30");
        Orange = Solid(dark ? "#FF9F0A" : "#FF9500");
        Tick = Solid(dark ? "#8CFFFFFF" : "#8C000000");
        SwitchOff = Solid(dark ? "#4A4A4A" : "#E3E3E3");
        ScrollThumb = Solid(dark ? "#66FFFFFF" : "#55000000");
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

    /// <summary>Pins light or dark regardless of the system setting (preview renders).</summary>
    public static void Force(bool dark) => Current = new Theme(dark);

    /// <summary>Meter fill for a severity band; the empty track when there's no data.</summary>
    public Brush Severity(string? severity) => severity switch
    {
        "warning" => Yellow,
        "critical" => Red,
        "none" => Track,
        _ => Blue,
    };

    /// <summary>
    /// The Total Spend ring's per-provider colors (the Mac app's brand palette), with the same stable
    /// fallback for providers that have none.
    /// </summary>
    public Brush SpendColor(string providerId)
    {
        var family = providerId.Split(':', 2)[0];
        var hex = family switch
        {
            "claude" => "#DE7356",
            "codex" => "#10A37F",
            "cursor" => IsDark ? "#F5F5F7" : "#13120A",
            "grok" => IsDark ? "#98989D" : "#8E8E93",
            "opencode" => IsDark ? "#AEAEB2" : "#6E6E73",
            "openrouter" => "#6467F2",
            "antigravity" => "#4285F4",
            "copilot" => "#A855F7",
            "zai" => IsDark ? "#D1D1D6" : "#2D2D2D",
            _ => null,
        };
        if (hex == null)
        {
            var fallback = new[] { "#34C759", "#5856D6", "#FF2D55", "#A2845E" };
            var hash = 0;
            foreach (var character in family)
            {
                hash = (hash * 31 + character) & 0xFFFF;
            }
            hex = fallback[hash % fallback.Length];
        }
        return Solid(hex);
    }

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
