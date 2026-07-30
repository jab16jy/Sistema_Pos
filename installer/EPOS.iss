; Inno Setup script for EPOS (Sistema POS) — builds EPOS-setup.exe.
; The release process publishes a fresh, self-contained single-file executable to build_output.
#define MyAppName "EPOS"
#ifndef MyAppVersion
  #define MyAppVersion "1.22.0"
#endif
#define MyAppPublisher "EPOS"
#define MyAppExeName "EPOS.exe"

[Setup]
AppId={{8F1B7E2B-6C1E-4B0A-9C8B-EPOS10000001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=EPOS-setup
SetupIconFile=..\logo\AppIcon.ico
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "..\build_output\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent
