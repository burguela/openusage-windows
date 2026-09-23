# Installs or updates Quota Tray for the current user from this fork's latest GitHub release. Paste into
# PowerShell:
#
#   irm https://github.com/burguela/openusage-windows/releases/latest/download/install.ps1 | iex
#
# It runs the regular installer silently (per user, no administrator rights) with "Start when I sign in"
# on, then starts the app. Set QUOTATRAY_SETUP to a local QuotaTray-Setup-x64.exe to skip the download.
& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'

    $setup = $env:QUOTATRAY_SETUP
    $downloaded = $false
    if (-not $setup) {
        $setup = Join-Path ([IO.Path]::GetTempPath()) 'QuotaTray-Setup-x64.exe'
        Write-Host 'Downloading Quota Tray...'
        Invoke-WebRequest -UseBasicParsing -OutFile $setup `
            -Uri 'https://github.com/burguela/openusage-windows/releases/latest/download/QuotaTray-Setup-x64.exe'
        $downloaded = $true
    }

    Write-Host 'Installing...'
    try {
        $process = Start-Process $setup -Wait -PassThru `
            -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=startup'
    } finally {
        if ($downloaded) { Remove-Item $setup -Force -ErrorAction SilentlyContinue }
    }
    if ($process.ExitCode -ne 0) { throw "The Quota Tray installer failed (exit code $($process.ExitCode))." }

    $app = Join-Path $env:LOCALAPPDATA 'Programs\QuotaTray\QuotaTray.exe'
    if (-not (Test-Path $app)) { throw "The installer finished but $app is missing." }
    if (-not $env:QUOTATRAY_NO_LAUNCH) { Start-Process $app }
    Write-Host 'Quota Tray is installed. Its numbers sit in the taskbar next to the clock, and it starts when you sign in.'
    Write-Host 'Uninstall from Settings > Apps, or paste:'
    Write-Host '  irm https://github.com/burguela/openusage-windows/releases/latest/download/uninstall.ps1 | iex'
}
