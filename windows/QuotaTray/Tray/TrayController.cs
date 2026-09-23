using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using QuotaTray.Engine;
using QuotaTray.Services;
using QuotaTray.Views;
using Forms = System.Windows.Forms;

namespace QuotaTray.Tray;

/// <summary>
/// Owns the notification-area icon, its menu, the popup, and the refresh schedule. All engine work
/// runs off the UI thread; results are applied back on it.
/// </summary>
public sealed class TrayController : IPopupActions, IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    // While the panel is closed, check for stale providers every five minutes (the engine's
    // refresh interval); while it is open, every minute so countdowns stay current.
    private const int HiddenTicksPerRefresh = 5;

    private readonly App _app;
    private readonly EngineClient _engine;
    private readonly TrayIconSet _icons;
    private readonly PopupWindow _popup;
    private readonly DispatcherTimer _timer;
    private readonly Forms.ToolStripMenuItem _launchAtLoginItem;
    private Dashboard? _dashboard;
    private string? _engineError;
    private bool _refreshing;
    private int _ticksSinceRefresh;
    private bool _disposed;

    public TrayController(App app)
    {
        _app = app;
        Forms.Application.EnableVisualStyles();
        _engine = EngineClient.Locate();
        AppLog.Info($"engine: {_engine.ExecutablePath}");

        _popup = new PopupWindow(this);

        _launchAtLoginItem = new Forms.ToolStripMenuItem("Launch at Login", null, (_, _) =>
            SetLaunchAtLogin(!LaunchAtLogin.IsEnabled));
        var menu = new Forms.ContextMenuStrip();
        var open = new Forms.ToolStripMenuItem("Open Quota Tray", null, (_, _) => _popup.ShowNearTray());
        open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(open);
        menu.Items.Add(new Forms.ToolStripMenuItem("Refresh Now", null, (_, _) => RefreshNow()));
        menu.Items.Add(new Forms.ToolStripMenuItem("Settings", null, (_, _) => _popup.ShowNearTray(settings: true)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_launchAtLoginItem);
        menu.Items.Add(new Forms.ToolStripMenuItem("Open Log Folder", null, (_, _) => OpenLogFolder()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Quit Quota Tray", null, (_, _) => Quit()));
        menu.Opening += (_, _) => _launchAtLoginItem.Checked = SafeLaunchAtLoginState();

        _icons = new TrayIconSet(menu, OnIconClick);

        _timer = new DispatcherTimer { Interval = TickInterval };
        _timer.Tick += (_, _) => OnTick();
    }

    public void Start()
    {
        _icons.Show();
        _timer.Start();
        _ = StartupLoadAsync();
    }

    /// <summary>Show what's cached immediately, then refresh whatever is stale.</summary>
    private async Task StartupLoadAsync()
    {
        await LoadAsync(RefreshMode.Cached, showsProgress: false);
        await LoadAsync(RefreshMode.IfStale, showsProgress: true);
    }

    private void OnTick()
    {
        _ticksSinceRefresh++;
        // Windows lists a new icon a moment after it appears; catch it on the next tick.
        TrayPromotion.PromoteNewIcons();
        if (_refreshing)
        {
            return;
        }
        if (_popup.IsVisible || _ticksSinceRefresh >= HiddenTicksPerRefresh)
        {
            _ = LoadAsync(RefreshMode.IfStale, showsProgress: false);
        }
    }

    private void OnIconClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left)
        {
            return;
        }
        if (_popup.IsVisible)
        {
            _popup.Hide();
        }
        else if (!_popup.ShouldIgnoreToggle)
        {
            _popup.ShowNearTray();
        }
    }

    // MARK: - Engine

    private async Task LoadAsync(RefreshMode mode, bool showsProgress)
    {
        if (mode != RefreshMode.Cached)
        {
            _refreshing = true;
            _ticksSinceRefresh = 0;
            if (showsProgress)
            {
                _popup.Update(null, _engineError, refreshing: true);
            }
        }
        try
        {
            Apply(await Task.Run(() => _engine.DashboardAsync(mode)), error: null);
        }
        catch (EngineException error)
        {
            AppLog.Error($"dashboard ({mode}) failed: {error}");
            Apply(null, error.Message);
        }
        finally
        {
            if (mode != RefreshMode.Cached)
            {
                _refreshing = false;
                _popup.Update(null, _engineError, refreshing: false);
            }
        }
    }

    private async Task RunCommandAsync(string description, Func<Task<Dashboard>> command)
    {
        try
        {
            Apply(await Task.Run(command), error: null);
        }
        catch (EngineException error)
        {
            AppLog.Error($"{description} failed: {error}");
            Apply(null, error.Message);
        }
    }

    private void Apply(Dashboard? dashboard, string? error)
    {
        if (_disposed)
        {
            return;
        }
        if (dashboard != null)
        {
            _dashboard = dashboard;
        }
        _engineError = error;
        _popup.Update(dashboard, error, _refreshing);
        UpdateIcons();
    }

    private void UpdateIcons() => _icons.Update(_dashboard, _engineError, UiSettings.TrayStyle);

    // MARK: - IPopupActions

    public void RefreshNow()
    {
        if (!_refreshing)
        {
            _ = LoadAsync(RefreshMode.Force, showsProgress: true);
        }
    }

    public void SetProviderEnabled(string providerId, bool enabled)
    {
        _ = RunCommandAsync($"{(enabled ? "enable" : "disable")} {providerId}", async () =>
        {
            var dashboard = await _engine.SetProviderEnabledAsync(providerId, enabled);
            // A newly enabled provider has nothing cached yet: fetch it right away.
            return enabled ? await _engine.DashboardAsync(RefreshMode.IfStale) : dashboard;
        });
    }

    public void SetMeterStyle(bool showRemaining)
    {
        _ = RunCommandAsync("meter style", () => _engine.SetMeterStyleAsync(showRemaining));
    }

    public void SetTrayStyle(TrayStyle style)
    {
        UiSettings.TrayStyle = style;
        UpdateIcons();
        _popup.Update(null, _engineError, _refreshing);
    }

    public void SetLaunchAtLogin(bool enabled)
    {
        try
        {
            LaunchAtLogin.SetEnabled(enabled);
        }
        catch (Exception error)
        {
            AppLog.Error($"launch at login change failed: {error}");
            ShowError("Quota Tray couldn't change Launch at Login.");
        }
        _popup.Update(null, _engineError, _refreshing);
    }

    public void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(AppLog.Directory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppLog.Directory}\"") { UseShellExecute = true });
        }
        catch (Exception error)
        {
            AppLog.Error($"open log folder failed: {error}");
            ShowError($"Quota Tray couldn't open {AppLog.Directory}.");
        }
    }

    public void Quit()
    {
        AppLog.Info("quit requested");
        Dispose();
        _app.Shutdown();
    }

    // MARK: - Other

    public void ThemeChanged()
    {
        Theme.Reload();
        _popup.ApplyTheme();
        // The icons follow the taskbar's own light/dark setting.
        UpdateIcons();
    }

    public void ShowError(string message)
    {
        if (!_disposed)
        {
            _icons.ShowBalloon(message);
        }
    }

    private static bool SafeLaunchAtLoginState()
    {
        try
        {
            return LaunchAtLogin.IsEnabled;
        }
        catch (Exception error)
        {
            AppLog.Warn($"launch at login read failed: {error.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _timer.Stop();
        _icons.Dispose();
        _popup.Close();
    }
}
