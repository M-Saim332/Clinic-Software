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

; ── Default appsettings is generated via [Code] section

[Icons]
Name: "{group}\{#AppName}";     Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Launch app after install
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; WorkingDir: "{app}"

[Messages]
FinishedLabel=Setup finished!%n%nNEXT STEPS (Doctor PC / Server):%n%n1. Open SQL Server Management Studio (SSMS)%n2. Connect to your SQL Server instance%n3. Open and run: {app}\Database\Schema.sql%n4. Launch the app.

[Code]
var
  DbPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  DbPage := CreateInputQueryPage(wpSelectTasks,
    'Database Configuration', 'Configure SQL Server Connection',
    'Please specify your SQL Server connection details. If using Windows Authentication, leave Username and Password blank.');
  
  DbPage.Add('Server Name / IP Address:', False);
  DbPage.Add('Database Name:', False);
  DbPage.Add('SQL Username (leave blank for Windows Auth):', False);
  DbPage.Add('SQL Password (leave blank for Windows Auth):', True);
  
  DbPage.Values[0] := '(local)\SQLEXPRESS';
  DbPage.Values[1] := 'ClinicDB';
  DbPage.Values[2] := '';
  DbPage.Values[3] := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  JsonContent: String;
  ConnString: String;
  Server, Db, User, Pass: String;
begin
  if CurStep = ssPostInstall then
  begin
    Server := DbPage.Values[0];
    Db := DbPage.Values[1];
    User := DbPage.Values[2];
    Pass := DbPage.Values[3];
    
    if (User = '') and (Pass = '') then
      ConnString := 'Server=' + Server + ';Database=' + Db + ';Integrated Security=True;TrustServerCertificate=True;'
    else
      ConnString := 'Server=' + Server + ';Database=' + Db + ';User Id=' + User + ';Password=' + Pass + ';TrustServerCertificate=True;';
      
    StringChangeEx(ConnString, '\', '\\', True);
    
    JsonContent := '{' + #13#10 +
                   '  "ConnectionStrings": {' + #13#10 +
                   '    "ClinicDB": "' + ConnString + '"' + #13#10 +
                   '  }' + #13#10 +
                   '}';
                   
    SaveStringToFile(ExpandConstant('{app}\appsettings.json'), JsonContent, False);
  end;
end;
