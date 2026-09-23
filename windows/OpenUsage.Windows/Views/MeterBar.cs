using System;
using System.Windows;
using System.Windows.Media;
using OpenUsage.Windows.Services;

namespace OpenUsage.Windows.Views;

/// <summary>A rounded usage meter: track, severity-colored fill, and the optional even-pace tick.</summary>
public sealed class MeterBar : FrameworkElement
{
    private readonly double _fraction;
    private readonly double? _tick;
    private readonly Brush _fill;

    public MeterBar(double fraction, double? tick, Brush fill)
    {
        _fraction = Math.Clamp(fraction, 0, 1);
        _tick = tick;
        _fill = fill;
        Height = 6;
        SnapsToDevicePixels = true;
    }

    protected override void OnRender(DrawingContext context)
    {
        var theme = Theme.Current;
        var width = ActualWidth;
        var height = ActualHeight;
        var radius = height / 2;
        context.DrawRoundedRectangle(theme.Track, null, new Rect(0, 0, width, height), radius, radius);
        if (_fraction > 0)
        {
            var fillWidth = Math.Max(height, width * _fraction);
            context.DrawRoundedRectangle(_fill, null, new Rect(0, 0, fillWidth, height), radius, radius);
        }
        if (_tick is double tick)
        {
            var x = Math.Clamp(tick, 0, 1) * width;
            var pen = new Pen(theme.Tick, 1.5);
            pen.Freeze();
            context.DrawLine(pen, new Point(x, -2), new Point(x, height + 2));
        }
    }
}

/// <summary>The Usage Trend row: one bar per day, oldest first, tallest day filling the height.</summary>
public sealed class TrendBars : FrameworkElement
{
    private readonly double[] _values;

    public TrendBars(double[] values)
    {
        _values = values;
        Height = 26;
        SnapsToDevicePixels = true;
    }

    protected override void OnRender(DrawingContext context)
    {
        if (_values.Length == 0)
        {
            return;
        }
        var theme = Theme.Current;
        var max = 0.0;
        foreach (var value in _values)
        {
            max = Math.Max(max, value);
        }
        var slot = ActualWidth / _values.Length;
        var barWidth = Math.Max(1, slot * 0.7);
        for (var index = 0; index < _values.Length; index++)
        {
            var share = max > 0 ? _values[index] / max : 0;
            var barHeight = Math.Max(1.5, share * ActualHeight);
            var x = index * slot + (slot - barWidth) / 2;
            var brush = share > 0 ? theme.Accent : theme.Track;
            context.DrawRoundedRectangle(brush, null,
                new Rect(x, ActualHeight - barHeight, barWidth, barHeight), 1, 1);
        }
    }
}
