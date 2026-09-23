import Foundation
import XCTest

final class ProvisioningProfileScriptTests: XCTestCase {
    override func setUpWithError() throws {
        #if os(Windows)
        throw XCTSkip("The provisioning-profile matcher is a bash script for the macOS release; Windows has no /bin/bash.")
        #endif
    }

    func testProfileMatcherAcceptsBareAndMatchingTeamPrefixedUbiquityIdentifiers() throws {
        XCTAssertTrue(try matches(containerID: "iCloud.com.robinebers.openusage.dev"))
        XCTAssertTrue(try matches(containerID: "TEAM123.iCloud.com.robinebers.openusage.dev"))
    }

    func testProfileMatcherRejectsWrongTeamBundleAndContainerIdentifiers() throws {
        XCTAssertFalse(try matches(containerID: "OTHERTEAM.iCloud.com.robinebers.openusage.dev"))
        XCTAssertFalse(try matches(
            applicationID: "TEAM123.com.robinebers.some-other-app",
            containerID: "TEAM123.iCloud.com.robinebers.openusage.dev"
        ))
        XCTAssertFalse(try matches(containerID: "TEAM123.iCloud.com.robinebers.openusage"))
    }

    private func matches(
        applicationID: String = "TEAM123.com.robinebers.openusage.dev",
        containerID: String
    ) throws -> Bool {
        let script = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("script/find_icloud_provisioning_profile.sh")

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/bash")
        process.arguments = [
            "-c",
            "source \"$1\"; profile_matches_identifiers \"$2\" \"$3\" \"$4\" \"$5\"",
            "profile-matcher-test",
            script.path,
            applicationID,
            containerID,
            "com.robinebers.openusage.dev",
            "iCloud.com.robinebers.openusage.dev"
        ]
        try process.run()
        process.waitUntilExit()
        return process.terminationStatus == 0
    }
}
