using System;
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

    private static void Write(string name, object value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
            key.SetValue(name, value);
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException)
        {
            AppLog.Error($"could not save setting {name}: {error.Message}");
        }
    }
}
