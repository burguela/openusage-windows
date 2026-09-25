# Removes Quota Tray for the current user. Paste into PowerShell:
#
#   irm https://github.com/burguela/quota-tray/releases/latest/download/uninstall.ps1 | iex
#
# Same as Settings > Apps > Quota Tray > Uninstall: it quits the app, removes it and its Start menu and
# sign-in entries, and keeps settings and logs in %LOCALAPPDATA%\QuotaTray unless QUOTATRAY_PURGE is set.
& {
    $ErrorActionPreference = 'Stop'

    $uninstaller = Join-Path $env:LOCALAPPDATA 'Programs\QuotaTray\unins000.exe'
    if (-not (Test-Path $uninstaller)) {
        Write-Host 'Quota Tray is not installed for this user.'
        return
    }
    Write-Host 'Uninstalling Quota Tray...'
    $process = Start-Process $uninstaller -Wait -PassThru -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
    if ($process.ExitCode -ne 0) { throw "The Quota Tray uninstaller failed (exit code $($process.ExitCode))." }

    $data = Join-Path $env:LOCALAPPDATA 'QuotaTray'
    if ($env:QUOTATRAY_PURGE) {
        Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item 'HKCU:\Software\QuotaTray' -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host 'Quota Tray and its settings, caches, and logs were removed.'
    } else {
        Write-Host "Quota Tray was removed. Settings and logs stay in $data; delete that folder to remove them too."
    }
}
