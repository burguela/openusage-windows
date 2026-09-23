using System;
using Microsoft.Win32;

namespace QuotaTray.Services;

/// <summary>
/// Launch at Login through the per-user <c>Run</c> key, the same place Windows' Startup apps
/// settings page reads and toggles.
/// </summary>
public static class LaunchAtLogin
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuotaTray";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else if (key.GetValue(ValueName) != null)
        {
            key.DeleteValue(ValueName);
        }
        AppLog.Info($"launch at login {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>Keeps an existing entry pointing at this copy after the app folder moves.</summary>
    public static void RepairPathIfEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key?.GetValue(ValueName) is string value && value.Length > 0 && value != Command)
        {
            key.SetValue(ValueName, Command, RegistryValueKind.String);
            AppLog.Info("launch at login path updated to this copy");
        }
    }
}
