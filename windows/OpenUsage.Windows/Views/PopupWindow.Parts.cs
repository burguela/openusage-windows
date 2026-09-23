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

// The popup's Settings screen and the small element builders both screens share.
public partial class PopupWindow
{
    private void BuildSettings(StackPanel body, Theme theme)
    {
        body.Children.Add(SectionTitle("Providers", theme));
        if (_dashboard == null)
        {
            body.Children.Add(Text(_engineError ?? "Loading…", theme.TextSecondary, 12, margin: new Thickness(16, 0, 16, 8), wrap: true));
        }
        else
        {
            foreach (var provider in _dashboard.Providers)
            {
                var id = provider.Id;
                body.Children.Add(Toggle(provider.DisplayName, provider.Enabled, theme,
                    enabled => _actions.SetProviderEnabled(id, enabled)));
            }
        }

        body.Children.Add(SectionTitle("Show Usage As", theme));
        var showRemaining = _dashboard?.MeterStyle != "used";
        var styles = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 16, 4) };
        styles.Children.Add(Choice("Left", showRemaining, theme, () => _actions.SetMeterStyle(true)));
        styles.Children.Add(Choice("Used", !showRemaining, theme, () => _actions.SetMeterStyle(false)));
        body.Children.Add(styles);

        body.Children.Add(SectionTitle("General", theme));
        body.Children.Add(Toggle("Launch at Login", LaunchAtLogin.IsEnabled, theme, _actions.SetLaunchAtLogin));
        var logs = LinkButton("Open Log Folder", theme, _actions.OpenLogFolder, fontSize: 12);
        logs.Margin = new Thickness(10, 2, 16, 8);
        body.Children.Add(logs);
    }

    // MARK: - Small builders

    private static TextBlock Text(
        string text,
        Brush brush,
        double size,
        FontWeight? weight = null,
        Thickness? margin = null,
        VerticalAlignment verticalAlignment = VerticalAlignment.Top,
        bool wrap = false,
        bool trim = false) => new()
        {
            Text = text,
            Foreground = brush,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Margin = margin ?? new Thickness(0),
            VerticalAlignment = verticalAlignment,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
        };

    private static UIElement Notice(string message, Brush accent, Theme theme, Thickness margin)
    {
        var border = new Border
        {
            BorderBrush = accent,
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(8, 2, 0, 2),
            Margin = margin,
        };
        border.Child = Text(message, theme.TextPrimary, 12, wrap: true);
        return border;
    }

    private static UIElement SectionTitle(string title, Theme theme) =>
        Text(title, theme.TextSecondary, 12, FontWeights.SemiBold, new Thickness(16, 10, 16, 4));

    private static FrameworkElement MarkIcon(Geometry geometry, Brush brush, double size, Thickness margin) => new System.Windows.Shapes.Path
    {
        Data = geometry,
        Fill = brush,
        Stretch = Stretch.Uniform,
        Width = size,
        Height = size,
        Margin = margin,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>A flat text button in the panel's style (no system button chrome).</summary>
    private static FrameworkElement LinkButton(string label, Theme theme, Action onClick, double fontSize = 12, bool enabled = true)
    {
        var text = Text(label, enabled ? theme.Accent : theme.TextSecondary, fontSize);
        var border = new Border
        {
            Child = text,
            Padding = new Thickness(6, 3, 6, 3),
            CornerRadius = new CornerRadius(5),
            Background = Brushes.Transparent,
            Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = enabled,
        };
        if (enabled)
        {
            border.MouseEnter += (_, _) => border.Background = theme.Hover;
            border.MouseLeave += (_, _) => border.Background = Brushes.Transparent;
            border.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            border.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space)
                {
                    e.Handled = true;
                    onClick();
                }
            };
        }
        return border;
    }

    private static UIElement Toggle(string label, bool isOn, Theme theme, Action<bool> onChange)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = isOn,
            Foreground = theme.TextPrimary,
            Margin = new Thickness(16, 3, 16, 3),
            FontSize = 13,
        };
        box.Click += (_, _) => onChange(box.IsChecked == true);
        return box;
    }

    private static UIElement Choice(string label, bool isOn, Theme theme, Action onSelect)
    {
        var button = new RadioButton
        {
            Content = label,
            IsChecked = isOn,
            GroupName = "meterStyle",
            Foreground = theme.TextPrimary,
            Margin = new Thickness(0, 3, 16, 3),
            FontSize = 13,
        };
        button.Checked += (_, _) =>
        {
            if (!isOn)
            {
                onSelect();
            }
        };
        return button;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AppLog.Error($"could not open link: {error.Message}");
            MessageBox.Show($"OpenUsage couldn't open {url}.", "OpenUsage", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
