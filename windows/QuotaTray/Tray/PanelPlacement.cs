using System;
using System.Runtime.InteropServices;
using static QuotaTray.Tray.NativeMethods;

namespace QuotaTray.Tray;

/// <summary>
/// Places the panel against the taskbar, above the taskbar strip (or the notification area when the
/// strip isn't showing), like the Mac popover under its menu-bar item. Everything is read live from
/// Windows in physical pixels, so it stays right after a resolution, scale, or taskbar change;
/// WPF's <c>SystemParameters</c> keep the values from when the app started.
/// </summary>
internal static class PanelPlacement
{
    private enum Edge { Bottom, Top, Left, Right }

    /// <summary>
    /// Moves <paramref name="window"/> to its spot. <paramref name="insetDip"/> is the transparent
    /// shadow margin around the visible panel, so the panel's own edge lines up with the anchor's.
    /// </summary>
    public static void Place(IntPtr window, RECT? strip, double insetDip)
    {
        if (!GetWindowRect(window, out var panel))
        {
            throw new ExternalException("GetWindowRect failed for the panel", Marshal.GetLastWin32Error());
        }
        var taskbar = FindWindow("Shell_TrayWnd", null);
        var work = WorkArea(taskbar, out var screen);
        var width = panel.Width;
        var height = panel.Height;
        var inset = (int)Math.Round(insetDip * GetDpiForWindow(window) / 96.0);

        int x, y;
        if (taskbar != IntPtr.Zero && GetWindowRect(taskbar, out var bar))
        {
            var anchor = strip ?? NotificationArea(taskbar) ?? bar;
            switch (EdgeOf(bar, screen))
            {
                case Edge.Top:
                    x = anchor.Right + inset - width;
                    y = Math.Max(work.Top, bar.Bottom);
                    break;
                case Edge.Left:
                    x = Math.Max(work.Left, bar.Right);
                    y = anchor.Bottom + inset - height;
                    break;
                case Edge.Right:
                    x = Math.Min(work.Right, bar.Left) - width;
                    y = anchor.Bottom + inset - height;
                    break;
                default:
                    // The panel's right edge lines up with the strip's, so it always opens in the same spot.
                    x = anchor.Right + inset - width;
                    y = Math.Min(work.Bottom, bar.Top) - height;
                    break;
            }
        }
        else
        {
            x = work.Right - width;
            y = work.Bottom - height;
        }
        x = Math.Max(work.Left, Math.Min(x, work.Right - width));
        y = Math.Max(work.Top, Math.Min(y, work.Bottom - height));
        if (!SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE))
        {
            throw new ExternalException("SetWindowPos failed for the panel", Marshal.GetLastWin32Error());
        }
    }

    /// <summary>The tallest the panel can be, in device-independent pixels at the taskbar's scale.</summary>
    public static double WorkAreaHeightDip()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        var work = WorkArea(taskbar, out _);
        var dpi = taskbar != IntPtr.Zero ? GetDpiForWindow(taskbar) : 0;
        return work.Height * 96.0 / (dpi == 0 ? 96 : dpi);
    }

    /// <summary>The work area of the taskbar's monitor (the primary one when there is no taskbar).</summary>
    private static RECT WorkArea(IntPtr taskbar, out RECT screen)
    {
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(taskbar, MONITOR_DEFAULTTOPRIMARY), ref info))
        {
            throw new ExternalException("GetMonitorInfo failed", Marshal.GetLastWin32Error());
        }
        screen = info.rcMonitor;
        return info.rcWork;
    }

    private static RECT? NotificationArea(IntPtr taskbar)
    {
        var notify = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        return notify != IntPtr.Zero && GetWindowRect(notify, out var rect) && rect.Width > 0 ? rect : null;
    }

    private static Edge EdgeOf(RECT bar, RECT screen)
    {
        if (bar.Width >= bar.Height)
        {
            return bar.Top + bar.Height / 2 < screen.Top + screen.Height / 2 ? Edge.Top : Edge.Bottom;
        }
        return bar.Left + bar.Width / 2 < screen.Left + screen.Width / 2 ? Edge.Left : Edge.Right;
    }
}
