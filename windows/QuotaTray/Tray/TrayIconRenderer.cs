using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using QuotaTray.Engine;

namespace QuotaTray.Tray;

/// <summary>
/// Draws the notification-area icon. With data it shows up to two mini meters for the pinned
/// rows (the Windows counterpart of the Mac menu-bar strip); without data it shows the app icon.
/// </summary>
public sealed class TrayIconRenderer : IDisposable
{
    private readonly Icon _appIcon;
    private Icon? _current;

    public TrayIconRenderer()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("QuotaTray.ico")
            ?? throw new InvalidOperationException("The embedded app icon is missing.");
        _appIcon = new Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
    }

    public Icon AppIcon => _appIcon;

    /// <summary>The meters the icon draws: the first two pinned meter rows that have data.</summary>
    public static IReadOnlyList<RowInfo> IconMeters(Dashboard? dashboard) =>
        dashboard == null
            ? Array.Empty<RowInfo>()
            : dashboard.Providers
                .Where(p => p.Enabled)
                .SelectMany(p => p.Rows)
                .Where(r => r.Pinned && r.Kind == "meter" && r.HasData && r.Fraction.HasValue)
                .Take(2)
                .ToList();

    /// <summary>Returns the icon to show. The previous rendered icon is released.</summary>
    public Icon Render(Dashboard? dashboard)
    {
        var meters = IconMeters(dashboard);
        var previous = _current;
        _current = meters.Count == 0 ? null : DrawMeters(meters);
        previous?.Dispose();
        return _current ?? _appIcon;
    }

    private static Icon DrawMeters(IReadOnlyList<RowInfo> meters)
    {
        var size = System.Windows.Forms.SystemInformation.SmallIconSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var lightTaskbar = TaskbarUsesLightTheme();
            var track = lightTaskbar ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(90, 255, 255, 255);
            var scale = size.Height / 16f;
            var barHeight = (meters.Count == 1 ? 6f : 5f) * scale;
            var gap = 2f * scale;
            var total = meters.Count * barHeight + (meters.Count - 1) * gap;
            var top = (size.Height - total) / 2f;
            var inset = 1f * scale;
            var width = size.Width - 2 * inset;

            foreach (var meter in meters)
            {
                var fraction = Math.Clamp(meter.Fraction ?? 0, 0, 1);
                using (var trackBrush = new SolidBrush(track))
                {
                    FillRounded(graphics, trackBrush, new RectangleF(inset, top, width, barHeight));
                }
                if (fraction > 0)
                {
                    using var fillBrush = new SolidBrush(FillColor(meter.Severity, lightTaskbar));
                    var fillWidth = Math.Max(barHeight, (float)(width * fraction));
                    FillRounded(graphics, fillBrush, new RectangleF(inset, top, fillWidth, barHeight));
                }
                top += barHeight + gap;
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            // Icon.FromHandle doesn't own the handle; clone it so the HICON can be freed right away.
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static Color FillColor(string? severity, bool lightTaskbar) => severity switch
    {
        "warning" => Color.FromArgb(0xFF, 0xCC, 0x00),
        "critical" => Color.FromArgb(0xFF, 0x3B, 0x30),
        _ => lightTaskbar ? Color.FromArgb(0x1B, 0x1C, 0x1F) : Color.White,
    };

    private static void FillRounded(Graphics graphics, Brush brush, RectangleF rect)
    {
        var radius = rect.Height / 2f;
        using var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, radius * 2, rect.Height, 90, 180);
        path.AddArc(rect.Right - radius * 2, rect.Top, radius * 2, rect.Height, 270, 180);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    private static bool TaskbarUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        // SystemUsesLightTheme covers the taskbar; missing (Windows 10 before 1903) means dark.
        return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
    }

    public void Dispose()
    {
        _current?.Dispose();
        _appIcon.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
