using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace OpenUsage.Windows.Services;

/// <summary>
/// Provider marks (the same vector artwork the Mac app draws), generated into
/// <c>Assets/provider-marks.json</c> by <c>windows/scripts/generate_assets.py</c>.
/// </summary>
public static class ProviderMarks
{
    private static readonly Dictionary<string, Geometry> Cache = Load();

    /// <summary>The mark for a provider or account card id ("claude", "claude:work"), or null.</summary>
    public static Geometry? For(string providerId)
    {
        var family = providerId.Split(':', 2)[0];
        return Cache.TryGetValue(family, out var geometry) ? geometry : null;
    }

    private static Dictionary<string, Geometry> Load()
    {
        var marks = new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("provider-marks.json");
        if (stream == null)
        {
            AppLog.Error("provider-marks.json is missing from the app resources");
            return marks;
        }
        using var document = JsonDocument.Parse(stream);
        foreach (var mark in document.RootElement.EnumerateObject())
        {
            try
            {
                var group = new GeometryGroup { FillRule = FillRule.Nonzero };
                foreach (var path in mark.Value.GetProperty("paths").EnumerateArray())
                {
                    var data = path.GetProperty("data").GetString() ?? "";
                    var evenOdd = path.GetProperty("evenOdd").GetBoolean();
                    group.Children.Add(Geometry.Parse((evenOdd ? "F0 " : "F1 ") + data));
                }
                group.Freeze();
                marks[mark.Name] = group;
            }
            catch (FormatException error)
            {
                AppLog.Error($"provider mark '{mark.Name}' could not be parsed: {error.Message}");
            }
        }
        return marks;
    }
}
