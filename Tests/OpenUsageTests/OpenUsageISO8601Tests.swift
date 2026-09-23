import Foundation
import Testing
@testable import OpenUsage

struct OpenUsageISO8601Tests {
    @Test func parsesZuluISO() {
        let date = OpenUsageISO8601.date(from: "2099-01-01T00:00:00.000Z")
        #expect(date != nil)
    }

    @Test func normalizesMicrosecondsWithoutTimezoneLikeClaudeAPI() {
        let date = OpenUsageISO8601.date(from: "2099-01-01T00:00:00.123456")
        #expect(date != nil)
        #expect(OpenUsageISO8601.string(from: date!) == "2099-01-01T00:00:00.123Z")
    }

    @Test func normalizesSpaceSeparatedUTC() {
        let date = OpenUsageISO8601.date(from: "2099-01-01 00:00:00 UTC")
        #expect(date != nil)
    }

    /// Regression: the shared formatters crashed inside ICU when providers parsed dates from several
    /// threads at once on Linux and Windows, where Foundation's formatters aren't thread-safe.
    @Test func parsesConcurrentlyWithoutCrashing() {
        DispatchQueue.concurrentPerform(iterations: 2_000) { index in
            let value = index.isMultiple(of: 2) ? "2099-01-01T00:00:00.123Z" : "2099-01-01 00:00:00 UTC"
            guard let date = OpenUsageISO8601.date(from: value) else {
                Issue.record("could not parse \(value)")
                return
            }
            _ = OpenUsageISO8601.string(from: date)
        }
    }
}
