using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using QuotaTray.Engine;

namespace QuotaTray.Tray;

/// <summary>A pinned reading and the provider it belongs to.</summary>
public sealed record TrayReading(ProviderInfo Provider, RowInfo Row, string Number);

/// <summary>
/// Draws the notification-area icons, the Windows counterpart of the Mac menu-bar strip. Text style
/// draws one icon per pinned reading with its number; Bars style draws up to two mini meters in one
/// icon. Without data the app icon shows.
/// </summary>
public static class TrayIconRenderer
{
    /// <summary>Icons the Text style shows at most, so the taskbar doesn't fill up.</summary>
    public const int MaxReadings = 4;

    public static Icon LoadAppIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("QuotaTray.ico")
            ?? throw new InvalidOperationException("The embedded app icon is missing.");
        return new Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
    }

    /// <summary>The meters the Bars icon draws: the first two pinned meter rows that have data.</summary>
    public static IReadOnlyList<RowInfo> IconMeters(Dashboard? dashboard) =>
        dashboard == null
            ? Array.Empty<RowInfo>()
            : dashboard.Providers
                .Where(p => p.Enabled)
                .SelectMany(p => p.Rows)
                .Where(r => r.Pinned && r.Kind == "meter" && r.HasData && r.Fraction.HasValue)
                .Take(2)
                .ToList();

    /// <summary>
    /// The readings the Text style draws, in dashboard order: pinned rows with data whose value fits
    /// a small square ("58%" draws as 58). Longer values such as "$49.85" stay in the hover text.
    /// </summary>
    public static IReadOnlyList<TrayReading> Readings(Dashboard? dashboard) =>
        dashboard == null
            ? Array.Empty<TrayReading>()
            : dashboard.Providers
                .Where(p => p.Enabled)
                .SelectMany(p => p.Rows.Select(r => (Provider: p, Row: r)))
                .Where(x => x.Row.Pinned && x.Row.HasData)
                .Select(x => new TrayReading(x.Provider, x.Row, NumberText(x.Row.CompactValue) ?? ""))
                .Where(x => x.Number.Length > 0)
                .Take(MaxReadings)
                .ToList();

    /// <summary>"58%" → "58"; null when the value isn't a number of up to three digits.</summary>
    internal static string? NumberText(string compactValue)
    {
        var number = compactValue.Trim().TrimEnd('%');
        return number.Length is > 0 and <= 3 && number.All(char.IsAsciiDigit) ? number : null;
    }

    public static Icon DrawMeters(IReadOnlyList<RowInfo> meters, bool lightTaskbar)
    {
        using var bitmap = DrawMetersBitmap(meters, System.Windows.Forms.SystemInformation.SmallIconSize, lightTaskbar);
        return ToIcon(bitmap);
    }

    public static Icon DrawReading(TrayReading reading, bool lightTaskbar)
    {
        using var bitmap = DrawReadingBitmap(reading.Row, reading.Number,
            System.Windows.Forms.SystemInformation.SmallIconSize, lightTaskbar);
        return ToIcon(bitmap);
    }

    /// <summary>The meters icon as a bitmap of <paramref name="size"/> (also used by the previews).</summary>
    internal static Bitmap DrawMetersBitmap(IReadOnlyList<RowInfo> meters, Size size, bool lightTaskbar)
    {
        var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var scale = size.Height / 16f;
            var barHeight = (meters.Count == 1 ? 6f : 5f) * scale;
            var gap = 2f * scale;
            var total = meters.Count * barHeight + (meters.Count - 1) * gap;
            var top = (size.Height - total) / 2f;
            var inset = 1f * scale;

            foreach (var meter in meters)
            {
                DrawBar(graphics, meter, new RectangleF(inset, top, size.Width - 2 * inset, barHeight), lightTaskbar);
                top += barHeight + gap;
            }
        }
        return bitmap;
    }

    /// <summary>
    /// One reading as a bitmap of <paramref name="size"/>: the number as large as the square allows,
    /// with a thin meter under it when the reading has a limit (also used by the previews).
    /// </summary>
    internal static Bitmap DrawReadingBitmap(RowInfo row, string number, Size size, bool lightTaskbar)
    {
        var bitmap = new Bitmap(size.Width, size.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var scale = size.Height / 16f;
        var hasBar = row.Fraction.HasValue;
        var barHeight = 2f * scale;
        var textBox = new RectangleF(0, 0, size.Width, size.Height - (hasBar ? barHeight + 1.5f * scale : 2f * scale));

        using var family = new FontFamily("Segoe UI");
        using var path = new GraphicsPath();
        path.AddString(number, family, (int)FontStyle.Bold, 100f, PointF.Empty, StringFormat.GenericTypographic);
        var bounds = path.GetBounds();
        var fit = Math.Min(textBox.Width / bounds.Width, textBox.Height / bounds.Height);
        using (var matrix = new Matrix())
        {
            matrix.Translate(
                textBox.Left + (textBox.Width - bounds.Width * fit) / 2f,
                textBox.Top + (textBox.Height - bounds.Height * fit) / 2f);
            matrix.Scale(fit, fit);
            matrix.Translate(-bounds.Left, -bounds.Top);
            path.Transform(matrix);
        }
        using (var textBrush = new SolidBrush(FillColor(row.Severity, lightTaskbar)))
        {
            graphics.FillPath(textBrush, path);
        }

        if (hasBar)
        {
            DrawBar(graphics, row, new RectangleF(0, size.Height - barHeight, size.Width, barHeight), lightTaskbar);
        }
        return bitmap;
    }

    private static void DrawBar(Graphics graphics, RowInfo meter, RectangleF rect, bool lightTaskbar)
    {
        var track = lightTaskbar ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(90, 255, 255, 255);
        using (var trackBrush = new SolidBrush(track))
        {
            FillRounded(graphics, trackBrush, rect);
        }
        var fraction = Math.Clamp(meter.Fraction ?? 0, 0, 1);
        if (fraction > 0)
        {
            using var fillBrush = new SolidBrush(FillColor(meter.Severity, lightTaskbar));
            FillRounded(graphics, fillBrush, rect with { Width = Math.Max(rect.Height, (float)(rect.Width * fraction)) });
        }
    }

    private static Color FillColor(string? severity, bool lightTaskbar) => severity switch
    {
        "warning" => Color.FromArgb(0xFF, 0xCC, 0x00),
        "critical" => Color.FromArgb(0xFF, 0x3B, 0x30),
        _ => lightTaskbar ? Color.FromArgb(0x1B, 0x1C, 0x1F) : Color.White,
    };

    private static void FillRounded(Graphics graphics, Brush brush, RectangleF rect)
    {
        var radius = rect.Height / 2f;
        using var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, radius * 2, rect.Height, 90, 180);
        path.AddArc(rect.Right - radius * 2, rect.Top, radius * 2, rect.Height, 270, 180);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    public static bool TaskbarUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        // SystemUsesLightTheme covers the taskbar; missing (Windows 10 before 1903) means dark.
        return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
    }

    private static Icon ToIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            // Icon.FromHandle doesn't own the handle; clone it so the HICON can be freed right away.
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
