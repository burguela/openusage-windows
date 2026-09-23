import Foundation
#if os(Windows)
import WinSDK
#elseif canImport(Darwin)
import Darwin
#elseif canImport(Glibc)
import Glibc
#elseif canImport(Musl)
import Musl
#endif

/// The few OS-level operations the provider engine needs that Foundation doesn't cover portably:
/// private atomic file writes, advisory file locks, and locating helper executables. macOS and
/// Linux use POSIX; Windows uses Win32. Everything else in the engine sticks to Foundation.
enum Platform {
    #if os(Windows)
    static let isWindows = true
    /// Executable suffixes Windows tries when a bare command name is resolved (`PATHEXT`).
    static let executableExtensions: [String] = {
        let raw = ProcessInfo.processInfo.environment["PATHEXT"] ?? ".COM;.EXE;.BAT;.CMD"
        return raw.split(separator: ";").map { $0.lowercased() }
    }()
    static let pathListSeparator: Character = ";"
    #else
    static let isWindows = false
    static let executableExtensions: [String] = [""]
    static let pathListSeparator: Character = ":"
    #endif

    /// Resolve a helper executable. An absolute path is used as-is; a bare name is looked up next to
    /// the running executable first (the Windows build ships `sqlite3.exe` beside `openusage.exe`),
    /// then on `PATH`. `nil` when nothing usable is found, so callers can fail loudly with context.
    static func findExecutable(_ name: String) -> URL? {
        if isAbsolutePath(name) {
            return FileManager.default.isExecutableFile(atPath: name) ? URL(fileURLWithPath: name) : nil
        }
        var directories: [URL] = []
        if let executableDirectory = Bundle.main.executableURL?.deletingLastPathComponent() {
            directories.append(executableDirectory)
        }
        let environment = ProcessInfo.processInfo.environment
        let path = environment["PATH"] ?? environment["Path"] ?? ""
        directories += path.split(separator: pathListSeparator).map {
            URL(fileURLWithPath: String($0).trimmingCharacters(in: CharacterSet(charactersIn: "\"")), isDirectory: true)
        }
        let hasExtension = !(name as NSString).pathExtension.isEmpty
        let candidates = isWindows && !hasExtension ? executableExtensions.map { name + $0 } : [name]
        for directory in directories {
            for candidate in candidates {
                let url = directory.appendingPathComponent(candidate)
                if FileManager.default.isExecutableFile(atPath: url.path) {
                    return url
                }
            }
        }
        return nil
    }

    static func isAbsolutePath(_ path: String) -> Bool {
        if path.hasPrefix("/") { return true }
        #if os(Windows)
        // `C:\…`, `C:/…`, or a UNC `\\server\share` path.
        if path.hasPrefix("\\\\") { return true }
        let scalars = Array(path.unicodeScalars.prefix(3))
        if scalars.count == 3, CharacterSet.letters.contains(scalars[0]), scalars[1] == ":",
           scalars[2] == "\\" || scalars[2] == "/" {
            return true
        }
        #endif
        return false
    }

    /// Per-user app data root: `~/Library/Application Support` on macOS, `%LOCALAPPDATA%` on Windows
    /// (machine-local, unlike the roaming `%APPDATA%`, which suits caches and logs), and the XDG data
    /// directory on Linux.
    static var appDataDirectory: URL {
        #if os(Windows)
        if let local = ProcessInfo.processInfo.environment["LOCALAPPDATA"], !local.isEmpty {
            return URL(fileURLWithPath: local, isDirectory: true)
        }
        #endif
        return FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? FileManager.default.temporaryDirectory
    }

    /// Set a variable in this process's environment (seen by libcurl and child processes).
    static func setEnvironmentVariable(_ name: String, _ value: String) {
        #if os(Windows)
        _ = name.withCString(encodedAs: UTF16.self) { key in
            value.withCString(encodedAs: UTF16.self) { SetEnvironmentVariableW(key, $0) }
        }
        #else
        setenv(name, value, 1)
        #endif
    }

    // MARK: - Private atomic writes

