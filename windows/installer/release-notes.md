Quota Tray {VERSION} for Windows 10 and 11 (x64): your AI usage limits in the taskbar, with the Mac app's strip of provider marks and numbers, and the same panel.

{CHANGES}

Quota Tray is an unofficial fork of [OpenUsage](https://github.com/robinebers/openusage) by Robin Ebers and contributors (MIT). It is not an official OpenUsage release; report problems in this repository's issues.

## Install

Paste into PowerShell (installs for your account only, no administrator rights, and starts Quota Tray when you sign in):

```powershell
irm https://github.com/burguela/quota-tray/releases/latest/download/install.ps1 | iex
```

Or download and run `QuotaTray-Setup-x64.exe` below. To update, run either one again.

`QuotaTray-windows-x64.zip` is the portable build: unzip it anywhere and run `QuotaTray.exe`.

## Uninstall

Settings → Apps → Quota Tray, or paste into PowerShell:

```powershell
irm https://github.com/burguela/quota-tray/releases/latest/download/uninstall.ps1 | iex
```

The app isn't code-signed yet, so Windows SmartScreen may warn the first time; choose **More info → Run anyway**. See [docs/windows.md](https://github.com/burguela/quota-tray/blob/main/docs/windows.md) for how Quota Tray works.
