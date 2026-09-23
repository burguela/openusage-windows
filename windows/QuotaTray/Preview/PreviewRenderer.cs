using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaTray.Engine;
using QuotaTray.Services;
using QuotaTray.Views;

namespace QuotaTray.Preview;

/// <summary>
/// <c>QuotaTray.exe --render-preview dashboard.json out-folder</c>: renders the panel (dashboard and
/// Settings, light and dark) to PNGs from a saved dashboard document, without the tray or the engine.
/// CI uses it to publish screenshots of the Windows UI.
/// </summary>
public static class PreviewRenderer
{
    public static int Run(string dashboardPath, string outputFolder)
    {
        var dashboard = JsonSerializer.Deserialize<Dashboard>(File.ReadAllText(dashboardPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"{dashboardPath} is not a dashboard document.");
        Directory.CreateDirectory(outputFolder);
        foreach (var dark in new[] { false, true })
        {
            foreach (var settings in new[] { false, true })
            {
                Theme.Force(dark);
                var window = new PopupWindow(new NoActions());
                var panel = window.RenderForPreview(dashboard, settings, expandFirstProvider: true);
                var name = $"{(settings ? "settings" : "dashboard")}-{(dark ? "dark" : "light")}.png";
                Save(panel, window, Path.Combine(outputFolder, name));
                window.Close();
            }
        }
        return 0;
    }

    private static void Save(FrameworkElement panel, Window window, string path)
    {
        // Lay the panel out on a neutral backdrop (so its shadow and corners read), at the window's
        // width and whatever height its content needs.
        var host = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98)),
            Child = panel,
            Width = window.Width,
        };
        TextOptions.SetTextFormattingMode(host, TextFormattingMode.Ideal);
        host.SetValue(TextElement.FontFamilyProperty, window.FontFamily);
        host.SetValue(TextElement.FontSizeProperty, window.FontSize);
        host.Resources.MergedDictionaries.Add(window.Resources);
        host.Measure(new Size(window.Width, double.PositiveInfinity));
        host.Arrange(new Rect(host.DesiredSize));
        host.UpdateLayout();

        const double scale = 2;
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(host.ActualWidth * scale), (int)Math.Ceiling(host.ActualHeight * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private sealed class NoActions : IPopupActions
    {
        public void RefreshNow() { }
        public void SetProviderEnabled(string providerId, bool enabled) { }
        public void SetMeterStyle(bool showRemaining) { }
        public void SetLaunchAtLogin(bool enabled) { }
        public void OpenLogFolder() { }
        public void Quit() { }
    }
}
