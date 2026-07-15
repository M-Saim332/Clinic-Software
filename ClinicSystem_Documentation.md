# Clinic Management System — Software Requirements & Architecture Documentation

**Project Name:** Clinic Management System (new build)
**Reference System:** Ans Medicine — Pharmacy & Inventory Management System (legacy WinForms app, used as UX/functional reference only)
**Target Environment:** Windows Desktop, 2 networked PCs (Doctor + Reception), offline LAN
**Stack:** C# (.NET 8+), Avalonia UI, SQL Server Express

---


## 1. Executive Summary

This document specifies the new Clinic Management System, built to replace informal/manual record-keeping with a desktop application shared between a doctor's PC and a reception PC on the same local network. It draws its **functional workflow patterns** (form lifecycle, list/search overlays, master-data registries) from an older reference system, "Ans Medicine," while using a **modern cross-platform tech stack** and a **simplified, clinic-specific data model** (patients, medicines, prescriptions — not full pharmacy sales/purchase/distributor logistics).

The system must run **fully offline**, with no dependency on the internet, since the clinic's own LAN is available but should not be treated as a substitute for cloud connectivity.

---

## 2. Reference System Analysis (Ans Medicine — Legacy)

The legacy system is a pharmacy/inventory management tool. It is **not** being ported directly — it's used here as a source of proven UX patterns. Key elements extracted for reuse:

### 2.1 What we are carrying over
- **Standardized form lifecycle** (state machine below) — proven, low-error-rate pattern for data-entry staff.
- **List/Find overlay pattern** — inline searchable grid triggered from any registry form.
- **Master data registry concept** — reusable pattern for Patients, Medicines, Users instead of the legacy's Company/Product/Customer/Supplier.
- **Menu-driven navigation** structure, simplified for clinic scope.

### 2.2 What we are NOT carrying over
- Sales/Purchase ledger engine, batch/expiry-driven invoicing, Profit & Loss reporting, tax/discount computation — these belong to a pharmacy retail/distributor business model, not a clinic's patient-and-prescription workflow.
- WinForms + N-Tier legacy architecture — replaced with Avalonia + a simpler layered structure appropriate for this project's scope.
- MDI (Multiple Document Interface) windowing and legacy color-coded modules (amber/green/navy/yellow per module) — replaced with a single modern Fluent-themed interface.

### 2.3 Legacy Form State Machine (adapted for the new system)

```
                     +---------------------------------------+
                     |               VIEW MODE                |
                     |  - Fields disabled / Read-Only         |
                     |  - New, Edit, Delete, Find, List: ON   |
                     |  - Save, Cancel: OFF                   |
                     +---------------------------------------+
                        /                                 \
           On Click    /                                   \   On Click
           "New"      /                                     \  "Edit"
                     v                                       v
+---------------------------------------+   +---------------------------------------+
|               ADD MODE                 |   |               EDIT MODE               |
| - Fields unlocked/Cleared              |   | - Fields unlocked/Retain values        |
| - Primary Key input ready              |   | - Primary Key locked                   |
| - Save, Cancel: ON                     |   | - Save, Cancel: ON                      |
| - New, Edit, Delete, Find, List: OFF   |   | - New, Edit, Delete, Find, List: OFF   |
+---------------------------------------+   +---------------------------------------+
            \                                                   /
             \_______________ On Click "Save" / "Cancel" ______/
                             v
                +---------------------------------------+
                |          RETURN TO VIEW MODE            |
                |     (Persist Context or Rollback)       |
                +---------------------------------------+
```

**Action panel layout (kept from legacy, applies to every registry form — Patients, Medicines, Users):**
- **Row 1 (Mutation):** `New` | `Edit` | `Delete` | `Save` | `Cancel`
- **Row 2 (Lookup):** `Find` | `List` | `Close`

---

## 3. New System Requirements

### 3.1 Functional Scope

| Module | Purpose |
|---|---|
| **Patients Registry** | Add/edit/search patient demographic and contact info, medical history |
| **Medicines Registry** | Track medicine stock, expiry, price, manufacturer |
| **Prescriptions / Visits** | Record a doctor's visit, diagnosis notes, medicines prescribed with quantity/dosage |
| **Users** | Login accounts for Doctor and Receptionist roles, with role-based access |
| **Reports (basic)** | Patient list, medicine stock list, expired/low-stock medicine alerts, visit history per patient |

Explicitly **out of scope** for this version (unlike the legacy pharmacy system): sales invoicing, purchase ledgers, supplier/company management, tax and discount computation, profit & loss reporting. These can be considered later only if the clinic's needs expand toward retail pharmacy operations.

