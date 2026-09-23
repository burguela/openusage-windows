using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OpenUsage.Windows.Engine;
using OpenUsage.Windows.Services;

namespace OpenUsage.Windows.Views;

/// <summary>What the popup asks its owner (the tray controller) to do.</summary>
public interface IPopupActions
{
    void RefreshNow();
    void SetProviderEnabled(string providerId, bool enabled);
    void SetMeterStyle(bool showRemaining);
    void SetLaunchAtLogin(bool enabled);
    void OpenLogFolder();
    void Quit();
}

/// <summary>
/// The dashboard panel that opens above the tray icon: provider sections with their meters, plus a
/// Settings screen in the same panel (like the Mac popover). Built in code from the engine's
/// display-ready dashboard; it closes when it loses focus.
/// </summary>
public partial class PopupWindow : Window
{
    private readonly IPopupActions _actions;
    private readonly HashSet<string> _expandedProviders = new();
    private Dashboard? _dashboard;
    private string? _engineError;
    private bool _refreshing;
    private bool _showingSettings;
    private bool _closing;

    /// <summary>When the panel last hid itself on focus loss (see <see cref="ShouldIgnoreToggle"/>).</summary>
    public DateTime LastAutoHide { get; private set; } = DateTime.MinValue;

    public PopupWindow(IPopupActions actions)
    {
        _actions = actions;
        InitializeComponent();
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) =>
        {
            // Closing (on Quit) also deactivates; hiding a closing window throws.
            if (_closing)
            {
                return;
            }
            LastAutoHide = DateTime.UtcNow;
            Hide();
        };
        SizeChanged += (_, _) => PositionNearTray();
        PreviewKeyDown += OnPreviewKeyDown;
        Render();
    }

    /// <summary>
    /// Clicking the tray icon while the panel is open first deactivates (hides) it, then delivers the
    /// click; treat that click as "close", not "open again".
    /// </summary>
    public bool ShouldIgnoreToggle => (DateTime.UtcNow - LastAutoHide).TotalMilliseconds < 350;

    public void ShowNearTray(bool settings = false)
    {
        _showingSettings = settings;
        Render();
        Show();
        PositionNearTray();
        Activate();
        Focus();
    }

    public void Update(Dashboard? dashboard, string? engineError, bool refreshing)
    {
        if (dashboard != null)
        {
            _dashboard = dashboard;
        }
        _engineError = engineError;
        _refreshing = refreshing;
        Render();
    }

    public void ApplyTheme() => Render();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_showingSettings)
            {
                _showingSettings = false;
                Render();
            }
            else
            {
                Hide();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.F5 || (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control))
        {
            _actions.RefreshNow();
            e.Handled = true;
        }
    }

    private void PositionNearTray()
    {
        if (!IsVisible)
        {
            return;
        }
        // Anchor to the work-area corner next to the taskbar's notification area. The work area
        // excludes the taskbar, so the edge it was trimmed from says where the taskbar sits.
        var area = SystemParameters.WorkArea;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        var taskbarOnTop = area.Top > 0;
        var taskbarOnLeft = area.Left > 0 && area.Width < screenWidth;
        Left = taskbarOnLeft ? area.Left : area.Right - ActualWidth;
        Top = taskbarOnTop || area.Height >= screenHeight ? area.Top : area.Bottom - ActualHeight;
        if (Top < area.Top)
        {
            Top = area.Top;
        }
    }

    // MARK: - Rendering

    private void Render()
    {
        var theme = Theme.Current;
        Frame.Background = theme.Background;
        Frame.BorderBrush = theme.Border;
        Foreground = theme.TextPrimary;
        Root.Children.Clear();

        var header = BuildHeader(theme);
        DockPanel.SetDock(header, Dock.Top);
        Root.Children.Add(header);

        var footer = BuildFooter(theme);
        DockPanel.SetDock(footer, Dock.Bottom);
        Root.Children.Add(footer);

        var body = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        if (_showingSettings)
        {
            BuildSettings(body, theme);
        }
        else
        {
            BuildDashboard(body, theme);
        }
        Root.Children.Add(new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height * 0.72),
            Padding = new Thickness(0, 0, 2, 0),
        });
    }

    private UIElement BuildHeader(Theme theme)
    {
        var grid = new Grid { Margin = new Thickness(16, 12, 10, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (_showingSettings)
        {
            title.Children.Add(LinkButton("‹ Back", theme, () =>
            {
                _showingSettings = false;
                Render();
            }, fontSize: 13));
            title.Children.Add(Text("Settings", theme.TextPrimary, 15, FontWeights.SemiBold, new Thickness(8, 0, 0, 0)));
        }
        else
        {
            if (ProviderMarks.For("openusage") is Geometry mark)
            {
                title.Children.Add(MarkIcon(mark, theme.Accent, 18, new Thickness(0, 0, 8, 0)));
            }
            title.Children.Add(Text("OpenUsage", theme.TextPrimary, 15, FontWeights.SemiBold));
        }
        grid.Children.Add(title);

        if (!_showingSettings)
        {
            var settings = LinkButton("Settings", theme, () =>
            {
                _showingSettings = true;
                Render();
            });
            Grid.SetColumn(settings, 1);
            grid.Children.Add(settings);
        }
        return grid;
    }

    private UIElement BuildFooter(Theme theme)
    {
        var border = new Border
        {
            BorderBrush = theme.Divider,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 8, 10, 10),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Text(StatusLine(), theme.TextSecondary, 12, verticalAlignment: VerticalAlignment.Center));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(LinkButton(_refreshing ? "Refreshing…" : "Refresh", theme, _actions.RefreshNow, enabled: !_refreshing));
        buttons.Children.Add(LinkButton("Quit", theme, _actions.Quit));
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);
        border.Child = grid;
        return border;
    }

    private string StatusLine()
    {
        if (_refreshing)
        {
            return "Updating usage…";
        }
        var newest = _dashboard?.Providers
            .Where(p => p.Enabled && p.RefreshedAt != null)
            .Select(p => p.RefreshedAt!.Value)
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max() ?? DateTimeOffset.MinValue;
        if (newest == DateTimeOffset.MinValue)
        {
            return "";
        }
        var age = DateTimeOffset.UtcNow - newest;
        if (age.TotalMinutes < 1)
        {
            return "Updated just now";
        }
        if (age.TotalHours < 1)
        {
            return $"Updated {(int)age.TotalMinutes}m ago";
        }
        return age.TotalDays < 1 ? $"Updated {(int)age.TotalHours}h ago" : $"Updated {(int)age.TotalDays}d ago";
    }

    private void BuildDashboard(StackPanel body, Theme theme)
    {
        if (_engineError != null)
        {
            body.Children.Add(Notice(_engineError, theme.Critical, theme, new Thickness(16, 4, 16, 8)));
        }
        if (_dashboard == null)
        {
            if (_engineError == null)
            {
                body.Children.Add(Text("Loading your usage…", theme.TextSecondary, 13, margin: new Thickness(16, 8, 16, 16)));
            }
            return;
        }

        var enabled = _dashboard.Providers.Where(p => p.Enabled).ToList();
        if (enabled.Count == 0)
        {
            body.Children.Add(Text("No providers are turned on. Open Settings to choose which ones to show.",
                theme.TextSecondary, 13, margin: new Thickness(16, 8, 16, 16), wrap: true));
            return;
        }
        for (var index = 0; index < enabled.Count; index++)
        {
            if (index > 0)
            {
                body.Children.Add(new Border { Height = 1, Background = theme.Divider, Margin = new Thickness(16, 4, 16, 4) });
            }
            body.Children.Add(BuildProvider(enabled[index], theme));
        }
    }

    private UIElement BuildProvider(ProviderInfo provider, Theme theme)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 6, 16, 6) };

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4), LastChildFill = true };
        if (ProviderMarks.For(provider.Id) is Geometry mark)
        {
            header.Children.Add(MarkIcon(mark, theme.TextSecondary, 16, new Thickness(0, 0, 8, 0)));
        }
        if (provider.Staleness != null)
        {
            var stale = Text("Outdated", theme.Warning, 11, verticalAlignment: VerticalAlignment.Center);
            stale.ToolTip = provider.Staleness;
            DockPanel.SetDock(stale, Dock.Right);
            header.Children.Add(stale);
        }
        var name = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        name.Inlines.Add(new System.Windows.Documents.Run(provider.DisplayName)
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = theme.TextPrimary,
        });
        if (!string.IsNullOrEmpty(provider.Plan))
        {
            name.Inlines.Add(new System.Windows.Documents.Run("  " + provider.Plan)
            {
                Foreground = theme.TextSecondary,
                FontSize = 12,
            });
        }
        header.Children.Add(name);
        panel.Children.Add(header);

        if (provider.Notice != null)
        {
            panel.Children.Add(Notice(provider.Notice, provider.IsError ? theme.Critical : theme.Warning, theme,
                new Thickness(0, 0, 0, 4)));
        }

        var expanded = _expandedProviders.Contains(provider.Id);
        foreach (var row in provider.Rows.Where(r => expanded || !r.OnDemand))
        {
            panel.Children.Add(BuildRow(row, theme));
        }

        var hasMore = provider.Rows.Any(r => r.OnDemand);
        if (hasMore || (expanded && provider.Links.Count > 0))
        {
            var actions = new WrapPanel { Margin = new Thickness(-6, 2, 0, 0) };
            if (hasMore)
            {
                actions.Children.Add(LinkButton(expanded ? "Show Less" : "Show More", theme, () =>
                {
                    if (!_expandedProviders.Remove(provider.Id))
                    {
                        _expandedProviders.Add(provider.Id);
                    }
                    Render();
                }, fontSize: 12));
            }
            if (expanded || !hasMore)
            {
                foreach (var link in provider.Links)
                {
                    actions.Children.Add(LinkButton(link.Label, theme, () => OpenUrl(link.Url), fontSize: 12));
                }
            }
            panel.Children.Add(actions);
        }
        return panel;
    }

    private UIElement BuildRow(RowInfo row, Theme theme)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        var top = new DockPanel { LastChildFill = true };
        var headline = Text(row.Headline, row.HasData ? theme.TextPrimary : theme.TextSecondary, 13,
            row.Kind == "meter" ? FontWeights.SemiBold : FontWeights.Normal);
        headline.Margin = new Thickness(8, 0, 0, 0);
        if (row.Note != null)
        {
            headline.ToolTip = row.Note;
        }
        DockPanel.SetDock(headline, Dock.Right);
        top.Children.Add(headline);
        top.Children.Add(Text(row.Title, theme.TextSecondary, 13, trim: true));
        panel.Children.Add(top);

        switch (row.Kind)
        {
            case "meter":
                panel.Children.Add(new MeterBar(row.Fraction ?? 0, row.PaceTick, theme.Severity(row.Severity))
                {
                    Margin = new Thickness(0, 5, 0, 3),
                    ToolTip = row.PaceTooltip,
                });
                if (row.Detail != null || row.PaceNote != null)
                {
                    var bottom = new DockPanel { LastChildFill = true };
                    if (row.PaceNote != null)
                    {
                        var pace = Text(row.PaceNote,
                            row.Severity == "critical" ? theme.Critical : theme.TextSecondary, 12);
                        DockPanel.SetDock(pace, Dock.Right);
                        bottom.Children.Add(pace);
                    }
                    bottom.Children.Add(Text(row.Detail ?? "", theme.TextSecondary, 12, trim: true));
                    panel.Children.Add(bottom);
                }
                break;
            case "chart" when row.Chart is { Count: > 0 } points:
                var bars = new TrendBars(points.Select(p => p.Value).ToArray()) { Margin = new Thickness(0, 5, 0, 0) };
                var latest = points[^1];
                bars.ToolTip = $"{latest.Label}: {latest.Readout}";
                panel.Children.Add(bars);
                break;
        }
        return panel;
    }
}
