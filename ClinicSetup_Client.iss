; ============================================================
;  Clinic Management System — CLIENT Setup (Reception PC)
;  Inno Setup 7
;  This installs only the application on the Reception PC.
;  The database lives on the Doctor PC over LAN.
; ============================================================

#define AppName    "Clinic Management System"
#define AppVer     "1.0.0"
#define AppExe     "ClinicSystem.UI.exe"
#define Publisher  "Clinic IT"

[Setup]
AppId={{A1B2C3D4-0002-4A42-8D13-CLIENT000002}
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher={#Publisher}
AppPublisherURL=https://github.com/M-Saim332/Clinic-Software

; Install to Program Files
DefaultDirName={autopf}\ClinicManagementSystem
DefaultGroupName={#AppName}

; Output
OutputDir=Installer
OutputBaseFilename=ClinicSetup_Client
SetupIconFile=Assets\avalonia-logo.ico

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
DiskSpanning=no

; Requires admin
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

; Wizard appearance
WizardStyle=modern
WizardSizePercent=120

; Uninstall
Uninstallable=yes
UninstallDisplayName={#AppName} (Client)
UninstallDisplayIcon={app}\{#AppExe}

; Min Windows version: Windows 10
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; ── Application (self-contained, no .NET runtime needed on target) ──────────
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── Default appsettings (placeholder → triggers DB Setup screen at first run)
; NOTE: We do NOT include Schema.sql — the database is on the Doctor PC.
Source: "Installer\appsettings_client.json"; DestDir: "{app}"; DestName: "appsettings.json"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";       Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Launch app after install
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; WorkingDir: "{app}"

[Messages]
FinishedLabel=Setup finished!%n%nNEXT STEPS (Reception PC / Client):%n%n1. Ask the Doctor PC admin for the server IP address (e.g. 192.168.1.100)%n2. Launch the app - a setup screen will appear automatically%n3. Enter the server IP and SQL credentials%n4. Click Test Connection, then Save and Continue%n%nBoth PCs must be on the same LAN for the connection to work.
