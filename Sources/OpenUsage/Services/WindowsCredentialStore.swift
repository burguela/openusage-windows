import Foundation
#if os(Windows)
import WinSDK
#endif

#if !os(macOS)
/// The system credential store off macOS. Providers keep calling it `SecurityKeychainAccessor` so
/// their initializers don't fork per platform; what backs it differs:
///
/// - **Windows:** the Windows Credential Manager (generic credentials, the store Git Credential
///   Manager, `gh`, and other CLIs use through `wincred`). A Keychain `service` maps to the
///   credential's target name, and an explicit `account` to `service:account` first — the convention
///   `go-keyring` (used by `gh`) follows — then the plain target filtered by user name.
/// - **Linux:** there is no single system keychain, so every lookup reports "no item" and providers
///   fall back to the credential files the CLIs write on those platforms.
///
/// Most CLIs keep file-based credentials on Windows (`~/.claude/.credentials.json`,
/// `~/.codex/auth.json`), so the Credential Manager is only the secondary source there.
struct SecurityKeychainAccessor: KeychainAccessing {
    init() {}

    func readGenericPassword(service: String) throws -> String? {
        try read(targets: [service], account: nil)
    }

    func readGenericPassword(service: String, account: String) throws -> String? {
        try read(targets: ["\(service):\(account)", service], account: account)
    }

    func readGenericPasswordForCurrentUser(service: String) throws -> String? {
        try read(targets: [service], account: nil)
    }

    func genericPasswordExists(service: String) -> Bool? {
        do {
            return try readGenericPassword(service: service) != nil
        } catch {
            return nil
        }
    }

    func writeGenericPassword(service: String, value: String) throws {
        try write(target: service, value: value)
    }

    func writeGenericPasswordForCurrentUser(service: String, value: String) throws {
        try write(target: service, value: value)
    }

    private func read(targets: [String], account: String?) throws -> String? {
        #if os(Windows)
        for target in targets {
            if let value = try Self.readCredential(target: target, requiredUserName: target == targets.last ? account : nil) {
                return value
            }
        }
        return nil
        #else
        return nil
        #endif
    }

    private func write(target: String, value: String) throws {
        #if os(Windows)
        try Self.writeCredential(target: target, value: value)
        #else
        throw KeychainError.writeFailed("No system credential store is available on this platform.")
        #endif
    }

    #if os(Windows)
    /// `CredReadW` for a generic credential. The blob is whatever the writer stored: CLIs built on
    /// `wincred`/`go-keyring` store UTF-8 bytes, while credentials typed into Credential Manager's UI
    /// are UTF-16LE. Decode UTF-8 first (the common case), then UTF-16LE.
    private static func readCredential(target: String, requiredUserName: String?) throws -> String? {
        var pointer: PCREDENTIALW?
        let found = target.withCString(encodedAs: UTF16.self) { name in
            CredReadW(name, DWORD(CRED_TYPE_GENERIC), 0, &pointer)
        }
        guard found, let credential = pointer?.pointee else {
            let code = GetLastError()
            if code == DWORD(ERROR_NOT_FOUND) { return nil }
            AppLog.warn(.keychain, "Credential Manager read failed for '\(target)' (Win32 error \(code))")
            throw KeychainError.readFailed("Credential Manager read failed (Win32 error \(code)).")
        }
        defer { CredFree(pointer) }

        if let requiredUserName, let userName = credential.UserName {
            let stored = String(decodingCString: userName, as: UTF16.self)
            guard stored.caseInsensitiveCompare(requiredUserName) == .orderedSame else { return nil }
        }
        guard let blob = credential.CredentialBlob, credential.CredentialBlobSize > 0 else { return nil }
        let data = Data(bytes: blob, count: Int(credential.CredentialBlobSize))
        let text = Self.decodeBlob(data)?.trimmingCharacters(in: .whitespacesAndNewlines)
        return text?.isEmpty == false ? text : nil
    }

    static func decodeBlob(_ data: Data) -> String? {
        // A UTF-16LE blob of ASCII text has a zero every other byte; UTF-8 text never contains NULs.
        if data.count >= 2, data.count % 2 == 0, data.contains(0) {
            return String(data: data, encoding: .utf16LittleEndian)
        }
        return String(data: data, encoding: .utf8)
    }

    private static func writeCredential(target: String, value: String) throws {
        var blob = Array(value.utf8)
        let userName = ProcessInfo.processInfo.environment["USERNAME"] ?? ""
        let written = target.withCString(encodedAs: UTF16.self) { name in
            userName.withCString(encodedAs: UTF16.self) { user in
                blob.withUnsafeMutableBytes { bytes -> Bool in
                    var credential = CREDENTIALW()
                    credential.Type = DWORD(CRED_TYPE_GENERIC)
                    credential.TargetName = UnsafeMutablePointer(mutating: name)
                    credential.UserName = UnsafeMutablePointer(mutating: user)
                    credential.CredentialBlobSize = DWORD(bytes.count)
                    credential.CredentialBlob = bytes.baseAddress?.assumingMemoryBound(to: BYTE.self)
                    credential.Persist = DWORD(CRED_PERSIST_LOCAL_MACHINE)
                    return CredWriteW(&credential, 0)
                }
            }
        }
        guard written else {
            let code = GetLastError()
            throw KeychainError.writeFailed("Credential Manager write failed (Win32 error \(code)).")
        }
    }
    #endif
}
#endif
