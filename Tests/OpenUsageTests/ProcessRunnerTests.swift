import XCTest
@testable import OpenUsage

final class ProcessRunnerTests: XCTestCase {
    /// Regression: a child whose output exceeds the ~64KB OS pipe buffer must not deadlock. Before the
    /// pipes were drained concurrently, this blocked the child on write, so it never exited and tripped
    /// the timeout. (`ps -ax -o command=` — used by language-server discovery — is ~240KB.)
    func testLargeStdoutDoesNotDeadlock() throws {
        let runner = SystemProcessRunner()
        #if os(Windows)
        let result = try runner.run(
            executable: "powershell",
            arguments: ["-NoProfile", "-Command", "[Console]::Out.Write('0123456789' * 20000)"],
            environment: [:],
            timeout: 30
        )
        #else
        let result = try runner.run(
            executable: "/bin/sh",
            arguments: ["-c", "yes 0123456789 | head -c 200000"],
            environment: [:],
            timeout: 10
        )
        #endif
        XCTAssertTrue(result.succeeded)
        XCTAssertEqual(result.stdout.count, 200_000)
    }

    func testCapturesStdoutAndExitCode() throws {
        let runner = SystemProcessRunner()
        #if os(Windows)
        let result = try runner.run(executable: "cmd", arguments: ["/c", "echo hello"], environment: [:], timeout: 5)
        #else
        let result = try runner.run(executable: "/bin/echo", arguments: ["hello"], environment: [:], timeout: 5)
        #endif
        XCTAssertEqual(result.exitCode, 0)
        XCTAssertEqual(result.stdout.trimmingCharacters(in: .whitespacesAndNewlines), "hello")
    }
}
