using System;
using System.Drawing;
using System.Linq;
using System.Text;
using QuotaTray.Engine;
using Forms = System.Windows.Forms;

namespace QuotaTray.Tray;

/// <summary>
/// The app's notification-area icon: the app icon while the taskbar strip shows the readings,
/// otherwise mini meters (Bars style, or a taskbar with no room for the strip). Its hover text lists
/// every pinned reading either way.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    // NotifyIcon.Text throws above 127 characters.
    private const int TooltipLimit = 127;

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon _appIcon;
    private Icon? _rendered;

    public TrayIcon(Forms.ContextMenuStrip menu, Forms.MouseEventHandler onClick)
    {
        _appIcon = TrayIconRenderer.LoadAppIcon();
        _notifyIcon = new Forms.NotifyIcon { Icon = _appIcon, Text = "Quota Tray", ContextMenuStrip = menu };
        _notifyIcon.MouseClick += onClick;
    }

    public void Show() => _notifyIcon.Visible = true;

    public void Update(Dashboard? dashboard, string? error, bool drawMeters)
    {
        var meters = drawMeters ? TrayIconRenderer.IconMeters(dashboard) : Array.Empty<RowInfo>();
        var previous = _rendered;
        _rendered = meters.Count == 0 ? null : TrayIconRenderer.DrawMeters(meters, TrayIconRenderer.TaskbarUsesLightTheme());
        _notifyIcon.Icon = _rendered ?? _appIcon;
        _notifyIcon.Text = Tooltip(dashboard, error);
        previous?.Dispose();
    }

    public void ShowBalloon(string message) =>
        _notifyIcon.ShowBalloonTip(5000, "Quota Tray", message, Forms.ToolTipIcon.Warning);

    /// <summary>"Quota Tray" plus the pinned readings, e.g. "Claude: Session 58% · Weekly 40%".</summary>
    internal static string Tooltip(Dashboard? dashboard, string? error)
    {
        var text = new StringBuilder("Quota Tray");
        if (error != null)
        {
            text.Append("\nCouldn't Refresh");
        }
        foreach (var provider in dashboard?.Providers.Where(p => p.Enabled) ?? Enumerable.Empty<ProviderInfo>())
        {
            var readings = provider.Rows
                .Where(r => r.Pinned && r.HasData && r.CompactValue.Length > 0)
                .Select(r => $"{r.Title} {r.CompactValue}")
                .ToList();
            if (readings.Count > 0)
            {
                text.Append('\n').Append(provider.DisplayName).Append(": ").Append(string.Join(" · ", readings));
            }
        }
        var value = text.ToString();
        return value.Length <= TooltipLimit ? value : value[..(TooltipLimit - 1)] + "…";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _rendered?.Dispose();
        _appIcon.Dispose();
    }
}
