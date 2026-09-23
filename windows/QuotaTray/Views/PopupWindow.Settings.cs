using System;
using System.Windows;
using System.Windows.Controls;
using QuotaTray.Services;

namespace QuotaTray.Views;

// The Settings screen: captioned sections over grouped cards of rows, like the Mac SettingsScreen.
// Providers get their own section here because Windows has no Customize screen.
public partial class PopupWindow
{
    private void BuildSettings(StackPanel body, Theme theme)
    {
        var general = Section("General", theme, body);
        general.Children.Add(SettingsRow("Show Total Spend", new ToggleSwitch(UiSettings.ShowTotalSpend, on =>
        {
            UiSettings.ShowTotalSpend = on;
        }), theme));
        general.Children.Add(SettingsRow("Launch at Login", new ToggleSwitch(SafeLaunchAtLogin(), _actions.SetLaunchAtLogin), theme));

        var display = Section("Usage Display", theme, body);
        var showRemaining = _dashboard?.MeterStyle != "used";
        FrameworkElement? picker = null;
        picker = PickerButton(showRemaining ? "Left" : "Used", theme, () => ShowMenu(picker!, above: false, new MenuItemSpec[]
        {
            new("Used", () => _actions.SetMeterStyle(false), IsChecked: !showRemaining),
            new("Left", () => _actions.SetMeterStyle(true), IsChecked: showRemaining),
        }));
        display.Children.Add(SettingsRow("Show Usage As", picker, theme));

        var providers = Section("Providers", theme, body);
        if (_dashboard == null)
        {
            providers.Children.Add(Text(_engineError ?? "Loading…", theme.TextSecondary, SupportingSize,
                margin: new Thickness(12, 9, 12, 9), wrap: true));
        }
        else
        {
            foreach (var provider in _dashboard.Providers)
            {
                var id = provider.Id;
                var label = new StackPanel { Orientation = Orientation.Horizontal };
                if (MarkIcon(id, theme.TextSecondary, 14, new Thickness(0, 0, 8, 0)) is { } mark)
                {
                    label.Children.Add(mark);
                }
                label.Children.Add(Text(provider.DisplayName, theme.TextPrimary, 13, verticalAlignment: VerticalAlignment.Center));
                providers.Children.Add(SettingsRow(label, new ToggleSwitch(provider.Enabled,
                    enabled => _actions.SetProviderEnabled(id, enabled)), theme));
            }
        }

        var advanced = Section("Advanced", theme, body);
        advanced.Children.Add(SettingsRow("Log Files",
            SmallButton(Text("Open Folder", theme.TextPrimary, SupportingSize, FontWeights.Medium), theme, _actions.OpenLogFolder), theme));

        // Credit where it's due: Quota Tray is an unofficial fork of OpenUsage (MIT).
        var about = Section("About", theme, body, last: true);
        var credit = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        credit.Children.Add(Text("Based on OpenUsage", theme.TextPrimary, 13));
        credit.Children.Add(Text("By Robin Ebers and contributors. Unofficial fork.", theme.TextSecondary, 11));
        about.Children.Add(SettingsRow(credit,
            SmallButton(Text("View Original", theme.TextPrimary, SupportingSize, FontWeights.Medium), theme,
                () => OpenUrl(OriginalProjectUrl)), theme));
    }

    private const string OriginalProjectUrl = "https://github.com/robinebers/openusage";

    /// <summary>A caption over a grouped card; returns the card's row stack.</summary>
    private static StackPanel Section(string title, Theme theme, StackPanel body, bool last = false)
    {
        var section = new StackPanel { Margin = new Thickness(0, 0, 0, last ? 0 : SectionSpacing) };
        section.Children.Add(Text(title, theme.TextSecondary, 11, FontWeights.SemiBold, margin: new Thickness(8, 0, 8, 0)));
        var rows = new StackPanel();
        var card = Card(rows, theme);
        card.Margin = new Thickness(0, HeaderToCard, 0, 0);
        section.Children.Add(card);
        body.Children.Add(section);
        return rows;
    }

    private static UIElement SettingsRow(string label, FrameworkElement control, Theme theme) =>
        SettingsRow(Text(label, theme.TextPrimary, 13, verticalAlignment: VerticalAlignment.Center), control, theme);

    private static UIElement SettingsRow(FrameworkElement label, FrameworkElement control, Theme theme)
    {
        _ = theme;
        var row = new DockPanel { Margin = new Thickness(12, 9, 12, 9), MinHeight = 20 };
        control.VerticalAlignment = VerticalAlignment.Center;
        control.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(control, Dock.Right);
        row.Children.Add(control);
        row.Children.Add(label);
        return row;
    }

    /// <summary>A trailing pop-up picker that hugs its selection (the Mac's menu-style Picker).</summary>
    private static Border PickerButton(string selection, Theme theme, Action onClick)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(Text(selection, theme.TextPrimary, SupportingSize, verticalAlignment: VerticalAlignment.Center));
        content.Children.Add(Glyph(Glyphs.ChevronUpDown, theme.TextSecondary, 9, stroke: 1.4, margin: new Thickness(6, 0, 0, 0)));
        return SmallButton(content, theme, onClick);
    }

    private static bool SafeLaunchAtLogin()
    {
        try
        {
            return LaunchAtLogin.IsEnabled;
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException)
        {
            AppLog.Warn($"launch at login read failed: {error.Message}");
            return false;
        }
    }
}
