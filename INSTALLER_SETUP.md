# Clinic Management System — Installer Build Setup

## ✓ Setup Complete

This document summarizes the installer build system that's now ready to use.

---

## Files Created/Updated

### 1. **ClinicSetup_Server.iss** (Inno Setup script for Doctor PC)
- **Purpose**: Creates `ClinicSetup_Server.exe` installer
- **Target**: Doctor PC (hosts the database)
- **Includes**:
  - Application files (self-contained .NET 10)
  - Database: `Schema.sql`, `Migration_AddDiscountRefunds.sql`, `TestData.sql`, `GenerateMockTransactions.sql`
  - Default config: `appsettings.json` → `(local)\SQLEXPRESS`
- **Post-Install**: Displays instructions to run Schema.sql in SSMS

### 2. **ClinicSetup_Client.iss** (Inno Setup script for Reception PC)
- **Purpose**: Creates `ClinicSetup_Client.exe` installer
- **Target**: Reception PC (no database)
- **Includes**:
  - Application files (self-contained .NET 10)
  - Default config: `appsettings.json` → `DOCTOR-PC\SQLEXPRESS`
- **Post-Install**: Displays instructions for entering Doctor PC IP

### 3. **build_installers.ps1** (Build orchestration script)
- **Purpose**: One-command build for both installers
- **Process**:
  1. Runs `dotnet publish` (self-contained, Release, win-x64)
  2. Cleans up `appsettings.local.json` from output
  3. Compiles `ClinicSetup_Server.iss` → `Installer\ClinicSetup_Server.exe`
  4. Compiles `ClinicSetup_Client.iss` → `Installer\ClinicSetup_Client.exe`
  5. Reports file sizes and next steps
- **Features**:
  - Colorized output with status indicators
  - Error checking at each step
  - `-NoPublish` flag to skip dotnet publish (for testing)
  - `-Verbose` flag for detailed output

### 4. **Installer/appsettings_server.json**
- Server-specific configuration
- Database hint: `(local)\SQLEXPRESS`
- Copied into installer during build

### 5. **Installer/appsettings_client.json**
- Client-specific configuration  
- Database hint: `DOCTOR-PC\SQLEXPRESS` (placeholder for setup screen)
- Copied into installer during build

---

## How It Works

```
┌─ dotnet publish (self-contained)
│  └─→ publish/  (all app files + .NET 10 runtime)
│
├─ ClinicSetup_Server.iss
│  └─→ Uses: publish/* + Database/*.sql + Installer/appsettings_server.json
│      └─→ Installer/ClinicSetup_Server.exe
│
└─ ClinicSetup_Client.iss
   └─→ Uses: publish/* + Installer/appsettings_client.json  
       └─→ Installer/ClinicSetup_Client.exe
```

---

## Usage

### Step 1: Publish the Application
```powershell
# From project root:
.\build_installers.ps1
```

This command:
- Publishes the app
- Compiles both installers
- Creates `Installer/ClinicSetup_Server.exe` and `Installer/ClinicSetup_Client.exe`

**Output**:
```
========================================
  BUILD COMPLETE
========================================

  Doctor PC  (Server): Installer\ClinicSetup_Server.exe  (250.5 MB)
  Reception  (Client): Installer\ClinicSetup_Client.exe  (250.3 MB)
```

### Step 2: Deploy Server
1. Copy `ClinicSetup_Server.exe` to Doctor PC
2. Run installer
3. When app starts: Database Setup screen appears
4. Use Windows Auth, Server = `(local)\SQLEXPRESS`
5. After app launches, open SSMS and run: `C:\Program Files\ClinicManagementSystem\Database\Schema.sql`

### Step 3: Deploy Client
1. Copy `ClinicSetup_Client.exe` to Reception PC
2. Run installer
3. When app starts: Database Setup screen appears (no server pre-filled)
4. Enter Doctor PC IP address (e.g., `192.168.1.100\SQLEXPRESS`)
5. Test connection and save

---

## Prerequisites

- **.NET 10 SDK** installed (for `dotnet publish`)
- **Inno Setup 7** installed at: `C:\Program Files\Inno Setup 7\ISCC.exe`
  - Download: https://jrsoftware.org/isdl.php

---

## Key Design Decisions

1. **Self-Contained Runtime**:
   - Both installers bundle .NET 10
   - Client PCs need NO .NET installation
   - Larger .exe files (~250 MB) but simpler deployment

2. **Separate AppSettings**:
   - Server gets `(local)\SQLEXPRESS` as default hint
   - Client gets `DOCTOR-PC\SQLEXPRESS` as default hint
   - Users can override at first launch via DB Setup screen

3. **SQL Files Only on Server**:
   - Server installer includes all `.sql` files (Schema, TestData, Migrations, MockData)
   - Client installer is lean (no SQL files)
   - Reduces client installer size

4. **Database Setup Screen**:
   - Both installers trigger setup screen on first launch (no valid `appsettings.local.json`)
   - Users can test connection before saving
   - Safer than hard-coding a single connection string

---

## Troubleshooting

### "Inno Setup 7 not found"
- Install from: https://jrsoftware.org/isdl.php
- Ensure it's installed at: `C:\Program Files\Inno Setup 7`

### "dotnet publish failed"
- Fix build errors in your project
- Run: `dotnet build -c Release` to identify issues
- Then retry: `.\build_installers.ps1`

### Installer runs but app won't start
- Check `{app}\appsettings.json` exists
- Verify connection string is correct
- Review app logs in: `%LOCALAPPDATA%\ClinicManagementSystem`

### Client can't connect to Server
- Ensure both PCs are on same network
- Check firewall allows SQL Server traffic (TCP 1433)
- Verify Server PC IP address is correct
- From Client PC, test: `Test-NetConnection DOCTOR-PC -Port 1433`

---

## Files Summary

| File | Purpose | Type |
|---|---|---|
| `ClinicSetup_Server.iss` | Server installer script | Inno Setup |
| `ClinicSetup_Client.iss` | Client installer script | Inno Setup |
| `build_installers.ps1` | Build orchestration | PowerShell |
| `Installer/appsettings_server.json` | Server default config | JSON |
| `Installer/appsettings_client.json` | Client default config | JSON |

---

## Next Steps

1. **Verify build**:
   ```powershell
   .\build_installers.ps1
   ```

2. **Test installers** (optional):
   - Install on two test PCs
   - Verify Database Setup screen appears
   - Test connection flow

3. **Deploy to production**:
   - Copy both `.exe` files to USB or share
   - Distribute to Doctor PC and Reception PC
   - Follow on-screen instructions

---

Generated: 2026-08-16
