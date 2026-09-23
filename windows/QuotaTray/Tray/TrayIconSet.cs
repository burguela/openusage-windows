using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using QuotaTray.Engine;
using QuotaTray.Services;
using Forms = System.Windows.Forms;

namespace QuotaTray.Tray;

/// <summary>
/// The app's notification-area icons. The first icon always exists; in Text style one more shows per
/// pinned reading, like the numbers in the Mac menu-bar strip. Icons that aren't needed are hidden
/// rather than removed so each keeps its identity, and with it the place Windows remembers for it.
/// </summary>
public sealed class TrayIconSet : IDisposable
{
    // NotifyIcon.Text throws above 127 characters.
    private const int TooltipLimit = 127;

    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.MouseEventHandler _onClick;
    private readonly Icon _appIcon;
    private readonly List<Slot> _slots = new();

    public TrayIconSet(Forms.ContextMenuStrip menu, Forms.MouseEventHandler onClick)
    {
        _menu = menu;
        _onClick = onClick;
        _appIcon = TrayIconRenderer.LoadAppIcon();
        AddSlot().NotifyIcon.Text = "Quota Tray";
    }

    public void Show() => _slots[0].NotifyIcon.Visible = true;

    public void Update(Dashboard? dashboard, string? error, TrayStyle style)
    {
        var light = TrayIconRenderer.TaskbarUsesLightTheme();
        var views = new List<(Icon? Icon, string Tooltip)>();
        var readings = style == TrayStyle.Text ? TrayIconRenderer.Readings(dashboard) : Array.Empty<TrayReading>();
        if (readings.Count > 0)
        {
            views.AddRange(readings.Select(r => ((Icon?)TrayIconRenderer.DrawReading(r, light), ReadingTooltip(r, error))));
        }
        else
        {
            var meters = style == TrayStyle.Bars ? TrayIconRenderer.IconMeters(dashboard) : Array.Empty<RowInfo>();
            views.Add((meters.Count == 0 ? null : TrayIconRenderer.DrawMeters(meters, light), Tooltip(dashboard, error)));
        }

        while (_slots.Count < views.Count)
        {
            AddSlot();
        }
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            var previous = slot.Rendered;
            if (i < views.Count)
            {
                slot.Rendered = views[i].Icon;
                slot.NotifyIcon.Icon = views[i].Icon ?? _appIcon;
                slot.NotifyIcon.Text = Clip(views[i].Tooltip);
                slot.NotifyIcon.Visible = true;
            }
            else
            {
                slot.NotifyIcon.Visible = false;
                slot.Rendered = null;
            }
            previous?.Dispose();
        }
        TrayPromotion.PromoteNewIcons();
    }

    public void ShowBalloon(string message) =>
        _slots[0].NotifyIcon.ShowBalloonTip(5000, "Quota Tray", message, Forms.ToolTipIcon.Warning);

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
        return Clip(text.ToString());
    }

    /// <summary>One reading's hover text, e.g. "Claude Weekly\n36% left · Resets in 1d 6h".</summary>
    internal static string ReadingTooltip(TrayReading reading, string? error)
    {
        var text = new StringBuilder($"{reading.Provider.DisplayName} {reading.Row.Title}\n");
        text.Append(reading.Row.Headline.Length > 0 ? reading.Row.Headline : reading.Row.CompactValue);
        if (!string.IsNullOrEmpty(reading.Row.Detail))
        {
            text.Append(" · ").Append(reading.Row.Detail);
        }
        if (error != null)
        {
            text.Append("\nCouldn't Refresh");
        }
        return Clip(text.ToString());
    }

    private static string Clip(string value) =>
        value.Length <= TooltipLimit ? value : value[..(TooltipLimit - 1)] + "…";

    private Slot AddSlot()
    {
        var notifyIcon = new Forms.NotifyIcon { Icon = _appIcon, ContextMenuStrip = _menu };
        notifyIcon.MouseClick += _onClick;
        var slot = new Slot(notifyIcon);
        _slots.Add(slot);
        return slot;
    }

    public void Dispose()
    {
        foreach (var slot in _slots)
        {
            slot.NotifyIcon.Visible = false;
            slot.NotifyIcon.Dispose();
            slot.Rendered?.Dispose();
        }
        _slots.Clear();
        _appIcon.Dispose();
    }

    private sealed class Slot(Forms.NotifyIcon notifyIcon)
    {
        public Forms.NotifyIcon NotifyIcon { get; } = notifyIcon;
        public Icon? Rendered { get; set; }
    }
}
