[Setup]
AppId={{5A9A7B3C-726F-4A42-8D13-4011B1B8A5A5}
AppName=Clinic Management System
AppVersion=1.0.0
AppPublisher=Clinic IT
DefaultDirName={autopf}\ClinicManagementSystem
DefaultGroupName=Clinic Management System
OutputDir=Installer
OutputBaseFilename=ClinicSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application files (requires running `dotnet publish -c Release -r win-x64 --self-contained false -o publish` first)
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Database schema for manual setup on the server PC
Source: "Database\Schema.sql"; DestDir: "{app}\Database"; Flags: ignoreversion

[Icons]
Name: "{group}\Clinic Management System"; Filename: "{app}\ClinicSystem.UI.exe"
Name: "{autodesktop}\Clinic Management System"; Filename: "{app}\ClinicSystem.UI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ClinicSystem.UI.exe"; Description: "{cm:LaunchProgram,Clinic Management System}"; Flags: nowait postinstall skipifsilent
