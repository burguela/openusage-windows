# Windows (Quota Tray)

> **Quota Tray** is the Windows app of [openusage-windows](https://github.com/burguela/openusage-windows),
> an unofficial fork of [OpenUsage](https://github.com/robinebers/openusage) by Robin Ebers and
> contributors. It is not an official OpenUsage release, and the original maintainers don't support it;
> report Windows problems in the fork's [issues](https://github.com/burguela/openusage-windows/issues).

Quota Tray runs on Windows 10 and 11 as a notification-area (tray) app. It is built on OpenUsage's
engine, so it tracks the same providers, reads the same local credentials, and shows the same numbers as
the OpenUsage Mac app.

## Using it

The panel follows the Mac popover's layout and colors, in Windows' light or dark app mode.

- **Click the tray icon** to open the panel above the taskbar. Click anywhere else, or press Esc, to
  close it.
- **Cost** at the top is the Total Spend ring: what Claude, Codex, Cursor, and other spend-tracking
  providers cost Today, Yesterday, or over the last 30 days. Turn it off in Settings.
- Each provider has its own card with its meters, the plan, and a warning triangle when the last refresh
  failed (hover it for the reason). **The caret** under a card's rows reveals its On Demand rows and
  quick links. **Click a meter's reading** ("58% left") to switch every meter between Left and Used.
- **The footer** shows the version and the next automatic update; click "Next update in …" (or press
  F5 / Ctrl+R) to refresh every provider now.
- **Options** (bottom right) opens Settings, the log folder, and Quit. Settings has Show Total Spend,
  Launch at Login, Show Usage As (Left or Used), a switch per provider, Open Folder for logs, and an About
  section that credits OpenUsage and links to the original project.
- **Right-click the tray icon** for Open Quota Tray, Refresh Now, Settings, Launch at Login, Open Log
  Folder, and Quit Quota Tray.

The tray icon draws two small meters for the first two pinned rows that have data (Claude's Session
and Weekly by default), in yellow or red when a limit is close. Hovering it lists every pinned reading.
Windows may place a new tray icon in the overflow (the `^` arrow); drag it onto the taskbar to keep it
visible.

<p align="center">
  <img src="screenshots/windows-dashboard-light.png" alt="The Windows panel in light mode" width="260">
  &nbsp;
  <img src="screenshots/windows-dashboard-dark.png" alt="The Windows panel in dark mode" width="260">
  &nbsp;
  <img src="screenshots/windows-settings-light.png" alt="Settings on Windows" width="260">
</p>

## Installing

There are no signed releases yet. Download the `QuotaTray-windows-x64` artifact from the latest successful
run of the fork's [Windows workflow](https://github.com/burguela/openusage-windows/actions/workflows/windows.yml),
unzip it anywhere (for example `%LOCALAPPDATA%\Programs\QuotaTray`), and run `QuotaTray.exe`. The build
isn't code-signed, so SmartScreen may warn the first time; choose **More info → Run anyway**. To update,
quit Quota Tray and replace the folder; settings and caches live elsewhere and are kept.

## First run and refreshing

- On first launch, Quota Tray turns on the providers whose credentials it finds on the PC (falling back to
  Claude, Codex, and Cursor when it finds none), the same rule the OpenUsage Mac app uses.
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

## Where Quota Tray keeps its files

| What | Where |
| --- | --- |
| Settings and cached snapshots (provider on/off, meter style) | the `io.github.burguela.quotatray` preferences file Foundation's `UserDefaults` keeps under your user's AppData folder |
| Logs | `%LOCALAPPDATA%\QuotaTray\Logs\` (`Engine.log` from the engine, `QuotaTray.log` from the tray app) |
| Spend-history parse cache, pricing cache | `%LOCALAPPDATA%\QuotaTray\` |
| Launch at Login | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `QuotaTray` |
| Show Total Spend, the selected spend period | `HKCU\Software\QuotaTray` |

Deleting that preferences file resets Quota Tray to a first run.

## What isn't on Windows yet

These OpenUsage Mac features are not part of Quota Tray: Customize (reordering, hiding, and pinning metrics;
Windows uses the default layout), notifications, the global shortcut, the local HTTP API, iCloud Sync,
share cards, the Cost/MTok and Tokens views of Total Spend, and automatic updates. Install a new version by replacing the app folder.

## How it's built

Two programs ship side by side in one folder:

- `quotatray-engine.exe`: OpenUsage's shared Swift engine (the `OpenUsageCLI` target, built as
  `openusage-cli.exe` and renamed when packaged). It still works as the [command-line interface](cli.md),
  and it adds the desktop commands the tray app calls.
- `QuotaTray.exe`: the tray app, a small .NET 8 WPF program in `windows/QuotaTray/`. It runs the
  engine, reads the `openusage.desktop.v1` JSON it prints, and draws it. It never reads credentials or
  calls provider APIs itself.

The folder also holds the Swift runtime DLLs, the engine's resources (`OpenUsage_OpenUsage.resources`),
and `sqlite3.exe`, which Cursor, Devin, OpenCode, and Claude Desktop need to read their local databases.

The `Windows` GitHub Actions workflow (`.github/workflows/windows.yml`) builds everything on
`windows-latest` and uploads the folder as the `QuotaTray-windows-x64` artifact. To build locally:

```powershell
# Swift 6.2 for Windows and the .NET 8 SDK installed. The -D flags let Swift 6.2's Clang use a newer
# Visual Studio C++ library; drop them if your Visual Studio matches the toolchain.
swift build -c release --product openusage-cli -Xcc -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH -Xcxx -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH
dotnet publish windows/QuotaTray/QuotaTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/QuotaTray
windows/scripts/package.ps1 -BuildDir .build/release -OutDir dist/QuotaTray
dist/QuotaTray/QuotaTray.exe
```

While developing the tray app, set `QUOTATRAY_ENGINE` to a built `openusage-cli.exe` to use an engine
from another folder. `QuotaTray.exe --render-preview <dashboard.json> <folder>` renders the panel (light,
dark, dashboard, Settings) to PNGs from a saved dashboard document; CI does this with
`windows/QuotaTray/Preview/sample-dashboard.json` and uploads the `QuotaTray-windows-screenshots`
artifact. `windows/scripts/generate_assets.py` regenerates the tray app's icon (Quota Tray's own
two-meter icon; the OpenUsage logo is the original project's trademark and isn't used) and provider
marks from `Sources/OpenUsage/Resources/ProviderIcons`.
