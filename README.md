# Quota Tray

Track your AI coding subscriptions from the Windows notification area.

> **Quota Tray is an unofficial, community fork of [OpenUsage](https://github.com/robinebers/openusage).**
> It brings the original macOS app to Windows. It is not the official OpenUsage, and it is not affiliated
> with, reviewed by, or endorsed by Robin Ebers or the OpenUsage maintainers. For the official macOS app,
> go to [robinebers/openusage](https://github.com/robinebers/openusage). Report problems with Quota Tray
> [here](https://github.com/burguela/openusage-windows/issues), not upstream.

Quota Tray shows how much of your AI coding plans you've used: session and weekly limits, credits, and
spend, all in one panel. It runs OpenUsage's engine on Windows 10 and 11 and draws it in a tray app that
looks and works like the OpenUsage Mac popover.

<p align="center">
  <img src="docs/screenshots/windows-dashboard-light.png" alt="Quota Tray in light mode: Total Spend ring and Claude, Codex, and Cursor usage cards" width="300">
  &nbsp;&nbsp;
  <img src="docs/screenshots/windows-dashboard-dark.png" alt="Quota Tray in dark mode" width="300">
</p>

## Credits

OpenUsage was created by **[Robin Ebers](https://github.com/robinebers)** and is developed by the
[OpenUsage contributors](https://github.com/robinebers/openusage/graphs/contributors). Almost everything
in this repository is their work: the provider integrations, the usage engine, spend pricing, the CLI, the
macOS app, the design, and the documentation. Quota Tray only adds the Windows port described below.

- **Original repository:** [github.com/robinebers/openusage](https://github.com/robinebers/openusage)
- **License:** MIT, © 2026 Robin Ebers. The original [LICENSE](LICENSE) is kept unchanged, and the
  Windows changes are released under the same license.
- **Name and logo:** "OpenUsage" and its logo are trademarks of Robin Ebers
  ([trademark policy](TRADEMARK.md)). As that policy asks of forks, the Windows app has its own name and
  icon; "OpenUsage" appears here only to credit the project it's based on. The app's Settings also credit
  OpenUsage and link to the original repository.
- **Windows port:** maintained by [João Luiz (@burguela)](https://github.com/burguela).

If you like the app, please star and support the [original project](https://github.com/robinebers/openusage).

## What this fork adds

- **Quota Tray, a Windows tray app** (`QuotaTray.exe`, .NET 8 WPF) with the Mac popover's layout and colors in light
  and dark mode: the Total Spend ring, a card per provider, meters with pace ticks, On Demand rows, Settings,
  and a tray icon with mini meters.
- **The shared engine on Windows.** The Swift providers, pricing, and caching now build for Windows
  (shipped as `quotatray-engine.exe`). Credential paths map to `%USERPROFILE%`, `%APPDATA%`, and `%LOCALAPPDATA%`, the
  Keychain maps to Windows Credential Manager, and Claude Desktop tokens are decrypted with DPAPI.
- **Desktop commands** in the CLI that print the whole dashboard as JSON for the tray app
  ([CLI docs](docs/cli.md#desktop-commands)).
- **A Windows CI workflow** that builds, smoke-tests, and packages the app, renders screenshots, and runs
  the tests on Linux.

The macOS app in this repository is the upstream OpenUsage app with no intended changes. Mac users should
install the official build from [robinebers/openusage](https://github.com/robinebers/openusage).

## Install on Windows

Paste into PowerShell:

```powershell
irm https://github.com/burguela/openusage-windows/releases/latest/download/install.ps1 | iex
```

It installs the latest [release](https://github.com/burguela/openusage-windows/releases/latest) for your
account only (no administrator rights), adds a Start menu entry, and starts Quota Tray when you sign in.
You can also download `QuotaTray-Setup-x64.exe` from the release, or the portable
`QuotaTray-windows-x64.zip`. Run the command or the installer again to update. Your pinned readings
appear in the taskbar next to the clock, provider icon and numbers, like the Mac app's menu-bar strip.

Uninstall from **Settings → Apps**, or with
`irm https://github.com/burguela/openusage-windows/releases/latest/download/uninstall.ps1 | iex`.

<p align="center">
  <img src="docs/screenshots/windows-tray-dark.png" alt="Quota Tray's readings in the Windows taskbar" width="420">
</p>

The build isn't code-signed, so Windows SmartScreen may warn the first time; choose **More info → Run
anyway**. Requires Windows 10 or 11 (x64). See [docs/windows.md](docs/windows.md) for how to use it, where
credentials and files live, and what isn't on Windows yet.

## Supported Providers

- **[Antigravity](docs/providers/antigravity.md)** — shared Gemini and Claude pool quotas, 5-hour and weekly windows
- **[Claude](docs/providers/claude.md)** — session, weekly, model-specific limits, extra usage, local daily spend
- **[Codex](docs/providers/codex.md)** — session, weekly, credits, local daily spend
- **[Copilot](docs/providers/copilot.md)** — AI credits, extra usage, organization billing, chat and completions
- **[Cursor](docs/providers/cursor.md)** — credits, total usage, Grok Bot, Cursor Models, Other Models, requests, on-demand, per-day spend
- **[Devin](docs/providers/devin.md)** — weekly and daily quota, extra usage balance
- **[Grok](docs/providers/grok.md)** — weekly shared pool, pay-as-you-go, local daily spend
- **[Ollama](docs/providers/ollama.md)** — Ollama Cloud session and weekly limits, recent activity spend
- **[OpenCode](docs/providers/opencode.md)** — Go session/weekly/monthly caps, Zen spend, local daily spend
- **[OpenRouter](docs/providers/openrouter.md)** — credit balance, daily/weekly/monthly spend (API key)
- **[Z.ai](docs/providers/zai.md)** — session, weekly, web-search quotas (GLM Coding Plan, API key)

Most providers read the credentials already on your machine (auth files, app state, the Keychain on Mac or
Credential Manager on Windows), so there's no extra login. OpenRouter and Z.ai are the exceptions: they
have no local credential to reuse, so you supply an API key (see [OpenRouter setup](docs/providers/openrouter.md)
or [Z.ai setup](docs/providers/zai.md)). Credentials are used only for the corresponding provider requests.
OpenUsage's separate anonymous summaries and public pricing downloads are documented under
[Privacy & usage data](docs/privacy.md).

## Documentation

- [Windows](docs/windows.md): Quota Tray, credential and file locations, what's Mac-only, how it's built.
- Behavior docs from the original project live in [docs/](docs/README.md): the [dashboard](docs/dashboard.md),
  [menu bar pins](docs/menu-bar.md), [settings](docs/settings.md), [refresh & caching](docs/refreshing.md),
  the [CLI](docs/cli.md), the [local HTTP API](docs/local-http-api.md), the [proxy](docs/proxy.md), and one
  page per provider. Pages about the menu bar, Customize, updates, and iCloud Sync describe the Mac app.
- Developer docs: [architecture](docs/architecture.md), [adding a provider](docs/adding-a-provider.md), and
  [debugging & capturing logs](docs/debugging.md). [AGENTS.md](AGENTS.md) holds the engineering conventions.

## Building

**Windows** (Swift 6.2 for Windows and the .NET 8 SDK):

```powershell
swift build -c release --product openusage-cli -Xcc -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH -Xcxx -D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH
dotnet publish windows/QuotaTray/QuotaTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/QuotaTray
windows/scripts/package.ps1 -BuildDir .build/release -OutDir dist/QuotaTray
```

[docs/windows.md](docs/windows.md#how-its-built) explains each step.

**macOS** (the upstream OpenUsage app, unchanged):

```sh
swift build            # debug build
swift test             # run the test suite
./script/build_and_run.sh   # build and launch the dev app from dist/ (no install)
```

## Keeping up with the original

The fork follows upstream `main`, so provider fixes and new providers from the original project can be
merged in:

```sh
git remote add upstream https://github.com/robinebers/openusage.git
git fetch upstream
git merge upstream/main
```

Most Windows code lives in separate places (`windows/`, `.github/workflows/windows.yml`, `docs/windows.md`,
and `#if os(Windows)` branches in the engine), which keeps these merges small.

## Architecture

The original app is a SwiftPM package: SwiftUI content hosted in an AppKit-owned `NSStatusItem` and a
custom `NSPanel`, with Swift 6 strict concurrency. Providers implement a small `ProviderRuntime` protocol
(auth store → usage client → mapper → `ProviderSnapshot`), and every surface reads the same normalized
data. On Windows the Mac UI is left out of the build; the tray app runs the engine and draws the JSON it
prints. See the [architecture overview](docs/architecture.md#windows).

## Releases

This fork publishes only Quota Tray releases (tags `quotatray-vX.Y.Z`), from the **Windows** workflow
run by hand on `main` with "Publish release" checked; see [docs/windows.md](docs/windows.md#releasing).
The release pipeline in [.github/workflows/release.yml](.github/workflows/release.yml) (signed and
notarized DMGs, Sparkle updates, Homebrew) belongs to the official macOS app and needs the original
project's signing secrets; it is kept only so upstream merges stay clean.

## Contributing and security

- **Quota Tray:** open an issue or pull request in
  [this repository](https://github.com/burguela/openusage-windows/issues).
- **Anything that also affects the Mac app** (providers, pricing, the engine): consider reporting it to the
  [original project](https://github.com/robinebers/openusage), following its
  [contributing rules](CONTRIBUTING.md), so everyone benefits.
- **Security:** report Windows-specific issues privately through this repository's
  [security advisories](https://github.com/burguela/openusage-windows/security/advisories/new); report
  anything in the shared code to the original project per [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE), © 2026 Robin Ebers. Quota Tray is released under the same license. The OpenUsage
name and logo are covered by the original project's [trademark policy](TRADEMARK.md).