    /// Write `data` to `path` so the final file appears atomically and is readable only by the
    /// current user. POSIX: a 0600 temporary file in the destination directory, fsynced, then renamed
    /// over the target. Windows: files under the user profile already inherit a per-user ACL, so a
    /// temporary file plus `MoveFileExW(MOVEFILE_REPLACE_EXISTING)` gives the same guarantee.
    static func writePrivateFileAtomically(_ data: Data, to path: String) throws {
        let destination = URL(fileURLWithPath: path)
        let parent = destination.deletingLastPathComponent()
        try FileManager.default.createDirectory(at: parent, withIntermediateDirectories: true)
        let temporary = parent.appendingPathComponent(
            ".\(destination.lastPathComponent).\(UUID().uuidString).tmp"
        )
        #if os(Windows)
        do {
            try data.write(to: temporary, options: .withoutOverwriting)
            let moved = temporary.path.withCString(encodedAs: UTF16.self) { source in
                destination.path.withCString(encodedAs: UTF16.self) { target in
                    MoveFileExW(source, target, DWORD(MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                }
            }
            guard moved else { throw windowsError("MoveFileExW") }
        } catch {
            try? FileManager.default.removeItem(at: temporary)
            throw error
        }
        #else
        let privateMode = mode_t(S_IRUSR | S_IWUSR)
        let descriptor = temporary.path.withCString {
            open($0, O_WRONLY | O_CREAT | O_EXCL | O_CLOEXEC | O_NOFOLLOW, privateMode)
        }
        guard descriptor >= 0 else { throw currentPOSIXError() }

        var descriptorIsOpen = true
        var temporaryExists = true
        defer {
            if descriptorIsOpen { _ = close(descriptor) }
            if temporaryExists {
                temporary.path.withCString { _ = unlink($0) }
            }
        }

        // A process umask may only remove permissions at creation. Reassert the exact private mode on
        // the still-unpublished inode before writing or renaming it into place.
        guard fchmod(descriptor, privateMode) == 0 else { throw currentPOSIXError() }
        try writeAll(data, to: descriptor)
        guard fsync(descriptor) == 0 else { throw currentPOSIXError() }
        let closeResult = close(descriptor)
        descriptorIsOpen = false
        guard closeResult == 0 else { throw currentPOSIXError() }

        let renameResult = temporary.path.withCString { source in
            path.withCString { target in rename(source, target) }
        }
        guard renameResult == 0 else { throw currentPOSIXError() }
        temporaryExists = false
        #endif
    }

    #if !os(Windows)
    private static func writeAll(_ data: Data, to descriptor: Int32) throws {
        try data.withUnsafeBytes { buffer in
            guard let baseAddress = buffer.baseAddress else { return }
            var offset = 0
            while offset < buffer.count {
                let result = write(descriptor, baseAddress.advanced(by: offset), buffer.count - offset)
                if result < 0 {
                    if errno == EINTR { continue }
                    throw currentPOSIXError()
                }
                guard result > 0 else { throw POSIXError(.EIO) }
                offset += result
            }
        }
    }

    static func currentPOSIXError() -> POSIXError {
        POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
    }
    #endif

    #if os(Windows)
    static func windowsError(_ operation: String, code: DWORD = GetLastError()) -> NSError {
        NSError(
            domain: "OpenUsage.Win32",
            code: Int(code),
            userInfo: [NSLocalizedDescriptionKey: "\(operation) failed (Win32 error \(code))"]
        )
    }
    #endif

    // MARK: - Owner-only permissions

    /// Restrict a file or directory to its owner (0600 / 0700). A no-op on Windows, where the user
    /// profile's inherited ACL already keeps these files private to the account.
    static func restrictToOwner(_ path: String, isDirectory: Bool) throws {
        #if !os(Windows)
        try FileManager.default.setAttributes(
            [.posixPermissions: isDirectory ? 0o700 : 0o600],
            ofItemAtPath: path
        )
        #endif
    }
}

/// An advisory, cross-process file lock, released when the value is dropped (or the process dies).
/// POSIX `flock` on macOS/Linux, `LockFileEx` on Windows.
final class PlatformFileLock {
    enum Mode {
        case shared
        case exclusive
    }

    enum LockError: Error, Equatable {
        /// A non-blocking request found the lock already held by someone else.
        case wouldBlock
    }

    #if os(Windows)
    private let handle: HANDLE
    #else
    private let descriptor: Int32
    #endif

    /// Open (creating if needed) the lock file at `url` and take the lock. `nonblocking` throws
    /// `LockError.wouldBlock` instead of waiting when another process holds a conflicting lock.
    init(url: URL, mode: Mode, nonblocking: Bool = false) throws {
        #if os(Windows)
        let handle = url.path.withCString(encodedAs: UTF16.self) { path in
            CreateFileW(
                path,
                DWORD(GENERIC_READ) | DWORD(GENERIC_WRITE),
                DWORD(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE),
                nil,
                DWORD(OPEN_ALWAYS),
                DWORD(FILE_ATTRIBUTE_NORMAL),
                nil
            )
        }
        guard let handle, handle != INVALID_HANDLE_VALUE else {
            throw Platform.windowsError("CreateFileW")
        }
        var flags = DWORD(0)
        if mode == .exclusive { flags |= DWORD(LOCKFILE_EXCLUSIVE_LOCK) }
        if nonblocking { flags |= DWORD(LOCKFILE_FAIL_IMMEDIATELY) }
        var overlapped = OVERLAPPED()
        guard LockFileEx(handle, flags, 0, DWORD.max, DWORD.max, &overlapped) else {
            let code = GetLastError()
            CloseHandle(handle)
            if code == DWORD(ERROR_LOCK_VIOLATION) || code == DWORD(ERROR_IO_PENDING) {
                throw LockError.wouldBlock
            }
            throw Platform.windowsError("LockFileEx", code: code)
        }
        self.handle = handle
        #else
        let privateMode = mode_t(S_IRUSR | S_IWUSR)
        let descriptor = open(url.path, O_CREAT | O_RDWR | O_CLOEXEC, privateMode)
        guard descriptor >= 0 else { throw Platform.currentPOSIXError() }
        guard fchmod(descriptor, privateMode) == 0 else {
            let error = Platform.currentPOSIXError()
            close(descriptor)
            throw error
        }
        var operation = mode == .exclusive ? LOCK_EX : LOCK_SH
        if nonblocking { operation |= LOCK_NB }
        guard flock(descriptor, operation) == 0 else {
            let code = errno
            close(descriptor)
            if code == EWOULDBLOCK { throw LockError.wouldBlock }
            throw POSIXError(POSIXErrorCode(rawValue: code) ?? .EIO)
        }
        self.descriptor = descriptor
        #endif
    }

    deinit {
        #if os(Windows)
        var overlapped = OVERLAPPED()
        UnlockFileEx(handle, 0, DWORD.max, DWORD.max, &overlapped)
        CloseHandle(handle)
        #else
        flock(descriptor, LOCK_UN)
        close(descriptor)
        #endif
    }
}
