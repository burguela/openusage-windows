#if !canImport(os)
import Foundation

// Minimal stand-ins for the two `os` APIs the engine uses, for platforms without Apple's `os`
// module (Windows, Linux). They keep the call sites identical to macOS.

/// `OSAllocatedUnfairLock` equivalent built on `NSLock`.
final class OSAllocatedUnfairLock<State>: @unchecked Sendable {
    private let lock = NSLock()
    private var state: State

    init(initialState: State) {
        state = initialState
    }

    func withLock<Result>(_ body: (inout State) throws -> Result) rethrows -> Result {
        lock.lock()
        defer { lock.unlock() }
        return try body(&state)
    }
}

enum OSLogType {
    case `default`, info, debug, error, fault
}

enum OSLogPrivacy {
    case `public`, `private`
}

/// Accepts the `"\(value, privacy: .public)"` interpolations written for `os.Logger`.
struct OSLogMessage: ExpressibleByStringInterpolation {
    struct StringInterpolation: StringInterpolationProtocol {
        var text = ""
        init(literalCapacity: Int, interpolationCount: Int) {}
        mutating func appendLiteral(_ literal: String) { text += literal }
        mutating func appendInterpolation(_ value: some CustomStringConvertible, privacy: OSLogPrivacy = .private) {
            text += value.description
        }
    }

    let text: String
    init(stringLiteral value: String) { text = value }
    init(stringInterpolation: StringInterpolation) { text = stringInterpolation.text }
}

/// `os.Logger` stand-in. There is no unified system log to feed here — `AppLog`'s file sink is the
/// log of record — so only errors are echoed, to stderr, where the Windows tray app surfaces them.
struct Logger: Sendable {
    let subsystem: String
    let category: String

    init(subsystem: String, category: String) {
        self.subsystem = subsystem
        self.category = category
    }

    func log(level: OSLogType, _ message: OSLogMessage) {
        guard level == .error || level == .fault else { return }
        FileHandle.standardError.write(Data("[\(subsystem):\(category)] \(message.text)\n".utf8))
    }

    func error(_ message: OSLogMessage) {
        log(level: .error, message)
    }
}
#endif
