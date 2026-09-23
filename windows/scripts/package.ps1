# Assembles the Windows app folder next to the published OpenUsage.exe:
#   openusage-cli.exe              the shared Swift engine (also usable as a CLI)
#   OpenUsage_OpenUsage.resources  the engine's bundled resources (pricing, provider icons)
#   *.dll                          the Swift runtime and the MSVC runtime it links against
#   sqlite3.exe                    used by the Cursor and OpenCode providers to read local databases
param(
    [Parameter(Mandatory)] [string] $BuildDir,
    [Parameter(Mandatory)] [string] $OutDir
)
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Copy-Item (Join-Path $BuildDir 'openusage-cli.exe') $OutDir
Copy-Item -Recurse -Force (Join-Path $BuildDir 'OpenUsage_OpenUsage.resources') $OutDir

# Swift runtime: every DLL in the toolchain's runtime folder (the one holding swiftCore.dll).
$swiftCore = where.exe swiftCore.dll | Select-Object -First 1
if (-not $swiftCore) { throw 'swiftCore.dll is not on PATH' }
$runtimeDir = Split-Path $swiftCore
Write-Host "Swift runtime: $runtimeDir"
Copy-Item (Join-Path $runtimeDir '*.dll') $OutDir

# Swift links against the Visual C++ runtime; ship it app-local so a clean machine needs nothing.
foreach ($dll in 'vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll') {
    $source = Join-Path $env:SystemRoot "System32\$dll"
    if (Test-Path $source) { Copy-Item $source $OutDir }
}

# sqlite3.exe from the SQLite command-line tools.
choco install sqlite --no-progress -y | Out-Null
$sqlite = Get-ChildItem -Recurse -Filter sqlite3.exe "$env:ChocolateyInstall\lib" | Select-Object -First 1
if (-not $sqlite) { throw 'sqlite3.exe not found after installing sqlite' }
Copy-Item $sqlite.FullName $OutDir

# The engine must run from the packaged folder alone.
$check = & (Join-Path $OutDir 'openusage-cli.exe') dashboard --cached | ConvertFrom-Json
if ($check.schema -ne 'openusage.desktop.v1') { throw 'packaged engine failed its smoke test' }
Get-ChildItem $OutDir | Format-Table Name, Length
