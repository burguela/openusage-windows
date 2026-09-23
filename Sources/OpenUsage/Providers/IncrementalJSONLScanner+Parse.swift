import Foundation

extension IncrementalJSONLScanner {
    /// Read + parse a bounded number of changed files in parallel. Results are keyed back to the input
    /// order; a `nil` item list marks an unreadable file.
    static func parseFiles<State: Sendable>(
        _ files: [JSONLScanning.DiscoveredFile],
        maxConcurrentParses: Int,
        permitPool: JSONLParsePermitPool,
        initialState: State,
        parse: @Sendable @escaping (Data, inout State) -> [Item]?
    ) async -> [ParseResult] {
        await withTaskGroup(of: (Int, [Item]?, Bool, Int).self, returning: [ParseResult].self) { group in
            func addTask(at index: Int) {
                let file = files[index]
                group.addTask {
                    guard await permitPool.acquire() else { return (index, nil, false, 0) }
                    let result: (Int, [Item]?, Bool, Int)
                    if Task.isCancelled || !FileManager.default.fileExists(atPath: file.path) {
                        result = (index, nil, false, 0)
                    } else {
                        do {
                            let streamed = try JSONLStreamingReader.read(
                                path: file.path,
                                initialState: initialState,
                                parse: parse
                            )
                            result = (index, streamed.items, false, streamed.oversizedRecordCount)
                        } catch is CancellationError {
                            result = (index, nil, false, 0)
                        } catch {
                            result = (index, nil, true, 0)
                        }
                    }
                    await permitPool.release()
                    return result
                }
            }

            var nextIndex = 0
            let initialCount = min(maxConcurrentParses, files.count)
            for index in 0..<initialCount where !Task.isCancelled {
                addTask(at: index)
                nextIndex += 1
            }

            var results = files.map {
                (file: $0, items: Optional<[Item]>.none, readFailed: false, oversizedRecordCount: 0)
            }
            for await (index, items, readFailed, oversizedRecordCount) in group {
                // Record before checking cancellation: a file that finished is complete either way, and
                // a cancelled scan still caches it (`keepFinishedFiles`).
                results[index] = (files[index], items, readFailed, oversizedRecordCount)
                if Task.isCancelled {
                    group.cancelAll()
                    break
                }
                if nextIndex < files.count {
                    addTask(at: nextIndex)
                    nextIndex += 1
                }
            }
            return results
        }
    }
}
