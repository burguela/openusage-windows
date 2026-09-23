using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using QuotaTray.Services;
using QuotaTray.Tray;

namespace QuotaTray;

/// <summary>
/// Entry point: one instance per user session, living in the notification area until Quit.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceName = @"Local\QuotaTray.Tray";

    private Mutex? _singleInstance;
    private TrayController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length == 3 && e.Args[0] == "--render-preview")
        {
            try
            {
                Shutdown(Preview.PreviewRenderer.Run(e.Args[1], e.Args[2]));
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"preview failed: {error}");
                Shutdown(1);
            }
            return;
        }

        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out var createdNew);
        if (!createdNew)
        {
            // Already running: its icon is in the notification area (possibly in the overflow).
            AppLog.Info("second launch ignored: Quota Tray is already running");
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error($"unhandled exception: {args.ExceptionObject}");

        AppLog.Info($"Quota Tray starting ({Environment.OSVersion})");
        try
        {
            LaunchAtLogin.RepairPathIfEnabled();
        }
        catch (Exception error)
        {
            AppLog.Warn($"launch at login repair failed: {error.Message}");
        }

        _tray = new TrayController(this);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _tray.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _tray?.Dispose();
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        AppLog.Info("Quota Tray exited");
        base.OnExit(e);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Light/dark mode changes arrive as the General category.
        if (e.Category == UserPreferenceCategory.General)
        {
            Dispatcher.BeginInvoke(() => _tray?.ThemeChanged());
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Log loudly, tell the user once, and keep the tray alive rather than vanishing silently.
        AppLog.Error($"unhandled UI exception: {e.Exception}");
        _tray?.ShowError("Quota Tray hit an unexpected error. Details are in the log folder.");
        e.Handled = true;
    }
}
