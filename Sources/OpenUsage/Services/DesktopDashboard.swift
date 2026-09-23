import Foundation

/// `openusage.desktop.v1`: the display-ready dashboard the Windows tray app renders. Every string is
/// already formatted by the shared engine (`WidgetData`), so the tray app never re-implements
/// formatting, pacing, or reset-countdown rules. Times are ISO-8601; the tray app re-reads the
/// dashboard every minute, which keeps countdowns current.
struct DesktopDashboard: Encodable {
    let schema = "openusage.desktop.v1"
    let generatedAt: Date
    /// `left` or `used` — the global meter style the headlines were formatted with.
    let meterStyle: String
    let providers: [DesktopProvider]
}

struct DesktopProvider: Encodable {
    let id: String
    let displayName: String
    let enabled: Bool
    let plan: String?
    /// The current refresh error, or a soft warning from the last good snapshot (error wins).
    let notice: String?
    let isError: Bool
    /// "Outdated" once the displayed snapshot has missed refreshes; `nil` while current.
    let staleness: String?
    let refreshedAt: Date?
    let links: [DesktopLink]
    let rows: [DesktopRow]
}

struct DesktopLink: Encodable {
    let label: String
    let url: String
}

struct DesktopRow: Encodable {
    let id: String
    let title: String
    /// `meter` (bounded, with a bar), `value` (one right-aligned reading), or `chart` (Usage Trend).
    let kind: String
    let hasData: Bool
    /// True for On Demand rows, which the tray shows behind the provider's "Show more" toggle.
    let onDemand: Bool
    /// True for rows pinned to the menu bar on macOS; the tray icon tooltip lists these.
    let pinned: Bool
    /// Meter headline ("58% left") or the value row's reading ("$4.08 spent").
    let headline: String
    /// Meter trailing text: reset countdown, "Not started", limit context.
    let detail: String?
    /// Meter fill 0...1 in the current meter style.
    let fraction: Double?
    /// `normal`, `warning`, `critical`, or `none` (no data).
    let severity: String?
    /// Even-pace tick position 0...1 on the bar, when shown.
    let paceTick: Double?
    /// Short pace copy shown on the row ("~8% spare", "Limit in 3h 45m", "Limit reached").
    let paceNote: String?
    /// Longer hover copy for the pace state ("~35% left at reset").
    let paceTooltip: String?
    /// The compact reading used for the tray icon tooltip ("58%", "$4.08").
    let compactValue: String
    let chart: [DesktopChartPoint]?
    let note: String?
}

struct DesktopChartPoint: Encodable {
    let label: String
    let value: Double
    let readout: String
}

@MainActor
enum DesktopDashboardBuilder {
    static func build(session: EngineSession, now: Date) -> DesktopDashboard {
        let store = session.dataStore
        let allIDs = session.registry.providers.map(\.id)
        let enabledMetrics = Set(DefaultLayout.expandingAccounts(DefaultLayout.metricIDs, providerIDs: allIDs))
        let onDemandMetrics = Set(DefaultLayout.expandingAccounts(DefaultLayout.expandedMetricIDs, providerIDs: allIDs))
        let pinnedMetrics = Set(DefaultLayout.expandingAccounts(DefaultLayout.pinnedMetricIDs, providerIDs: allIDs))

        let providers = session.orderedProviderIDs.compactMap { id -> DesktopProvider? in
            guard let provider = session.registry.provider(id: id) else { return nil }
            let enabled = session.enablement.isEnabled(id)
            let error = store.errorMessage(for: id)
            let rows: [DesktopRow] = enabled
                ? session.registry.descriptors(for: id)
                    .filter { enabledMetrics.contains($0.id) }
                    .map { descriptor in
                        row(
                            descriptor: descriptor,
                            data: store.data(for: descriptor),
                            onDemand: onDemandMetrics.contains(descriptor.id),
                            pinned: pinnedMetrics.contains(descriptor.id),
                            now: now
                        )
                    }
                : []
            return DesktopProvider(
                id: id,
                displayName: provider.displayName,
                enabled: enabled,
                plan: enabled ? store.plan(for: id) : nil,
                notice: enabled ? store.headerNotice(for: id) : nil,
                isError: enabled && error != nil,
                staleness: enabled ? store.stalenessHint(for: id)?.tooltip : nil,
                refreshedAt: enabled ? store.snapshots[id]?.refreshedAt : nil,
                links: provider.visibleLinks.map { DesktopLink(label: $0.label, url: $0.url) },
                rows: Self.promotingAllOnDemand(rows)
            )
        }
        return DesktopDashboard(
            generatedAt: now,
            meterStyle: store.meterStyle == .remaining ? "left" : "used",
            providers: providers
        )
    }

