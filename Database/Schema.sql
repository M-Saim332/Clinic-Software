-- ============================================================
--  Clinic Management System -- Database Schema
--  SQL Server Express 2019/2022
--  Run this script on the Doctor's PC (server) once.
-- ============================================================

USE master;
GO

-- Create the database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ClinicDB')
BEGIN
    CREATE DATABASE ClinicDB;
END
GO

USE ClinicDB;
GO

-- ============================================================
--  TABLES
-- ============================================================

-- Users (must exist first due to FK in Prescriptions)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
CREATE TABLE Users (
    UserID       INT IDENTITY(1,1) PRIMARY KEY,
    Username     VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    Role         VARCHAR(20)  NOT NULL CHECK (Role IN ('Doctor', 'Receptionist')),
    FullName     VARCHAR(150),
    IsActive     BIT NOT NULL DEFAULT 1,
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Patients' AND xtype='U')
CREATE TABLE Patients (
    PatientID      INT IDENTITY(1,1) PRIMARY KEY,
    Name           VARCHAR(150) NOT NULL,
    DateOfBirth    DATE,
    Age            INT,
    Gender         VARCHAR(10)  CHECK (Gender IN ('Male', 'Female', 'Other')),
    Contact        VARCHAR(50),
    Address        VARCHAR(255),
    MedicalHistory NVARCHAR(MAX),
    CreatedAt      DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedAt      DATETIME NOT NULL DEFAULT GETDATE()
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Medicines' AND xtype='U')
CREATE TABLE Medicines (
    MedicineID   INT IDENTITY(1,1) PRIMARY KEY,
    Name         VARCHAR(150) NOT NULL,
    Stock        INT          NOT NULL DEFAULT 0,
    MinStock     INT          NOT NULL DEFAULT 10,   -- threshold for low-stock alert
    ExpiryDate   DATE,
    Price        DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    Manufacturer VARCHAR(150),
    Category     VARCHAR(100),
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Prescriptions' AND xtype='U')
CREATE TABLE Prescriptions (
    PrescriptionID INT IDENTITY(1,1) PRIMARY KEY,
    PatientID      INT NOT NULL FOREIGN KEY REFERENCES Patients(PatientID),
    DoctorID       INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
    VisitDate      DATETIME NOT NULL DEFAULT GETDATE(),
    Diagnosis      NVARCHAR(MAX),
    Notes          NVARCHAR(MAX),
    CreatedAt      DATETIME NOT NULL DEFAULT GETDATE()
);
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PrescriptionItems' AND xtype='U')
CREATE TABLE PrescriptionItems (
    PrescriptionItemID INT IDENTITY(1,1) PRIMARY KEY,
    PrescriptionID     INT NOT NULL FOREIGN KEY REFERENCES Prescriptions(PrescriptionID) ON DELETE CASCADE,
    MedicineID         INT NOT NULL FOREIGN KEY REFERENCES Medicines(MedicineID),
    Quantity           INT NOT NULL,
    Dosage             VARCHAR(150)
);
GO

-- ============================================================
--  INDEXES
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Patients_Name')
    CREATE INDEX IX_Patients_Name ON Patients(Name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Medicines_Name')
    CREATE INDEX IX_Medicines_Name ON Medicines(Name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Prescriptions_PatientID')
    CREATE INDEX IX_Prescriptions_PatientID ON Prescriptions(PatientID);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Prescriptions_VisitDate')
    CREATE INDEX IX_Prescriptions_VisitDate ON Prescriptions(VisitDate);
GO

-- ============================================================
--  SEED DATA — Default admin user (Doctor)
--  Password: Admin@123
--  BCrypt hash generated offline; change after first login.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (Username, PasswordHash, Role, FullName)
    VALUES (
        'admin',
        '$2a$11$u0LyGgHmhN2kTeoBK.a5m.FVHXHSUA/xHZFJ9tE1O4Oj4QvICWT.O',  -- Admin@123
        'Doctor',
        'System Administrator'
    );
END
GO

-- ============================================================
--  EXPIRY SAFETY TRIGGER
--  Prevent prescribing an expired medicine
-- ============================================================
IF OBJECT_ID('trg_PreventExpiredMedicine', 'TR') IS NOT NULL
    DROP TRIGGER trg_PreventExpiredMedicine;
GO

CREATE TRIGGER trg_PreventExpiredMedicine
ON PrescriptionItems
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN Medicines m ON i.MedicineID = m.MedicineID
        WHERE m.ExpiryDate IS NOT NULL AND m.ExpiryDate < CAST(GETDATE() AS DATE)
    )
    BEGIN
        RAISERROR('Cannot prescribe an expired medicine.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END
GO

PRINT 'ClinicDB schema created / verified successfully.';
GO
