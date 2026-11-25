; ALICE — Advanced Literary & Creative Intelligence Studio
; Inno Setup 6 — wählbarer Installationspfad, keine Fremdkomponenten

#define MyAppName "ALICE"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ALICE Studio"
#define MyAppExeName "ALICE.App.exe"

[Setup]
AppId={{8A3B2C1D-4E5F-4A6B-8C9D-0E1F2A3B4C5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ALICE
DefaultGroupName=ALICE
DisableProgramGroupPage=no
OutputBaseFilename=ALICE-Setup
SetupIconFile=..\assets\alice.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0

[Languages]
Name: "deutsch"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\portable\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\portable\*"; DestDir: "{app}"; Excludes: "ALICE.App.exe"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "ALICE starten"; Flags: postinstall shellexec skipifsilent

[UninstallRun]
; Projekte in Dokumente\ALICE Projekte bewusst nicht löschen
