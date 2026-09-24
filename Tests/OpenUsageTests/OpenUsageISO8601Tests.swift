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

    /// The fast path must read exactly what the formatter reads, since cached history mixes both.
    @Test func fastPathMatchesTheFormatter() throws {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        var generator = SystemRandomNumberGenerator()
        for _ in 0..<2_000 {
            let seconds = Int.random(in: 0..<4_102_444_800, using: &generator)
            let millis = Int.random(in: 0..<1000, using: &generator)
            let base = Date(timeIntervalSince1970: TimeInterval(seconds) + TimeInterval(millis) / 1000)
            let text = formatter.string(from: base)
            let expected = try #require(formatter.date(from: text))
            #expect(OpenUsageISO8601.fastDate(from: text) == expected, "\(text)")
        }
        let cases: [(String, String)] = [
            ("2024-02-29T23:59:59.9Z", "2024-02-29T23:59:59.900Z"),
            ("2026-09-23T21:31:19.771234Z", "2026-09-23T21:31:19.771Z"),
            ("2026-09-23T21:31:19Z", "2026-09-23T21:31:19.000Z"),
            ("2026-09-23T18:31:19.5-03:00", "2026-09-23T21:31:19.500Z"),
            ("2026-09-24T03:01:19.000+05:30", "2026-09-23T21:31:19.000Z"),
        ]
        for (input, utc) in cases {
            #expect(OpenUsageISO8601.fastDate(from: input) == formatter.date(from: utc), "\(input)")
        }
    }

    @Test func fastPathLeavesOtherShapesToTheFormatter() {
        for value in ["2026-02-30T00:00:00Z", "2026-09-23 21:31:19 UTC", "2026-09-23T21:31:19",
                      "2026-09-23T24:00:00Z", "2026-09-23T21:31:19.Z", "not a date"] {
            #expect(OpenUsageISO8601.fastDate(from: value) == nil, "\(value)")
        }
        #expect(OpenUsageISO8601.date(from: "2026-09-23T21:31:19") != nil)
    }
}
