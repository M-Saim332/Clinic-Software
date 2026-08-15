; ============================================================
;  Clinic Management System — SERVER Setup (Doctor PC)
;  Inno Setup 7
;  This installs the application on the Doctor PC which also
;  hosts the SQL Server database.
; ============================================================

#define AppName    "Clinic Management System"
#define AppVer     "1.0.0"
#define AppExe     "ClinicSystem.UI.exe"
#define Publisher  "Clinic IT"

[Setup]
AppId={{A1B2C3D4-0001-4A42-8D13-SERVER000001}
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher={#Publisher}
AppPublisherURL=https://github.com/M-Saim332/Clinic-Software

; Install to Program Files
DefaultDirName={autopf}\ClinicManagementSystem
DefaultGroupName={#AppName}

; Output
OutputDir=Installer
OutputBaseFilename=ClinicSetup_Server
SetupIconFile=Assets\avalonia-logo.ico

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
DiskSpanning=no

; Requires admin (needed for Program Files install)
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

; Wizard appearance
WizardStyle=modern
WizardSizePercent=120

; Uninstall
Uninstallable=yes
UninstallDisplayName={#AppName} (Server)
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

; ── Database scripts ─────────────────────────────────────────────────────────
Source: "Database\Schema.sql";                    DestDir: "{app}\Database"; Flags: ignoreversion
Source: "Database\Migration_AddDiscountRefunds.sql"; DestDir: "{app}\Database"; Flags: ignoreversion
Source: "Database\TestData.sql";                  DestDir: "{app}\Database"; Flags: ignoreversion
Source: "Database\GenerateMockTransactions.sql";  DestDir: "{app}\Database"; Flags: ignoreversion

; ── Default appsettings (placeholder → triggers DB Setup screen at first run) 
Source: "Installer\appsettings_server.json"; DestDir: "{app}"; DestName: "appsettings.json"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";     Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Launch app after install
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; WorkingDir: "{app}"

[Messages]
FinishedLabel=Setup finished!%n%nNEXT STEPS (Doctor PC / Server):%n%n1. Open SQL Server Management Studio (SSMS)%n2. Connect to your SQL Server instance%n3. Open and run: {app}\Database\Schema.sql%n4. Launch the app - a setup screen will guide you through connection configuration.
