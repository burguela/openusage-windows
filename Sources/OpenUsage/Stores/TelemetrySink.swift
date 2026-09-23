import Foundation

/// The transport seam telemetry is emitted through. Abstracted from PostHog so the recorder's
/// daily-rollup/dedup logic can be unit-tested against a fake sink.
@MainActor
protocol TelemetrySink: AnyObject {
    func capture(_ event: String, _ properties: [String: Any])
    /// Mirror the optional-analytics preference without disabling daily activity or crash reporting.
    func setOptionalAnalyticsEnabled(_ enabled: Bool)
    func flush()
}
