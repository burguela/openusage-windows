using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenUsage.Windows.Engine;
using OpenUsage.Windows.Services;
using OpenUsage.Windows.Views;

namespace OpenUsage.Windows.Preview;

/// <summary>
/// <c>OpenUsage.exe --render-preview dashboard.json out-folder</c>: renders the panel (dashboard and
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
                window.ShowForPreview(dashboard, settings, expandFirstProvider: true);
                var name = $"{(settings ? "settings" : "dashboard")}-{(dark ? "dark" : "light")}.png";
                Save(window, Path.Combine(outputFolder, name));
                window.Close();
            }
        }
        return 0;
    }

    private static void Save(Window window, string path)
    {
        const double scale = 2;
        var width = (int)Math.Ceiling(window.ActualWidth * scale);
        var height = (int)Math.Ceiling(window.ActualHeight * scale);
        var panel = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        panel.Render(window);
        // Put the transparent window on a neutral backdrop so its shadow and corners read in the image.
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, window.ActualWidth, window.ActualHeight);
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98)), null, bounds);
            context.DrawImage(panel, bounds);
        }
        var output = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        output.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(output));
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
