using System;
using System.Collections.Generic;

namespace OpenUsage.Windows.Engine;

// The `openusage.desktop.v1` document printed by `openusage dashboard` (see
// Sources/OpenUsage/Services/DesktopDashboard.swift). Every string arrives formatted by the shared
// engine; this app only lays it out.

public sealed class Dashboard
{
    public string Schema { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; }
    public string AppVersion { get; set; } = "";
    /// <summary>"left" or "used": the meter style the headlines were formatted with.</summary>
    public string MeterStyle { get; set; } = "left";
    /// <summary>The Total Spend ring; null when no enabled provider tracks spend.</summary>
    public TotalSpendInfo? TotalSpend { get; set; }
    public List<ProviderInfo> Providers { get; set; } = new();
}

public sealed class ProviderInfo
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Enabled { get; set; }
    public string? Plan { get; set; }
    public string? Notice { get; set; }
    public bool IsError { get; set; }
    public string? Staleness { get; set; }
    public DateTimeOffset? RefreshedAt { get; set; }
    public List<LinkInfo> Links { get; set; } = new();
    public List<RowInfo> Rows { get; set; } = new();
}

public sealed class LinkInfo
{
    public string Label { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class RowInfo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>"meter", "value", or "chart".</summary>
    public string Kind { get; set; } = "value";
    public bool HasData { get; set; }
    public bool OnDemand { get; set; }
    public bool Pinned { get; set; }
    public string Headline { get; set; } = "";
    public string? Detail { get; set; }
    public double? Fraction { get; set; }
    /// <summary>"normal", "warning", "critical", or "none".</summary>
    public string? Severity { get; set; }
    public double? PaceTick { get; set; }
    public string? PaceNote { get; set; }
    /// <summary>True when the pace note carries the flame (limit reached or projected to run out).</summary>
    public bool PaceFlame { get; set; }
    public string? PaceTooltip { get; set; }
    public string CompactValue { get; set; } = "";
    public List<ChartPoint>? Chart { get; set; }
    public string? Note { get; set; }
    /// <summary>A value row's secondary line under its reading.</summary>
    public string? Subtitle { get; set; }
}

public sealed class TotalSpendInfo
{
    /// <summary>Display names of the providers the total can include.</summary>
    public List<string> Providers { get; set; } = new();
    public List<SpendPeriod> Periods { get; set; } = new();
}

public sealed class SpendPeriod
{
    /// <summary>"today", "yesterday", or "last30".</summary>
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Center { get; set; } = "";
    public string Unit { get; set; } = "";
    public List<SpendSlice> Slices { get; set; } = new();
}

public sealed class SpendSlice
{
    public string ProviderId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public double Amount { get; set; }
    public string Value { get; set; } = "";
}

public sealed class ChartPoint
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
    public string Readout { get; set; } = "";
}
