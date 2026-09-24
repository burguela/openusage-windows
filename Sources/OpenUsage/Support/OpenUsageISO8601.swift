import Foundation

/// Shared ISO-8601 date parsing/formatting used by multiple providers and the local API. Normalizes
/// the various timestamp shapes providers return (space-separated, " UTC" suffix, variable fractional
/// digits) before parsing.
enum OpenUsageISO8601 {
    static func string(from date: Date) -> String {
        fractionalFormatter.with { $0.string(from: date) }
    }

    static func date(from value: String) -> Date? {
        if let date = fastDate(from: value) { return date }
        let normalized = normalizeTimestamp(value)
        return fractionalFormatter.with { $0.date(from: normalized) } ??
        plainFormatter.with { $0.date(from: normalized) }
    }

    /// The shape nearly every timestamp takes (`2026-02-20T16:00:00.123Z`, or with a `±HH:MM` offset),
    /// read directly. The regex normalization and formatter below cost ~90µs a call on Linux and Windows,
    /// which dominated a cold read of a large Claude history. Like that path, fractional seconds are cut
    /// to milliseconds. Any other shape, or an out-of-range field, returns `nil` for the full path.
    static func fastDate(from value: String) -> Date? {
        let bytes = Array(value.utf8)
        guard bytes.count >= 20, bytes[4] == UInt8(ascii: "-"), bytes[7] == UInt8(ascii: "-"),
              bytes[10] == UInt8(ascii: "T"), bytes[13] == UInt8(ascii: ":"), bytes[16] == UInt8(ascii: ":")
        else { return nil }
        func number(_ start: Int, _ length: Int) -> Int? {
            var result = 0
            for index in start..<(start + length) {
                let digit = Int(bytes[index]) - Int(UInt8(ascii: "0"))
                guard (0...9).contains(digit) else { return nil }
                result = result * 10 + digit
            }
            return result
        }
        guard let year = number(0, 4), year >= 1, let month = number(5, 2), let day = number(8, 2),
              let hour = number(11, 2), let minute = number(14, 2), let second = number(17, 2),
              (1...12).contains(month), (1...daysIn(month: month, year: year)).contains(day),
              hour < 24, minute < 60, second < 60
        else { return nil }

        var index = 19
        var milliseconds = 0
        if bytes[index] == UInt8(ascii: ".") {
            index += 1
            let digitsStart = index
            while index < bytes.count, (UInt8(ascii: "0")...UInt8(ascii: "9")).contains(bytes[index]) {
                if index - digitsStart < 3 {
                    milliseconds = milliseconds * 10 + Int(bytes[index] - UInt8(ascii: "0"))
                }
                index += 1
            }
            guard index > digitsStart else { return nil }
            for _ in min(index - digitsStart, 3)..<3 { milliseconds *= 10 }
        }

        var offsetSeconds = 0
        guard index < bytes.count else { return nil }
        if bytes[index] == UInt8(ascii: "Z") {
            index += 1
        } else if bytes[index] == UInt8(ascii: "+") || bytes[index] == UInt8(ascii: "-") {
            guard index + 6 == bytes.count, bytes[index + 3] == UInt8(ascii: ":"),
                  let offsetHours = number(index + 1, 2), let offsetMinutes = number(index + 4, 2),
                  offsetHours < 24, offsetMinutes < 60
            else { return nil }
            offsetSeconds = (offsetHours * 3600 + offsetMinutes * 60) * (bytes[index] == UInt8(ascii: "-") ? -1 : 1)
            index += 6
        } else {
            return nil
        }
        guard index == bytes.count else { return nil }

        // Days since 1970-01-01 in the proleptic Gregorian calendar (Howard Hinnant's days_from_civil).
        let shiftedYear = month <= 2 ? year - 1 : year
        let era = shiftedYear / 400
        let yearOfEra = shiftedYear - era * 400
        let dayOfYear = (153 * ((month + 9) % 12) + 2) / 5 + day - 1
        let dayOfEra = yearOfEra * 365 + yearOfEra / 4 - yearOfEra / 100 + dayOfYear
        let days = era * 146_097 + dayOfEra - 719_468
        let seconds = days * 86_400 + hour * 3600 + minute * 60 + second - offsetSeconds
        return Date(timeIntervalSince1970: Double(seconds) + Double(milliseconds) / 1000)
    }

    private static func daysIn(month: Int, year: Int) -> Int {
        switch month {
        case 2: (year % 4 == 0 && year % 100 != 0) || year % 400 == 0 ? 29 : 28
        case 4, 6, 9, 11: 30
        default: 31
        }
    }

    /// Aligns with the JavaScript plugin `ctx.util.toIso` string normalization (Claude `resets_at`, etc.).
    private static func normalizeTimestamp(_ raw: String) -> String {
        var s = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !s.isEmpty else { return s }

        if s.contains(" "),
           let range = s.range(of: #"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}"#, options: .regularExpression) {
            s.replaceSubrange(range, with: s[range].replacingOccurrences(of: " ", with: "T"))
        }
        if s.hasSuffix(" UTC") {
            s = String(s.dropLast(4)) + "Z"
        }

        if let match = s.range(of: #"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(\.\d+)?(Z|[+-]\d{2}:\d{2})$"#, options: .regularExpression) {
            let matched = String(s[match])
            return normalizeFractionalISO(matched, assumeUTC: false)
        }
        if let match = s.range(of: #"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(\.\d+)?$"#, options: .regularExpression) {
            let matched = String(s[match])
            return normalizeFractionalISO(matched, assumeUTC: true)
        }

        return s
    }

    private static func normalizeFractionalISO(_ value: String, assumeUTC: Bool) -> String {
        let pattern = #"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(\.\d+)?(Z|[+-]\d{2}:\d{2})?$"#
        guard let regex = try? NSRegularExpression(pattern: pattern),
              let match = regex.firstMatch(in: value, range: NSRange(value.startIndex..., in: value)),
              match.numberOfRanges >= 2,
              let headRange = Range(match.range(at: 1), in: value)
        else {
            return assumeUTC && !value.hasSuffix("Z") ? value + "Z" : value
        }

        let head = String(value[headRange])
        var frac = ""
        if match.numberOfRanges > 2, match.range(at: 2).location != NSNotFound,
           let fracRange = Range(match.range(at: 2), in: value) {
            var digits = String(value[fracRange]).dropFirst()
            if digits.count > 3 {
                digits = digits.prefix(3)
            }
            while digits.count < 3 {
                digits.append("0")
            }
            frac = ".\(digits)"
        }

        var tz = "Z"
        if !assumeUTC, match.numberOfRanges > 3, match.range(at: 3).location != NSNotFound,
           let tzRange = Range(match.range(at: 3), in: value) {
            tz = String(value[tzRange])
        }

        return head + frac + tz
    }

    // ISO8601DateFormatter is expensive to construct and is hit on every snapshot decode and local-API
    // encode, so the two fixed configurations are built once and shared (`SharedFormatter` serializes
    // them where Foundation's formatters aren't thread-safe).
    private static let fractionalFormatter = SharedFormatter(makeFormatter(fractionalSeconds: true))
    private static let plainFormatter = SharedFormatter(makeFormatter(fractionalSeconds: false))

    private static func makeFormatter(fractionalSeconds: Bool) -> ISO8601DateFormatter {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = fractionalSeconds
            ? [.withInternetDateTime, .withFractionalSeconds]
            : [.withInternetDateTime]
        return formatter
    }
}