### 3.2 Non-Functional Requirements

- Must run **fully offline** — no internet dependency.
- Must support **two PCs on the same LAN** reading/writing the same live data.
- Must have a **modern UI** (not the dated legacy WinForms look).
- Must be installable by non-technical staff with a single installer per machine.
- Zero recurring software cost (no cloud subscription, no paid licenses).
- Data must survive a single machine failure where reasonably possible (backup strategy required — see §6.4).

---

## 4. Technology Stack

| Layer | Choice | Rationale |
|---|---|---|
| **UI Framework** | Avalonia UI (Fluent theme) | Cross-platform — team is split Windows/Mac; same C#/XAML model as WPF; modern look out of the box |
| **Language** | C# / .NET 8+ | Team's existing skillset |
| **Database** | SQL Server Express | Free; supports concurrent LAN access from multiple PCs; team already knows SQL Server |
| **Data Access** | Dapper | Lightweight, database-agnostic, matches team's existing experience |
| **Installer** | Inno Setup / MSIX / WiX | Free, produces a native Windows installer |

**Total licensing cost: $0.**

### 4.1 Why not the legacy stack (WinForms + N-Tier)?
WinForms is Windows-only, which doesn't fit a team split across Windows and Mac. Avalonia preserves the same development experience (XAML, C#, MVVM) while running natively on both platforms during development, and still produces a native Windows installer for the clinic.

### 4.2 Why not cloud storage?
Considered and explicitly rejected for this version:
- Adds a recurring cost where the LAN approach has none.
- Introduces a new single point of failure (internet outage) that the current LAN design doesn't have.
- Patient data would leave the building unnecessarily.
- No current requirement for remote access or multi-branch sync — the trigger conditions for needing cloud don't apply here.

---

## 5. System Architecture

### 5.1 Network Topology (2 PCs, LAN, Offline)

```
┌───────────────────────┐        LAN (no internet needed)        ┌───────────────────────┐
│     Doctor's PC        │ <────────────────────────────────────> │  Receptionist's PC     │
│     (SERVER)           │                                        │     (CLIENT)           │
│                        │                                        │                        │
│  • SQL Server Express  │                                        │  • Avalonia App only   │
│  • Holds the .mdf file │                                        │  • No DB installed     │
│  • Runs Avalonia App   │                                        │  • Connects via LAN IP │
│    too                 │                                        │    e.g. 192.168.1.5    │
└───────────────────────┘                                        └───────────────────────┘
```

- **One shared database**, not two synced databases — avoids conflict-resolution complexity.
- The doctor's PC was chosen as the server because it is expected to stay powered on throughout clinic hours. **This assumption should be confirmed operationally** — if reception hours extend beyond the doctor's presence, the database becomes unreachable during those hours.

### 5.2 Server-Side Setup (Doctor's PC)
1. Install SQL Server 2019/2022 Express.
2. Enable **TCP/IP** protocol in SQL Server Configuration Manager; restart the SQL Server service.
3. Enable the **SQL Server Browser** service.
4. Open **port 1433** in Windows Firewall (inbound rule).
5. Assign a **static local IP** (or DHCP reservation) to this PC on the router.

### 5.3 Client-Side Setup (Receptionist's PC)
- Install the Avalonia app only — no database software required.
- Connection string points to the server's LAN IP:
  ```
  Server=192.168.1.5\SQLEXPRESS;Database=ClinicDB;Trusted_Connection=True;
  ```

### 5.4 Solution Structure

```
ClinicSystem/
├── ClinicSystem.Data/     → Dapper repositories, connection/session logic
├── ClinicSystem.Core/     → Domain models, validation, business rules
├── ClinicSystem.UI/       → Avalonia views, viewmodels, Fluent theme
└── Database/
    └── Schema.sql         → Full table creation script
```

---

## 6. Data Architecture

### 6.1 Core Schema

