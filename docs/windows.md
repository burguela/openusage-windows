# Windows

OpenUsage runs on Windows 10 and 11 as a notification-area (tray) app. It tracks the same providers,
reads the same local credentials, and shows the same numbers as the Mac app, because both use one
shared engine.

## Using it

- **Click the tray icon** to open the panel above the taskbar. It shows each enabled provider with its
  meters, the plan, and any error. Click anywhere else, or press Esc, to close it.
- **Show More** under a provider reveals its On Demand rows (the ones behind the caret on the Mac).
- **Refresh** in the footer (or F5 / Ctrl+R while the panel is open) refreshes every provider now,
  skipping the cache.
- **Settings** (top right of the panel, or right-click the tray icon) turns providers on and off, picks
  whether meters show usage **Left** or **Used**, and toggles **Launch at Login**.
- **Right-click the tray icon** for Open OpenUsage, Refresh Now, Settings, Launch at Login, Open Log
  Folder, and Quit OpenUsage.

The tray icon draws two small meters for the first two pinned rows that have data (Claude's Session
and Weekly by default), in orange or red when a limit is close. Hovering it lists every pinned reading.
Windows may place a new tray icon in the overflow (the `^` arrow); drag it onto the taskbar to keep it
visible.

## First run and refreshing

- On first launch, OpenUsage turns on the providers whose credentials it finds on the PC (falling back to
  Claude, Codex, and Cursor when it finds none), the same rule the Mac app uses.
- It shows cached values right away, then refreshes anything older than five minutes. While the panel is
  open it re-checks every minute, so reset countdowns stay current; while it's closed, every five minutes.
- A provider that fails keeps its last good values and shows the error above its rows.

## Where credentials come from

Each provider reads what its own CLI or desktop app already stored, exactly as on the Mac. On Windows:

| Provider | Looks in |
| --- | --- |
| Claude | `%USERPROFILE%\.claude\.credentials.json`; Claude Desktop in `%APPDATA%\Claude` (decrypted with the Windows account's DPAPI key) |
| Codex | `%USERPROFILE%\.codex\auth.json` (or `CODEX_HOME`) |
| Cursor | `%APPDATA%\Cursor\User\globalStorage\state.vscdb` |
| Devin | `%APPDATA%\Devin\User\globalStorage\state.vscdb` |
| Copilot | `%LOCALAPPDATA%\github-copilot\apps.json`, then `%APPDATA%\GitHub CLI\hosts.yml`, then Windows Credential Manager (`gh:github.com`) |
| Antigravity | the running Antigravity language server, found with PowerShell |
| Others | the same files and environment variables their provider pages list, under `%USERPROFILE%` |

The Mac Keychain maps to **Windows Credential Manager** (generic credentials). Most CLIs keep file-based
credentials on Windows, so Credential Manager is only a fallback.

## Where OpenUsage keeps its files

| What | Where |
| --- | --- |
| Settings and cached snapshots (provider on/off, meter style) | the `com.robinebers.openusage` preferences file Foundation's `UserDefaults` keeps under your user's AppData folder |
| Logs | `%LOCALAPPDATA%\OpenUsage\Logs\` (`OpenUsage.log` from the engine, `OpenUsage.Windows.log` from the tray app) |
| Spend-history parse cache, pricing cache | `%LOCALAPPDATA%\OpenUsage\` |
| Launch at Login | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `OpenUsage` |

Deleting that preferences file resets OpenUsage to a first run.

## What isn't on Windows yet

These Mac features are not part of the Windows app: Customize (reordering, hiding, and pinning metrics;
Windows uses the default layout), notifications, the global shortcut, the local HTTP API, iCloud Sync,
share cards, and automatic updates. Install a new version by replacing the app folder.

## How it's built

Two programs ship side by side in one folder:

- `openusage-cli.exe` — the shared Swift engine (the `OpenUsageCLI` target). It still works as the
  [command-line interface](cli.md), and it adds the desktop commands the tray app calls.
- `OpenUsage.exe` — the tray app, a small .NET 8 WPF program in `windows/OpenUsage.Windows/`. It runs the
  engine, reads the `openusage.desktop.v1` JSON it prints, and draws it. It never reads credentials or
  calls provider APIs itself.

The folder also holds the Swift runtime DLLs, the engine's resources (`OpenUsage_OpenUsage.resources`),
and `sqlite3.exe`, which Cursor, Devin, OpenCode, and Claude Desktop need to read their local databases.

The `Windows` GitHub Actions workflow (`.github/workflows/windows.yml`) builds everything on
`windows-latest` and uploads the folder as the `OpenUsage-windows-x64` artifact. To build locally:

```powershell
# Swift 6.2 for Windows and the .NET 8 SDK installed. The -D flags let Swift 6.2's Clang use a newer
# Visual Studio C++ library; drop them if your Visual Studio matches the toolchain.
swift build -c release --product openusage-cli -Xcc -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH -Xcxx -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH
dotnet publish windows/OpenUsage.Windows/OpenUsage.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/OpenUsage
windows/scripts/package.ps1 -BuildDir .build/release -OutDir dist/OpenUsage
dist/OpenUsage/OpenUsage.exe
```

While developing the tray app, set `OPENUSAGE_ENGINE` to a built `openusage-cli.exe` to use an engine
from another folder. `windows/scripts/generate_assets.py` regenerates the tray app's icon and provider
marks from `Sources/OpenUsage/Resources/ProviderIcons`.
