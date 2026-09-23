using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace QuotaTray.Services;

/// <summary>
/// The tray app's own display preferences (the Mac app keeps these in its UI layer too), stored under
/// <c>HKCU\Software\QuotaTray</c>. Provider on/off and the Used/Left meter style live in the engine.
/// </summary>
public static class UiSettings
{
    private const string KeyPath = @"Software\QuotaTray";

    /// <summary>Settings, General, Show Total Spend. On by default, like the Mac app.</summary>
    public static bool ShowTotalSpend
    {
        get => Read("ShowTotalSpend") is not int value || value != 0;
        set => Write("ShowTotalSpend", value ? 1 : 0);
    }

    /// <summary>The Total Spend card's selected period id ("today", "yesterday", "last30").</summary>
    public static string SpendPeriod
    {
        get => Read("SpendPeriod") as string ?? "today";
        set => Write("SpendPeriod", value);
    }

    /// <summary>Settings, Usage Display, Icon Style. Text (numbers) by default.</summary>
    public static TrayStyle TrayStyle
    {
        get => Read("TrayStyle") as string == "bars" ? TrayStyle.Bars : TrayStyle.Text;
        set => Write("TrayStyle", value == TrayStyle.Bars ? "bars" : "text");
    }

    /// <summary>
    /// Set once Launch at Login got its on-by-default value (by the first launch or by the installer),
    /// so a later choice to turn it off sticks.
    /// </summary>
    public static bool LaunchAtLoginDefaulted
    {
        get => Read("LaunchAtLoginDefaulted") is int value && value != 0;
        set => Write("LaunchAtLoginDefaulted", value ? 1 : 0);
    }

    /// <summary>Windows 11 icon entries already moved out of the overflow once (see TrayPromotion).</summary>
    public static IReadOnlyCollection<string> PromotedTrayIcons
    {
        get => Read("PromotedTrayIcons") as string[] ?? Array.Empty<string>();
        set => Write("PromotedTrayIcons", value.ToArray(), RegistryValueKind.MultiString);
    }

    private static object? Read(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(name);
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException)
        {
            AppLog.Warn($"could not read setting {name}: {error.Message}");
            return null;
        }
    }

    private static void Write(string name, object value, RegistryValueKind kind = RegistryValueKind.Unknown)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
            key.SetValue(name, value, kind);
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException)
        {
            AppLog.Error($"could not save setting {name}: {error.Message}");
        }
    }
}

/// <summary>How the taskbar shows pinned readings, like the Mac's Icon Style.</summary>
public enum TrayStyle
{
    /// <summary>One icon per pinned reading, showing its number.</summary>
    Text,
    /// <summary>One icon with up to two mini meters.</summary>
    Bars,
}
