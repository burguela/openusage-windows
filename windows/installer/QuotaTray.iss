; Quota Tray installer (Inno Setup 6). Built by .github/workflows/windows.yml from the packaged app
; folder; see docs/windows.md. A per-user install: no administrator rights, files go under
; %LOCALAPPDATA%\Programs\QuotaTray, and it never touches other users.
;
;   iscc /DAppVersion=0.7.0 /DAppDir=dist\QuotaTray /DOutputDir=dist windows\installer\QuotaTray.iss

#ifndef AppVersion
  #error Pass the version: /DAppVersion=x.y.z
#endif
#ifndef AppDir
  #error Pass the packaged app folder: /DAppDir=dist\QuotaTray
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

[Setup]
AppId={{6F1B7C2E-4A8D-4C1F-9E57-3D2B8A61C0F4}
AppName=Quota Tray
AppVersion={#AppVersion}
AppVerName=Quota Tray {#AppVersion}
AppPublisher=Quota Tray (unofficial fork of OpenUsage)
AppPublisherURL=https://github.com/burguela/openusage-windows
AppSupportURL=https://github.com/burguela/openusage-windows/issues
AppComments=Based on OpenUsage by Robin Ebers and contributors (MIT).
DefaultDirName={localappdata}\Programs\QuotaTray
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=QuotaTray-Setup-{#AppVersion}-x64
SetupIconFile=..\QuotaTray\Assets\QuotaTray.ico
UninstallDisplayIcon={app}\QuotaTray.exe
UninstallDisplayName=Quota Tray
LicenseFile=..\..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; A running copy is closed before files are replaced or removed (see [Code]).
CloseApplications=no

[Tasks]
Name: "startup"; Description: "Start Quota Tray when I sign in to Windows"
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#AppDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Quota Tray"; Filename: "{app}\QuotaTray.exe"
Name: "{autodesktop}\Quota Tray"; Filename: "{app}\QuotaTray.exe"; Tasks: desktopicon

[Registry]
; The same Run value the app's Launch at Login switch reads and writes (Services/LaunchAtLogin.cs).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "QuotaTray"; ValueData: """{app}\QuotaTray.exe"""; Tasks: startup

[Run]
Filename: "{app}\QuotaTray.exe"; Description: "Open Quota Tray"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// The tray app has no window to close politely, and it only holds caches it can rebuild, so end it.
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM QuotaTray.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Launch at Login may have been turned on from the app itself, so remove the Run value either way.
  // Settings, caches, and logs in %LOCALAPPDATA%\QuotaTray stay, so a reinstall keeps them.
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'QuotaTray');
end;
