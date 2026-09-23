import Foundation
import Testing
@testable import OpenUsage

struct PlatformExecutableTests {
    /// Regression: on Windows a missing `sqlite3.com` beside the engine counted as executable (the
    /// check went by extension alone) and shadowed the real `sqlite3.exe`, so every helper launch failed.
    @Test func missingFilesAreNotExecutables() {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        for name in ["sqlite3.com", "sqlite3.exe", "sqlite3"] {
            #expect(!Platform.isExistingExecutable(atPath: directory.appendingPathComponent(name).path))
        }
    }

    @Test func directoriesAreNotExecutables() throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString)
            .appendingPathComponent("tool.exe")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory.deletingLastPathComponent()) }
        #expect(!Platform.isExistingExecutable(atPath: directory.path))
    }

    @Test func findsAShellOnPath() {
        #if os(Windows)
        let found = Platform.findExecutable("cmd")
        #expect(found?.lastPathComponent.lowercased() == "cmd.exe")
        #else
        #expect(Platform.findExecutable("sh") != nil)
        #endif
    }
}
