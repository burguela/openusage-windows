import XCTest
@testable import OpenUsageCLI

final class CLIArgumentsTests: XCTestCase {
    func testParsesProviderAndForce() throws {
        let parsed = try CLIArguments.parse(["Codex", "--force"])
        XCTAssertEqual(parsed.providerID, "codex")
        XCTAssertTrue(parsed.force)
    }

    func testRejectsUnknownOptionsAndMultipleProviders() {
        XCTAssertThrowsError(try CLIArguments.parse(["--json"]))
        XCTAssertThrowsError(try CLIArguments.parse(["claude", "codex"]))
    }

    func testParsesDesktopCommands() throws {
        XCTAssertEqual(try CLIArguments.parse(["dashboard"]).command, .dashboard)
        let cached = try CLIArguments.parse(["dashboard", "--cached"])
        XCTAssertTrue(cached.cachedOnly)
        XCTAssertEqual(try CLIArguments.parse(["enable", "Cursor"]).command, .enable("cursor"))
        XCTAssertEqual(try CLIArguments.parse(["disable", "grok"]).command, .disable("grok"))
        XCTAssertEqual(try CLIArguments.parse(["meter-style", "used"]).command, .meterStyle(showRemaining: false))
        XCTAssertNil(try CLIArguments.parse(["dashboard"]).providerID)
    }

    func testRejectsMalformedDesktopCommands() {
        XCTAssertThrowsError(try CLIArguments.parse(["enable"]))
        XCTAssertThrowsError(try CLIArguments.parse(["dashboard", "claude"]))
        XCTAssertThrowsError(try CLIArguments.parse(["meter-style", "sideways"]))
        XCTAssertThrowsError(try CLIArguments.parse(["dashboard", "--force", "--cached"]))
        XCTAssertThrowsError(try CLIArguments.parse(["codex", "--cached"]))
    }
}
