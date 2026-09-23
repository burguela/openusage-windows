import Foundation
import OpenUsage

@main
struct OpenUsageCLI {
    static func main() async {
        do {
            let arguments = try CLIArguments.parse(Array(CommandLine.arguments.dropFirst()))
            if arguments.showHelp {
                print(help)
                return
            }

            let app = AppBundleLocator.locate()
            if arguments.showVersion {
                print(app.version.map { "openusage \($0)" } ?? "openusage (development build)")
                return
            }

            guard let defaults = UserDefaults(suiteName: app.bundleIdentifier) else {
                throw CLIError.appDefaultsUnavailable
            }
            switch arguments.command {
            case .limits:
                let result = try await UsageReader(userDefaults: defaults).read(
                    providerID: arguments.providerID,
                    force: arguments.force
                )
                writeOutput(result.data)
                if !result.warnings.isEmpty {
                    defaults.synchronize()
                    result.warnings.forEach { writeError("warning: \($0)") }
                    exit(4)
                }
            case .dashboard:
                let mode: DesktopHost.RefreshMode = arguments.force ? .force : arguments.cachedOnly ? .cached : .ifStale
                writeOutput(await DesktopHost(userDefaults: defaults).dashboard(refresh: mode))
            case .enable(let providerID):
                writeOutput(try await DesktopHost(userDefaults: defaults).setProviderEnabled(true, providerID: providerID))
            case .disable(let providerID):
                writeOutput(try await DesktopHost(userDefaults: defaults).setProviderEnabled(false, providerID: providerID))
            case .meterStyle(let showRemaining):
                writeOutput(await DesktopHost(userDefaults: defaults).setMeterStyle(showRemaining: showRemaining))
            }
            // macOS persists defaults through cfprefsd as they're set. swift-corelibs-foundation
            // (Windows, Linux) keeps them in memory until an explicit flush, and this process exits
            // right after printing — so flush, or the cache and settings would never reach disk.
            defaults.synchronize()
        } catch CLIError.usage(let message) {
            fail("\(message)\nRun 'openusage --help' for usage.", code: 2)
        } catch CLIError.appDefaultsUnavailable {
            fail("Could not open the OpenUsage settings domain.", code: 4)
        } catch UsageReaderError.unknownProvider(let providerID) {
            fail("Unknown provider: \(providerID)", code: 2)
        } catch {
            fail(error.localizedDescription, code: 4)
        }
    }

    private static func writeOutput(_ data: Data) {
        FileHandle.standardOutput.write(data)
        FileHandle.standardOutput.write(Data("\n".utf8))
    }

    private static func writeError(_ message: String) {
        FileHandle.standardError.write(Data("openusage: \(message)\n".utf8))
    }

    private static func fail(_ message: String, code: Int32) -> Never {
        writeError(message)
        exit(code)
    }

    private static let help = """
    Usage: openusage [provider] [--force]

    Read limits through OpenUsage's shared five-minute cache and exit. Output is always JSON.

    Options:
      --force      Refresh even when the shared cache is still fresh
      -v, --version
      -h, --help

    Desktop commands (used by the Windows app):
      openusage dashboard [--force | --cached]   Display-ready dashboard JSON
      openusage enable <provider>                 Turn a provider on
      openusage disable <provider>                Turn a provider off
      openusage meter-style left|used             Show meters as remaining or used
    """
}
