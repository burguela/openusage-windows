import Foundation

/// A formatter built once and shared across threads. Apple's Foundation formatters are thread-safe
/// for parsing and formatting; swift-corelibs-foundation's (Windows, Linux) are not — concurrent
/// parses crash inside ICU — so off Apple platforms every use takes a lock.
final class SharedFormatter<F: Formatter>: @unchecked Sendable {
    private let formatter: F
    #if !canImport(Darwin)
    private let lock = NSLock()
    #endif

    init(_ formatter: F) {
        self.formatter = formatter
    }

    func with<T>(_ body: (F) throws -> T) rethrows -> T {
        #if !canImport(Darwin)
        lock.lock()
        defer { lock.unlock() }
        #endif
        return try body(formatter)
    }
}
