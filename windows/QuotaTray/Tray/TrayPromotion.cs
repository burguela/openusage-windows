using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using QuotaTray.Services;

namespace QuotaTray.Tray;

/// <summary>
/// Windows 11 puts a new app's notification-area icons in the hidden overflow ("^") until the user
/// switches them on under Settings, Personalization, Taskbar, Other system tray icons. The readings
/// are the point of Quota Tray, so each of its icons is switched on once, the first time Windows lists
/// it; after that the user's own choice in Settings stands. Windows 10 has no per-icon entries here,
/// so there the icons stay where the user drags them.
/// </summary>
internal static class TrayPromotion
{
    private const string SettingsPath = @"Control Panel\NotifyIconSettings";

    public static void PromoteNewIcons()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(SettingsPath);
            if (root == null)
            {
                return;
            }
            var exeName = Path.GetFileName(Environment.ProcessPath) ?? "QuotaTray.exe";
            var done = UiSettings.PromotedTrayIcons.ToHashSet(StringComparer.Ordinal);
            var changed = false;
            foreach (var name in root.GetSubKeyNames().Where(n => !done.Contains(n)))
            {
                using var entry = root.OpenSubKey(name, writable: true);
                // ExecutablePath can start with a known-folder id instead of a drive, so match the file name.
                if (entry?.GetValue("ExecutablePath") is not string path
                    || !string.Equals(Path.GetFileName(path), exeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                entry.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                done.Add(name);
                changed = true;
                AppLog.Info($"tray icon {name} shown on the taskbar");
            }
            if (changed)
            {
                UiSettings.PromotedTrayIcons = done;
            }
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            AppLog.Warn($"could not show the tray icons on the taskbar: {error.Message}");
        }
    }
}
