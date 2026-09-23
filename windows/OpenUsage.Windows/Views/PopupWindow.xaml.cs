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
/// The panel that opens above the tray icon, laid out like the Mac popover: the Total Spend card,
/// then one grouped card per provider, and a pinned footer with the version, the next-update
/// countdown, and the Options menu. Settings opens in the same panel. Built in code from the
/// engine's display-ready dashboard; it closes when it loses focus.
/// </summary>
public partial class PopupWindow : Window
{
    // The Mac popover's metrics (DashboardView, DensitySetting.regular), in device-independent pixels.
    private const double OuterPadding = 14;
    private const double SectionSpacing = 14;
    private const double HeaderToCard = 4;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly IPopupActions _actions;
    private readonly HashSet<string> _expandedProviders = new();
    private readonly DispatcherTimer _clock;
    private Dashboard? _dashboard;
    private string? _engineError;
    private bool _refreshing;
    private bool _showingSettings;
    private bool _closing;
    private TextBlock? _footerStatus;
    /// <summary>Preview renders freeze the clock and let the panel grow to its full height.</summary>
    private DateTimeOffset? _previewNow;

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
        // The footer's "Next update in …" counts down every second while the panel is open.
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateFooterStatus();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                _clock.Start();
            }
            else
            {
                _clock.Stop();
            }
        };
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

    /// <summary>Shows the panel off-screen without activating it, for <c>--render-preview</c>.</summary>
    public void ShowForPreview(Dashboard dashboard, bool settings, bool expandFirstProvider)
    {
        _dashboard = dashboard;
        _showingSettings = settings;
        _previewNow = dashboard.GeneratedAt;
        if (expandFirstProvider && dashboard.Providers.FirstOrDefault(p => p.Enabled) is { } first)
        {
            _expandedProviders.Add(first.Id);
        }
        Render();
        ShowActivated = false;
        Left = -20000;
        Top = 0;
        Show();
        UpdateLayout();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_showingSettings)
            {
                ShowScreen(settings: false);
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

    private void ShowScreen(bool settings)
    {
        _showingSettings = settings;
        Render();
    }

    private void PositionNearTray()
    {
        if (!IsVisible || !ShowActivated)
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
        Frame.Background = theme.Tray;
        Frame.BorderBrush = theme.PanelBorder;
        Foreground = theme.TextPrimary;
        Resources["ScrollThumbBrush"] = theme.ScrollThumb;
        Root.Children.Clear();

        if (_showingSettings)
        {
            var topBar = BuildTopBar(theme);
            DockPanel.SetDock(topBar, Dock.Top);
            Root.Children.Add(topBar);
        }

        var footer = BuildFooter(theme);
        DockPanel.SetDock(footer, Dock.Bottom);
        Root.Children.Add(footer);

        var body = new StackPanel
        {
            Margin = new Thickness(OuterPadding, _showingSettings ? 4 : OuterPadding, OuterPadding, 12),
        };
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
            // Grow with the content up to the screen, like the Mac panel.
            MaxHeight = _previewNow != null
                ? double.PositiveInfinity
                : Math.Max(240, SystemParameters.WorkArea.Height - 24 - 16 - 64 - (_showingSettings ? 44 : 0)),
            Focusable = false,
        });
    }

    /// <summary>Settings' navigation bar: a Back capsule and the centered screen title.</summary>
    private UIElement BuildTopBar(Theme theme)
    {
        var grid = new Grid { Height = 44, Margin = new Thickness(OuterPadding, 0, OuterPadding, 0) };
        grid.Children.Add(Text("Settings", theme.TextPrimary, 13, FontWeights.SemiBold,
            horizontalAlignment: HorizontalAlignment.Center, verticalAlignment: VerticalAlignment.Center));
        var back = new StackPanel { Orientation = Orientation.Horizontal };
        back.Children.Add(Glyph(Glyphs.ChevronLeft, theme.TextPrimary, 10, stroke: 1.6, margin: new Thickness(0, 0, 5, 0)));
        back.Children.Add(Text("Back", theme.TextPrimary, 13, FontWeights.Medium, verticalAlignment: VerticalAlignment.Center));
        var button = Capsule(back, theme, () => ShowScreen(settings: false), new Thickness(10, 0, 14, 0));
        button.HorizontalAlignment = HorizontalAlignment.Left;
        grid.Children.Add(button);
        return grid;
    }

    /// <summary>
    /// The pinned footer: "OpenUsage x.y.z" over the next-update countdown (click it to refresh now),
    /// and on the dashboard the Options menu capsule.
    /// </summary>
    private UIElement BuildFooter(Theme theme)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var version = _dashboard?.AppVersion is { Length: > 0 } value ? $"OpenUsage {value}" : "OpenUsage";
        identity.Children.Add(Text(version, theme.TextSecondary, 11));
        _footerStatus = Text("", theme.TextSecondary, 11);
        _footerStatus.Cursor = Cursors.Hand;
        _footerStatus.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (!_refreshing)
            {
                _actions.RefreshNow();
            }
        };
        identity.Children.Add(_footerStatus);
        UpdateFooterStatus();
        grid.Children.Add(identity);

        if (!_showingSettings)
        {
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(Text("Options", theme.TextPrimary, 13, FontWeights.SemiBold, verticalAlignment: VerticalAlignment.Center));
            label.Children.Add(Glyph(Glyphs.ChevronDown, theme.TextPrimary, 9, stroke: 1.8, margin: new Thickness(6, 1, 0, 0)));
            FrameworkElement? options = null;
            options = Capsule(label, theme, () => ShowMenu(options!, above: true, new MenuItemSpec[]
            {
                new("Settings", () => ShowScreen(settings: true)),
                new("Open Log Folder", _actions.OpenLogFolder),
                MenuItemSpec.Separator,
                new("Quit OpenUsage", _actions.Quit),
            }), new Thickness(14, 0, 12, 0));
            Grid.SetColumn(options, 1);
            grid.Children.Add(options);
        }

        return new Border
        {
            Background = theme.FooterFill,
            BorderBrush = theme.Separator,
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0, 0, 12, 12),
            Padding = new Thickness(OuterPadding, 12, OuterPadding, 12),
            Child = grid,
        };
    }

    private void UpdateFooterStatus()
    {
        if (_footerStatus == null)
        {
            return;
        }
        _footerStatus.Text = FooterStatus(_previewNow ?? DateTimeOffset.UtcNow);
    }

    /// <summary>"Next update in 3m" (seconds under a minute), or "Updating…" while a refresh runs.</summary>
    private string FooterStatus(DateTimeOffset now)
    {
        if (_refreshing)
        {
            return "Updating…";
        }
        var newest = _dashboard?.Providers
            .Where(p => p.Enabled && p.RefreshedAt != null)
            .Select(p => p.RefreshedAt!.Value)
            .DefaultIfEmpty(now)
            .Max() ?? now;
        var remaining = newest + RefreshInterval - now;
        var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        if (seconds <= 0)
        {
            // Due: the tray's next tick (at most a minute away while the panel is open) refreshes.
            return "Updating…";
        }
        return seconds >= 60 ? $"Next update in {(int)Math.Ceiling(seconds / 60.0)}m" : $"Next update in {seconds}s";
    }
}
