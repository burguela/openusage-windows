using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shapes = System.Windows.Shapes;
using QuotaTray.Engine;
using QuotaTray.Tray;
using Drawing = System.Drawing;

namespace QuotaTray.Preview;

/// <summary>
/// Renders the notification-area end of the taskbar with Quota Tray's real icons (the same readings
/// <see cref="TrayIconRenderer"/> draws in the default Text style) and one icon's hover tooltip, so
/// the previews show what sits in the taskbar. The taskbar around the icons is a simplified Windows 11
/// look.
/// </summary>
internal static class TrayPreview
{
    private const double Width = 340;
    private const double TaskbarHeight = 48;

    public static void Save(Dashboard dashboard, bool dark, string path)
    {
        var readings = TrayIconRenderer.Readings(dashboard);
        if (readings.Count == 0)
        {
            throw new InvalidOperationException("The preview dashboard has no pinned readings to draw.");
        }
        // The tooltip belongs to the icon under the pointer: the last one, nearest the clock.
        var hovered = readings.Count - 1;
        var taskbarFill = dark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF3, 0xF3);
        var text = dark ? Colors.White : Color.FromRgb(0x1B, 0x1B, 0x1B);
        var hover = dark ? Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x14, 0x00, 0x00, 0x00);

        var root = new Grid
        {
            Width = Width,
            Background = new SolidColorBrush(dark ? Color.FromRgb(0x3A, 0x3F, 0x48) : Color.FromRgb(0x8A, 0x8F, 0x98)),
        };
        root.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, new FontFamily("Segoe UI"));
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TaskbarHeight) });

        // The tooltip Windows shows while the pointer rests on the icon.
        var tooltip = new Border
        {
            Background = new SolidColorBrush(dark ? Color.FromRgb(0x2C, 0x2C, 0x2C) : Color.FromRgb(0xF9, 0xF9, 0xF9)),
            BorderBrush = new SolidColorBrush(dark ? Color.FromRgb(0x45, 0x45, 0x45) : Color.FromRgb(0xD5, 0xD5, 0xD5)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 5, 8, 6),
            Margin = new Thickness(0, 24, 20, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock
            {
                Text = TrayIconSet.ReadingTooltip(readings[hovered], error: null),
                Foreground = new SolidColorBrush(text),
                FontSize = 12,
            },
        };
        root.Children.Add(tooltip);

        var taskbar = new DockPanel { Background = new SolidColorBrush(taskbarFill), LastChildFill = false };
        Grid.SetRow(taskbar, 1);
        root.Children.Add(taskbar);

        var clock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 14, 0) };
        clock.Children.Add(ClockLine("16:00", text));
        clock.Children.Add(ClockLine("23/09/2026", text));
        DockPanel.SetDock(clock, Dock.Right);
        taskbar.Children.Add(clock);

        // Docked right to left, so the last reading sits next to the clock.
        for (var i = readings.Count - 1; i >= 0; i--)
        {
            var icon = new Border
            {
                Width = 28,
                Height = 40,
                CornerRadius = new CornerRadius(4),
                Background = i == hovered ? new SolidColorBrush(hover) : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Image
                {
                    // Drawn at 2x so the 16-pixel icon stays sharp in the 2x preview.
                    Source = ToBitmapSource(TrayIconRenderer.DrawReadingBitmap(
                        readings[i].Row, readings[i].Number, new Drawing.Size(32, 32), !dark)),
                    Width = 16,
                    Height = 16,
                },
            };
            DockPanel.SetDock(icon, Dock.Right);
            taskbar.Children.Add(icon);
        }

        var overflow = new Shapes.Path
        {
            Data = Geometry.Parse("M0,5 L5,0 L10,5"),
            Stroke = new SolidColorBrush(text),
            StrokeThickness = 1.2,
            Width = 10,
            Height = 6,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(overflow, Dock.Right);
        taskbar.Children.Add(overflow);

        PreviewRenderer.SaveElement(root, Width, path);
    }

    private static TextBlock ClockLine(string value, Color color) => new()
    {
        Text = value,
        Foreground = new SolidColorBrush(color),
        FontSize = 12,
        HorizontalAlignment = HorizontalAlignment.Right,
    };

    private static BitmapSource ToBitmapSource(Drawing.Bitmap bitmap)
    {
        using (bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
