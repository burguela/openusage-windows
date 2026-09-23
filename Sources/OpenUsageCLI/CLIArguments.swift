import Foundation

struct CLIArguments: Equatable, Sendable {
    /// The desktop-host commands the Windows tray app drives (see `DesktopHost`). The default command
    /// (no subcommand) prints the stable `/v1/limits` JSON.
    enum Command: Equatable, Sendable {
        case limits
        case dashboard
        case enable(String)
        case disable(String)
        case meterStyle(showRemaining: Bool)
    }

    var command: Command = .limits
    var providerID: String?
    var force = false
    var cachedOnly = false
    var showHelp = false
    var showVersion = false

    static func parse(_ arguments: [String]) throws -> CLIArguments {
        var parsed = CLIArguments()
        var positionals: [String] = []
        for argument in arguments {
            switch argument {
            case "--force": parsed.force = true
            case "--cached": parsed.cachedOnly = true
            case "-h", "--help": parsed.showHelp = true
            case "-v", "--version": parsed.showVersion = true
            default:
                if argument.hasPrefix("-") {
                    throw CLIError.usage("Unknown option: \(argument)")
                }
                positionals.append(argument)
            }
        }

        switch positionals.first?.lowercased() {
        case "dashboard":
            guard positionals.count == 1 else { throw CLIError.usage("dashboard takes no provider.") }
            parsed.command = .dashboard
        case "enable", "disable":
            guard positionals.count == 2 else {
                throw CLIError.usage("\(positionals[0]) needs exactly one provider id.")
            }
            let id = positionals[1].lowercased()
            parsed.command = positionals[0].lowercased() == "enable" ? .enable(id) : .disable(id)
        case "meter-style":
            guard positionals.count == 2, ["left", "used"].contains(positionals[1].lowercased()) else {
                throw CLIError.usage("meter-style needs 'left' or 'used'.")
            }
            parsed.command = .meterStyle(showRemaining: positionals[1].lowercased() == "left")
        default:
            guard positionals.count <= 1 else {
                throw CLIError.usage("Only one provider can be requested at a time.")
            }
            parsed.providerID = positionals.first?.lowercased()
        }

        if parsed.force && parsed.cachedOnly {
            throw CLIError.usage("--force and --cached can't be combined.")
        }
        if parsed.cachedOnly && parsed.command != .dashboard {
            throw CLIError.usage("--cached only applies to dashboard.")
        }
        return parsed
    }
}

enum CLIError: Error, Equatable {
    case usage(String)
    case appDefaultsUnavailable
}
