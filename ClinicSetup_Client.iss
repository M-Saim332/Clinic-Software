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
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

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

; ── Default appsettings is generated via [Code] section

[Icons]
Name: "{group}\{#AppName}";         Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Flags: runmaximized
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";    Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon; Flags: runmaximized

[Run]
; Launch app after install (elevated)
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser; WorkingDir: "{app}"

[Messages]
FinishedLabel=Setup finished!%n%nNEXT STEPS (Reception PC / Client):%n%n1. The application will launch.%n2. You are connected to the main server.

[Code]
var
  DbPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  DbPage := CreateInputQueryPage(wpSelectTasks,
    'Database Configuration', 'Configure SQL Server Connection',
    'Please specify the Doctor PC SQL Server connection details. SQL Authentication is required for remote connections.');
  
  DbPage.Add('Server Name / IP Address:', False);
  DbPage.Add('Database Name:', False);
  DbPage.Add('SQL Username:', False);
  DbPage.Add('SQL Password:', True);
  
  DbPage.Values[0] := '192.168.100.50,1433';
  DbPage.Values[1] := 'ClinicDB';
  DbPage.Values[2] := 'sa';
  DbPage.Values[3] := 'Admin@123';
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
