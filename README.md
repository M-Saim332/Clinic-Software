# Clinic Management System

A desktop clinic management application built with **C# + Avalonia UI** and **SQL Server**.  
Targets **Windows** for deployment (client installer), with cross-platform development support on Windows and macOS.

---

## Table of Contents

- [Project Overview](#project-overview)
- [Architecture](#architecture)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Setup — macOS (Development)](#setup--macos-development)
- [Setup — Windows (Development)](#setup--windows-development)
- [Per-Machine Connection String](#per-machine-connection-string-important)
- [Running from Source](#running-from-source)
- [Building the Windows Installer](#building-the-windows-installer)
- [Default Credentials](#default-credentials)
- [Team Workflow](#team-workflow)
- [Troubleshooting](#troubleshooting)

---

## Project Overview

| Item | Value |
|------|-------|
| Language | C# 13 / .NET 10 |
| UI Framework | Avalonia UI 12 (cross-platform WPF-like) |
| Database | SQL Server (Express 2019/2022 on Windows, Docker on Mac) |
| ORM | Dapper (lightweight, raw SQL) |
| Pattern | MVVM + Dependency Injection (Microsoft.Extensions) |
| Final Target | Windows desktop (self-contained or .NET-dependent installer) |

---

## Architecture

```
ClinicSystem.slnx
├── ClinicSystem.Core          # Domain models & enums (no external deps)
│   ├── Models/
│   │   ├── Patient.cs
│   │   ├── User.cs
│   │   ├── Medicine.cs
│   │   ├── Prescription.cs
│   │   └── PrescriptionItem.cs
│   └── Enums/
│       └── UserRole.cs
│
├── ClinicSystem.Data          # Data access layer (Dapper + SQL Server)
│   ├── DatabaseSession.cs     # Connection factory + backup utility
│   └── Repositories/
│       ├── PatientRepository.cs
│       ├── MedicineRepository.cs
│       ├── PrescriptionRepository.cs
│       └── UserRepository.cs
│
├── ClinicSystem.UI            # Avalonia MVVM front-end
│   ├── App.axaml / App.axaml.cs   # DI container + app startup
│   ├── Views/                      # AXAML UI files
│   │   ├── LoginWindow
│   │   ├── MainWindow
│   │   ├── Patients/
│   │   ├── Medicines/
│   │   ├── Prescriptions/
│   │   ├── Reports/
│   │   └── Users/
│   ├── ViewModels/                 # MVVM ViewModels
│   │   ├── LoginViewModel.cs
│   │   ├── MainWindowViewModel.cs
│   │   ├── Patients/
│   │   ├── Medicines/
│   │   ├── Prescriptions/
│   │   ├── Reports/
│   │   └── Users/
│   ├── appsettings.json           # Placeholder — do NOT put real credentials here
│   └── appsettings.local.json     # ← YOUR machine's connection string (gitignored)
│
├── PwdReset                   # CLI utility to generate BCrypt hashes for password resets
├── Database/
│   └── Schema.sql             # Full DB schema + seed data (run once per machine)
└── ClinicSetup.iss            # Inno Setup script for Windows installer
```

---

## Features

| Module | Description |
|--------|-------------|
| **Login / Auth** | BCrypt-hashed passwords, Doctor / Receptionist roles |
| **Patient Registry** | Add, edit, search, delete patients |
| **Prescription** | Create prescriptions with medicine items; auto-decrements stock |
| **Visit History** | Browse all prescriptions per patient |
| **Medicine Registry** | Manage medicines, stock levels, expiry tracking |
| **User Management** | Doctor-only: manage staff accounts |
| **Reports** | Patient list, medicine stock, expired/low-stock alerts, all visits |
| **Database Backup** | One-click backup to `.bak` file via the toolbar |

---

## Prerequisites

### macOS (Development)

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Build & run |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest | SQL Server container |
| [Azure Data Studio](https://aka.ms/azuredatastudio) *(optional)* | Latest | DB GUI |
| Git | Any | Version control |

### Windows (Development / Client)

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Build & run |
| [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) | 2019 or 2022 | Local DB |
| [SQL Server Management Studio (SSMS)](https://aka.ms/ssmsfullsetup) *(recommended)* | Latest | DB GUI |
| Git | Any | Version control |

---

## Setup — macOS (Development)

### 1. Start SQL Server in Docker

```bash
docker run -e "ACCEPT_EULA=Y" \
           -e "MSSQL_SA_PASSWORD=YourStrong!Password" \
           -p 1433:1433 \
           --name sql2022 \
           -d mcr.microsoft.com/mssql/server:2022-latest
```

> **Tip:** Replace `YourStrong!Password` with your actual SA password.  
> Password must be 8+ chars with uppercase, lowercase, digit, and symbol.

Wait ~20 seconds for SQL Server to start, then verify:

```bash
docker ps  # should show sql2022 as "Up"
```

### 2. Run the Database Schema

```bash
docker exec -i sql2022 /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P 'YourStrong!Password' -C \
    < Database/Schema.sql
```

You should see: `ClinicDB schema created / verified successfully.`

### 3. Create Your Local Connection String File

```bash
# From the project root:
cat > appsettings.local.json << 'EOF'
{
  "ConnectionStrings": {
    "ClinicDB": "Server=localhost,1433;Database=ClinicDB;User Id=sa;Password=YourStrong!Password;TrustServerCertificate=True;"
  }
}
EOF
```

> This file is **gitignored** — it will never be committed or overwrite teammates' configs.

### 4. Clone, Build & Run

```bash
git clone <repo-url>
cd Clinic-Software
dotnet restore
dotnet run --project ClinicSystem.UI
```

---

## Setup — Windows (Development)

### 1. Install SQL Server Express

Download and install from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads  
Choose **Express** edition → **Basic** installation type.

During setup, note your **instance name** (usually `.\SQLEXPRESS` or `.`).

### 2. Run the Database Schema

Open **SSMS** or **Azure Data Studio**, connect to your instance, and run:

```
Database\Schema.sql
```

Or via command line:

```cmd
sqlcmd -S .\SQLEXPRESS -E -i Database\Schema.sql
```

(`-E` uses Windows Authentication — no password needed.)

### 3. Create Your Local Connection String File

Create a file called `appsettings.local.json` in the project root:

**Windows Authentication (recommended — no password):**
```json
{
  "ConnectionStrings": {
    "ClinicDB": "Server=.\\SQLEXPRESS;Database=ClinicDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

**SQL Authentication (if you set up an SA password):**
```json
{
  "ConnectionStrings": {
    "ClinicDB": "Server=.\\SQLEXPRESS;Database=ClinicDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
  }
}
```

> **Note:** Use `Server=.` if SQL Server is the default instance, or `Server=.\SQLEXPRESS` for Express.

### 4. Clone, Build & Run

```cmd
git clone <repo-url>
cd Clinic-Software
dotnet restore
dotnet run --project ClinicSystem.UI
```

---

## Per-Machine Connection String (IMPORTANT)

The `appsettings.json` file in the repo contains a **placeholder** — it is safe to commit.  
Each developer must create their own **`appsettings.local.json`** file (same folder) with their real connection string.

```
appsettings.json         ← committed to git (placeholder only)
appsettings.local.json   ← gitignored, YOUR machine only
```

The app loads both files at startup. `appsettings.local.json` takes priority and overrides `appsettings.json`.

**The file is in `.gitignore` — it will never be pushed, and pulling new code will never overwrite it.**

---

## Running from Source

```bash
# Debug (development)
dotnet run --project ClinicSystem.UI

# Or use the convenience batch file (Windows):
RunClinic.bat
```

---

## Building the Windows Installer

> **This step must be done on a Windows machine.**

### Step 1 — Publish the app

```cmd
dotnet publish ClinicSystem.UI.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -o publish
```

> Use `--self-contained true` if the client PC may not have .NET installed.

### Step 2 — Build the installer

1. Install [Inno Setup](https://jrsoftware.org/isinfo.php)
2. Open `ClinicSetup.iss` in Inno Setup Compiler
3. Press **Build → Compile** (or `Ctrl+F9`)
4. The installer will be generated at `Installer\ClinicSetup.exe`

### Step 3 — Client PC setup

On the client's Windows PC:

1. Install **SQL Server Express** (one-time, from the Microsoft website)
2. Run `Database\Schema.sql` in SSMS to create the database
3. Run `Installer\ClinicSetup.exe`
4. Create `appsettings.local.json` in the install directory (e.g. `C:\Program Files\ClinicManagementSystem\`) with the client's connection string
5. Launch from the Start Menu or Desktop shortcut

---

## Default Credentials

| Username | Password | Role |
|----------|----------|------|
| `admin` | `Admin@123` | Doctor |

> **Change this password immediately after first login** via the User Management screen.

---

## Team Workflow

### Adding a new developer to the team

1. Clone the repo
2. Create `appsettings.local.json` with your own DB connection string (see the [Setup](#setup--macos-development) sections above)
3. Run `Database/Schema.sql` once against your local SQL Server
4. `dotnet restore && dotnet run --project ClinicSystem.UI`

### Pushing code changes

```bash
git add .
git commit -m "your message"
git push
```

`appsettings.local.json`, `bin/`, `obj/`, `.vs/`, and `publish/` are all gitignored — they will never be accidentally committed.

### Pulling changes from teammates

```bash
git pull
```

Your `appsettings.local.json` is never touched by git pulls — it stays exactly as you set it.

---

## Troubleshooting

### "Connection string 'ClinicDB' not found"
→ You haven't created `appsettings.local.json`. See [Per-Machine Connection String](#per-machine-connection-string-important).

### "Cannot open server" / login error
→ Check that SQL Server is running and the connection string is correct (server name, port, credentials).

### macOS: Docker container not running
```bash
docker start sql2022
```

### Windows: SQL Server service not running
Open **Services** (`Win+R` → `services.msc`) → start **SQL Server (SQLEXPRESS)**.

### App crashes on startup with "InvalidOperationException: Connection already open"
→ Already fixed in this version. If you pulled old code, ensure `PrescriptionRepository.cs` does not call `conn.Open()` after `CreateConnection()`.

### Reset admin password
Run the `PwdReset` utility to generate a new BCrypt hash, then update the database manually:

```bash
dotnet run --project PwdReset
# Copy the hash printed between HASH_START: and :HASH_END
```

```sql
-- In SSMS / sqlcmd:
UPDATE Users SET PasswordHash = '<paste hash here>' WHERE Username = 'admin';
```