    /// A provider always keeps at least one Always Visible row: when every row is On Demand, all of
    /// them show (the same rule the Mac dashboard applies).
    private static func promotingAllOnDemand(_ rows: [DesktopRow]) -> [DesktopRow] {
        guard !rows.isEmpty, rows.allSatisfy(\.onDemand) else { return rows }
        return rows.map { $0.withOnDemand(false) }
    }

    private static func row(
        descriptor: WidgetDescriptor,
        data: WidgetData,
        onDemand: Bool,
        pinned: Bool,
        now: Date
    ) -> DesktopRow {
        if data.isChart {
            return DesktopRow(
                id: descriptor.id,
                title: descriptor.title,
                kind: "chart",
                hasData: data.hasData,
                onDemand: onDemand,
                pinned: pinned,
                headline: data.hasData ? "" : WidgetData.noDataSubtitle,
                detail: nil,
                fraction: nil,
                severity: nil,
                paceTick: nil,
                paceNote: nil,
                paceTooltip: nil,
                compactValue: "",
                chart: data.chartPoints.map { DesktopChartPoint(label: $0.label, value: $0.value, readout: $0.readout) },
                note: data.chartNote
            )
        }
        if data.isBounded {
            let state = data.meterState(now: now)
            return DesktopRow(
                id: descriptor.id,
                title: descriptor.title,
                kind: "meter",
                hasData: data.hasData,
                onDemand: onDemand,
                pinned: pinned,
                headline: data.headline,
                detail: data.boundedTrailingText(now: now),
                fraction: data.hasData ? data.fraction : nil,
                severity: severityName(state.severity),
                paceTick: data.paceTick(for: state, now: now),
                paceNote: paceNote(state, alwaysShowPacing: data.alwaysShowPacing),
                paceTooltip: state.tooltip,
                compactValue: data.menuBarValue,
                chart: nil,
                note: data.infoNote
            )
        }
        return DesktopRow(
            id: descriptor.id,
            title: descriptor.title,
            kind: "value",
            hasData: data.hasData,
            onDemand: onDemand,
            pinned: pinned,
            headline: data.unboundedDetail,
            detail: nil,
            fraction: nil,
            severity: nil,
            paceTick: nil,
            paceNote: nil,
            paceTooltip: nil,
            compactValue: data.menuBarValue,
            chart: nil,
            note: data.infoNote ?? data.valueTooltipNote
        )
    }

    private static func severityName(_ severity: WidgetData.MeterSeverity?) -> String {
        switch severity {
        case .none: "none"
        case .normal: "normal"
        case .warning: "warning"
        case .critical: "critical"
        }
    }

    private static func paceNote(_ state: WidgetData.MeterState, alwaysShowPacing: Bool) -> String? {
        switch state {
        case .spent: "Limit reached"
        case .runningOut(let eta, _): eta
        case .closeToLimit(let spare, _): spare
        case .healthy: alwaysShowPacing ? state.tooltip : nil
        case .noData, .level: nil
        }
    }
}

private extension DesktopRow {
    func withOnDemand(_ onDemand: Bool) -> DesktopRow {
        DesktopRow(
            id: id, title: title, kind: kind, hasData: hasData, onDemand: onDemand, pinned: pinned,
            headline: headline, detail: detail, fraction: fraction, severity: severity,
            paceTick: paceTick, paceNote: paceNote, paceTooltip: paceTooltip,
            compactValue: compactValue, chart: chart, note: note
        )
    }
}
