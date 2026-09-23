import Foundation

/// Bounds a provider's optional local-log scan so its live limits still publish when the history is slow
/// to read. The first scan of a large history (or a slow disk) can outlast the whole provider deadline,
/// and a timed-out refresh publishes nothing at all, not even the limits the API already returned. Past
/// the budget the scan is cancelled; the files it finished stay in the parse cache, so the next refresh
/// resumes it, and the store keeps the last good history meanwhile.
enum LocalUsageScanBudget {
    /// Half the provider deadline (`WidgetDataStore.providerRefreshTimeout`, 120s), which leaves the rest
    /// for the live requests and pricing.
    static let standard: Duration = .seconds(60)

    private enum Outcome<Value: Sendable>: Sendable {
        case finished(Value)
        case overBudget
    }

    /// The scan's result, or `nil` when `budget` elapsed first (or the caller was cancelled).
    static func run<Value: Sendable>(
        budget: Duration,
        providerID: String,
        _ scan: @escaping @Sendable () async -> Value
    ) async -> Value? {
        let outcome = await withTaskGroup(of: Outcome<Value>.self) { group in
            group.addTask { .finished(await scan()) }
            group.addTask {
                try? await Task.sleep(for: budget)
                return .overBudget
            }
            let first = await group.next() ?? .overBudget
            group.cancelAll()
            return first
        }
        switch outcome {
        case let .finished(value):
            return value
        case .overBudget:
            if !Task.isCancelled {
                AppLog.warn(
                    LogTag.plugin(providerID),
                    "local usage history not read within \(budget); showing live limits now, "
                        + "the history scan resumes on the next refresh"
                )
            }
            return nil
        }
    }
}