```sql
CREATE TABLE Patients (
    PatientID INT IDENTITY PRIMARY KEY,
    Name VARCHAR(150) NOT NULL,
    Age INT,
    Gender VARCHAR(10),
    Contact VARCHAR(50),
    Address VARCHAR(255),
    MedicalHistory TEXT
);

CREATE TABLE Medicines (
    MedicineID INT IDENTITY PRIMARY KEY,
    Name VARCHAR(150) NOT NULL,
    Stock INT DEFAULT 0,
    ExpiryDate DATE,
    Price DECIMAL(10, 2) DEFAULT 0.00,
    Manufacturer VARCHAR(150)
);

CREATE TABLE Users (
    UserID INT IDENTITY PRIMARY KEY,
    Username VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    Role VARCHAR(20) NOT NULL -- 'Doctor' or 'Receptionist'
);

CREATE TABLE Prescriptions (
    PrescriptionID INT IDENTITY PRIMARY KEY,
    PatientID INT FOREIGN KEY REFERENCES Patients(PatientID),
    DoctorID INT FOREIGN KEY REFERENCES Users(UserID),
    VisitDate DATETIME DEFAULT GETDATE(),
    Notes TEXT
);

CREATE TABLE PrescriptionItems (
    PrescriptionItemID INT IDENTITY PRIMARY KEY,
    PrescriptionID INT FOREIGN KEY REFERENCES Prescriptions(PrescriptionID),
    MedicineID INT FOREIGN KEY REFERENCES Medicines(MedicineID),
    Quantity INT NOT NULL,
    Dosage VARCHAR(100)
);
```

### 6.2 Referential Integrity Rules (adapted from legacy)

1. **Orphan Prevention:** A `PrescriptionItem` cannot reference a `MedicineID` that doesn't exist in `Medicines`; a `Prescription` cannot reference a nonexistent `PatientID` or `DoctorID`.
2. **Expiry Safety Rule:** Any medicine with `ExpiryDate <= current date` should be flagged/excluded from being prescribed via the UI layer (application-level check, plus optionally a database trigger).
3. **Low Stock Rule:** Medicines below their defined safe stock threshold should surface in a dedicated report/alert (legacy equivalent: `Purchase Demand Report`).

### 6.3 Business Logic Notes
Unlike the legacy system's sales/purchase pricing formulas (gross amount, discount %, tax overhead), this system has no sales-transaction math — its core logic is simpler: stock decrement on prescribing, expiry/stock alerts, and visit history tracking. Pricing fields on `Medicines` are for reference/reporting only unless the clinic later requires billing.

### 6.4 Backup Strategy
Since the doctor's PC holds the only copy of the `.mdf` database file, a regular backup routine (e.g., a scheduled SQL Server backup job to an external drive, or periodic manual copy) is required. This is an open item to finalize before go-live.

---

## 7. UI/UX Design Direction

### 7.1 What changes from the legacy look
- No MDI windowing, no per-module color coding (amber/green/navy/yellow).
- No classic 8.25pt/11pt Microsoft Sans Serif grid styling.
- Replaced with a **single modern Fluent-themed Avalonia interface** — consistent light/dark theme, Segoe UI or system default, clean spacing, no color-per-module scheme.

### 7.2 What stays the same (functional pattern only, restyled)
- The **New / Edit / Delete / Save / Cancel** and **Find / List / Close** action pattern on every registry screen (Patients, Medicines, Users) — this is a proven, low-error workflow for non-technical staff and is being restyled, not redesigned.
- The **List overlay** search-and-select pattern for quickly finding an existing record.

### 7.3 Navigation Structure (simplified from legacy)

```
[Clinic System Main Window]
 │
 ├── Registries ─────► [Patients] [Medicines] [Users]
 ├── Prescriptions ──► [New Visit / Prescription] [Visit History]
 ├── Reports ────────► [Patient List] [Medicine Stock] [Expired/Low Stock] [Visit History by Patient]
 ├── Utilities ──────► [Change Password] [Backup Database]
 └── Exit
```

---

## 8. Build Plan

1. **Network setup** — install and verify SQL Server Express connectivity between both PCs before any app code is written.
2. **Database schema** — create and run `Schema.sql`.
3. **Solution scaffolding** — set up the 3-project structure in a shared Git repository.
4. **Patients module end-to-end** — full CRUD, tested live from both PCs over LAN.
5. **Repeat pattern** for Medicines, then Prescriptions.
6. **Apply Fluent theme** across all screens.
7. **Package and test the installer on real Windows hardware** before delivering to the clinic.

---

## 9. Cost Summary

| Item | Cost |
|---|---|
| C# / .NET SDK | Free |
| Avalonia UI | Free (open source) |
| SQL Server Express | Free |
| Visual Studio Community | Free (student/individual use) |
| JetBrains Rider | Free (via GitHub Student Pack) |
| Inno Setup | Free |
| **Total** | **$0** |

---

## 10. Open Items / Risks

- [ ] Confirm the doctor's PC will realistically stay powered on for all hours the receptionist works.
- [ ] Finalize backup routine for the doctor's PC database file.
- [ ] Decide final installer tool (Inno Setup vs MSIX vs WiX).
- [ ] Decide whether billing/pricing features are needed later (would require reintroducing legacy-style sales logic).
- [ ] Confirm role-based access rules for Doctor vs Receptionist (e.g., should reception be able to edit medical history?).
