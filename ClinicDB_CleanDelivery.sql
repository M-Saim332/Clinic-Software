/*
 Clinic Management System — Clean Delivery Database
 Generated from the current application schema and migrations.

 This script creates/updates ClinicDB without development business data.
 It preserves only application bootstrap defaults required for first login.
 Change the default administrator password after the first sign-in.
*/
IF DB_ID(N'ClinicDB') IS NULL
    CREATE DATABASE [ClinicDB];
GO
USE [ClinicDB];
GO
GO
-- ============================================================
--  Clinic Management System -- Database Schema
--  SQL Server Express 2019/2022
--  This script is idempotent and can be safely run multiple times.
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
--  CREATE TABLES WITH IDEMPOTENT CHECKS
-- ============================================================

IF OBJECT_ID('Companies', 'U') IS NULL
BEGIN
    CREATE TABLE Companies (
        CompanyID INT IDENTITY(1,1) PRIMARY KEY,
        CCode     INT NOT NULL DEFAULT 0,
        Name      VARCHAR(150) NOT NULL,
        Address   VARCHAR(255),
        Phone     VARCHAR(50),
        Email     VARCHAR(150)
    );
END
ELSE
BEGIN
    IF COL_LENGTH('Companies', 'CCode') IS NULL
        ALTER TABLE Companies ADD CCode INT NOT NULL CONSTRAINT DF_Companies_CCode DEFAULT 0;
END
GO

IF OBJECT_ID('Suppliers', 'U') IS NULL
BEGIN
    CREATE TABLE Suppliers (
        SupplierID INT IDENTITY(1,1) PRIMARY KEY,
        SCode      INT NOT NULL DEFAULT 0,
        Name       VARCHAR(150) NOT NULL,
        Address    VARCHAR(255),
        Phone      VARCHAR(50),
        Email      VARCHAR(150),
        CNIC       NVARCHAR(50) NULL
    );
END
ELSE
BEGIN
    IF COL_LENGTH('Suppliers', 'CNIC') IS NULL ALTER TABLE Suppliers ADD CNIC NVARCHAR(50) NULL;
    IF COL_LENGTH('Suppliers', 'SCode') IS NULL ALTER TABLE Suppliers ADD SCode INT NOT NULL CONSTRAINT DF_Suppliers_SCode DEFAULT 0;
END
GO

IF OBJECT_ID('Products', 'U') IS NULL
BEGIN
    CREATE TABLE Products (
        ProductID        INT IDENTITY(1,1) PRIMARY KEY,
        Name              VARCHAR(150) NOT NULL,
        GenericName       VARCHAR(150),
        CompanyID         INT FOREIGN KEY REFERENCES Companies(CompanyID),
        CompanyName       VARCHAR(150),
        SupplierID        INT FOREIGN KEY REFERENCES Suppliers(SupplierID),
        SupplierName      VARCHAR(150),
        BatchNumber       VARCHAR(50),
        Type              VARCHAR(50),
        Category          VARCHAR(100),
        Rack              VARCHAR(50),
        ExpiryDate        DATE,
        Rate              DECIMAL(10,2) NOT NULL DEFAULT 0,
        PurchasePrice     DECIMAL(10,2) DEFAULT 0,
        SellingPrice      DECIMAL(10,2) DEFAULT 0,
        TabletsPerBox     INT NOT NULL DEFAULT 1,
        MinimumStockLevel INT DEFAULT 0,
        IsReturnable      BIT NOT NULL DEFAULT 1,
        IsActive          BIT NOT NULL DEFAULT 1
    );
END
ELSE
BEGIN
    IF COL_LENGTH('Products', 'GenericName') IS NULL ALTER TABLE Products ADD GenericName VARCHAR(150) NULL;
    IF COL_LENGTH('Products', 'CompanyName') IS NULL ALTER TABLE Products ADD CompanyName VARCHAR(150) NULL;
    IF COL_LENGTH('Products', 'SupplierID') IS NULL ALTER TABLE Products ADD SupplierID INT NULL FOREIGN KEY REFERENCES Suppliers(SupplierID);
    IF COL_LENGTH('Products', 'SupplierName') IS NULL ALTER TABLE Products ADD SupplierName VARCHAR(150) NULL;
    IF COL_LENGTH('Products', 'BatchNumber') IS NULL ALTER TABLE Products ADD BatchNumber VARCHAR(50) NULL;
    IF COL_LENGTH('Products', 'Type') IS NULL ALTER TABLE Products ADD Type VARCHAR(50) NULL;
    IF COL_LENGTH('Products', 'Category') IS NULL ALTER TABLE Products ADD Category VARCHAR(100) NULL;
    IF COL_LENGTH('Products', 'Rack') IS NULL ALTER TABLE Products ADD Rack VARCHAR(50) NULL;
    IF COL_LENGTH('Products', 'ExpiryDate') IS NULL ALTER TABLE Products ADD ExpiryDate DATE NULL;
    IF COL_LENGTH('Products', 'PurchasePrice') IS NULL ALTER TABLE Products ADD PurchasePrice DECIMAL(10,2) DEFAULT 0;
    IF COL_LENGTH('Products', 'SellingPrice') IS NULL ALTER TABLE Products ADD SellingPrice DECIMAL(10,2) DEFAULT 0;
    IF COL_LENGTH('Products', 'TabletsPerBox') IS NULL ALTER TABLE Products ADD TabletsPerBox INT NOT NULL DEFAULT 1;
    IF COL_LENGTH('Products', 'MinimumStockLevel') IS NULL ALTER TABLE Products ADD MinimumStockLevel INT DEFAULT 0;
    IF COL_LENGTH('Products', 'Rate') IS NULL ALTER TABLE Products ADD Rate DECIMAL(10,2) NOT NULL CONSTRAINT DF_Products_Rate DEFAULT 0;
    IF COL_LENGTH('Products', 'IsReturnable') IS NULL ALTER TABLE Products ADD IsReturnable BIT NOT NULL DEFAULT 1;
    IF COL_LENGTH('Products', 'IsActive') IS NULL ALTER TABLE Products ADD IsActive BIT NOT NULL DEFAULT 1;
END
GO

IF OBJECT_ID('Patients', 'U') IS NULL
BEGIN
    CREATE TABLE Patients (
        PatientID           INT IDENTITY(1,1) PRIMARY KEY,
        Name                VARCHAR(150) NOT NULL,
        Age                 INT,
        Gender              VARCHAR(10) CHECK (Gender IN ('Male', 'Female', 'Other')),
        Phone               VARCHAR(50),
        Address             VARCHAR(255),
        Diagnosis           TEXT,
        Prescription        TEXT,
        ConsultationFee     DECIMAL(10,2) DEFAULT 0,
        Discount            DECIMAL(10,2) DEFAULT 0,
        NextAppointmentDate DATE,
        NextAppointmentTime TIME,
        VisitStatus         VARCHAR(20) NULL,
        LastVisitDate       DATE NULL,
        CNIC                NVARCHAR(50) NULL,
        IsActive            BIT NOT NULL DEFAULT 1,
        PatientContext      NVARCHAR(20) NOT NULL DEFAULT 'Clinical',
        ReasonOfVisit       NVARCHAR(500) NULL
    );
END
ELSE
BEGIN
    IF COL_LENGTH('Patients', 'VisitStatus') IS NULL ALTER TABLE Patients ADD VisitStatus VARCHAR(20) NULL;
    IF COL_LENGTH('Patients', 'LastVisitDate') IS NULL ALTER TABLE Patients ADD LastVisitDate DATE NULL;
    IF COL_LENGTH('Patients', 'Age') IS NULL ALTER TABLE Patients ADD Age INT NULL;
    IF COL_LENGTH('Patients', 'Gender') IS NULL ALTER TABLE Patients ADD Gender VARCHAR(10) NULL;
    IF COL_LENGTH('Patients', 'Phone') IS NULL ALTER TABLE Patients ADD Phone VARCHAR(50) NULL;
    IF COL_LENGTH('Patients', 'Address') IS NULL ALTER TABLE Patients ADD Address VARCHAR(255) NULL;
    IF COL_LENGTH('Patients', 'Diagnosis') IS NULL ALTER TABLE Patients ADD Diagnosis TEXT NULL;
    IF COL_LENGTH('Patients', 'Prescription') IS NULL ALTER TABLE Patients ADD Prescription TEXT NULL;
    IF COL_LENGTH('Patients', 'ConsultationFee') IS NULL ALTER TABLE Patients ADD ConsultationFee DECIMAL(10,2) DEFAULT 0;
    IF COL_LENGTH('Patients', 'Discount') IS NULL ALTER TABLE Patients ADD Discount DECIMAL(10,2) DEFAULT 0;
    IF COL_LENGTH('Patients', 'NextAppointmentDate') IS NULL ALTER TABLE Patients ADD NextAppointmentDate DATE NULL;
    IF COL_LENGTH('Patients', 'NextAppointmentTime') IS NULL ALTER TABLE Patients ADD NextAppointmentTime TIME NULL;
    IF COL_LENGTH('Patients', 'CNIC') IS NULL ALTER TABLE Patients ADD CNIC NVARCHAR(50) NULL;
    IF COL_LENGTH('Patients', 'IsActive') IS NULL ALTER TABLE Patients ADD IsActive BIT NOT NULL DEFAULT 1;
    IF COL_LENGTH('Patients', 'PatientContext') IS NULL ALTER TABLE Patients ADD PatientContext NVARCHAR(20) NOT NULL CONSTRAINT DF_Patients_Context DEFAULT 'Clinical';
    IF COL_LENGTH('Patients', 'ReasonOfVisit') IS NULL ALTER TABLE Patients ADD ReasonOfVisit NVARCHAR(500) NULL;
    IF COL_LENGTH('Patients', 'Phone') IS NOT NULL AND COL_LENGTH('Patients', 'Contact') IS NOT NULL
        EXEC('UPDATE Patients SET Phone = Contact WHERE Phone IS NULL AND Contact IS NOT NULL');
END
GO

IF OBJECT_ID('Users', 'U') IS NULL
BEGIN
    CREATE TABLE Users (
        UserID       INT IDENTITY(1,1) PRIMARY KEY,
        Username     VARCHAR(100) NOT NULL UNIQUE,
        PasswordHash VARCHAR(255) NOT NULL,
        Role         VARCHAR(20)  NOT NULL CHECK (Role IN ('Doctor', 'Receptionist', 'Admin', 'Pharmacist', 'Assistant')),
        FullName     VARCHAR(150) NULL,
        IsActive     BIT DEFAULT 1,
        Permissions  VARCHAR(1000) NULL,
        CreatedAt    DATETIME DEFAULT GETDATE(),
        Email        NVARCHAR(100) NULL,
        Phone        NVARCHAR(50) NULL,
        CNIC         NVARCHAR(50) NULL,
        Address      NVARCHAR(500) NULL,
        Gender       NVARCHAR(20) NULL,
        Qualification NVARCHAR(200) NULL,
        Designation  NVARCHAR(200) NULL,
        LicenseNumber NVARCHAR(100) NULL,
        DateOfBirth  DATETIME2 NULL,
        ProfilePicture VARBINARY(MAX) NULL,
        LastLogin    DATETIME2 NULL,
        UpdatedAt    DATETIME2 NULL,
        ForcePasswordChange BIT NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH('Users', 'Permissions') IS NULL ALTER TABLE Users ADD Permissions VARCHAR(1000) NULL;
    IF COL_LENGTH('Users', 'Email') IS NULL ALTER TABLE Users ADD Email NVARCHAR(100) NULL;
    IF COL_LENGTH('Users', 'Phone') IS NULL ALTER TABLE Users ADD Phone NVARCHAR(50) NULL;
    IF COL_LENGTH('Users', 'CNIC') IS NULL ALTER TABLE Users ADD CNIC NVARCHAR(50) NULL;
    IF COL_LENGTH('Users', 'Address') IS NULL ALTER TABLE Users ADD Address NVARCHAR(500) NULL;
    IF COL_LENGTH('Users', 'Gender') IS NULL ALTER TABLE Users ADD Gender NVARCHAR(20) NULL;
    IF COL_LENGTH('Users', 'Qualification') IS NULL ALTER TABLE Users ADD Qualification NVARCHAR(200) NULL;
    IF COL_LENGTH('Users', 'Designation') IS NULL ALTER TABLE Users ADD Designation NVARCHAR(200) NULL;
    IF COL_LENGTH('Users', 'LicenseNumber') IS NULL ALTER TABLE Users ADD LicenseNumber NVARCHAR(100) NULL;
    IF COL_LENGTH('Users', 'DateOfBirth') IS NULL ALTER TABLE Users ADD DateOfBirth DATETIME2 NULL;
    IF COL_LENGTH('Users', 'ProfilePicture') IS NULL ALTER TABLE Users ADD ProfilePicture VARBINARY(MAX) NULL;
    IF COL_LENGTH('Users', 'LastLogin') IS NULL ALTER TABLE Users ADD LastLogin DATETIME2 NULL;
    IF COL_LENGTH('Users', 'UpdatedAt') IS NULL ALTER TABLE Users ADD UpdatedAt DATETIME2 NULL;
    IF COL_LENGTH('Users', 'ForcePasswordChange') IS NULL ALTER TABLE Users ADD ForcePasswordChange BIT NOT NULL DEFAULT 0;
END
GO

IF OBJECT_ID('Appointments', 'U') IS NULL
BEGIN
    CREATE TABLE Appointments (
        AppointmentID      INT IDENTITY(1,1) PRIMARY KEY,
        AppointmentNo      VARCHAR(50) NOT NULL,
        PatientID          INT FOREIGN KEY REFERENCES Patients(PatientID),
        PatientName        VARCHAR(150),
        Phone              VARCHAR(50),
        CNIC               NVARCHAR(50) NULL,
        DoctorID           INT FOREIGN KEY REFERENCES Users(UserID),
        AppointmentDate    DATE NOT NULL,
        AppointmentTime    TIME NOT NULL,
        Reason             VARCHAR(255),
        Status             VARCHAR(20) NOT NULL DEFAULT 'Scheduled' CHECK (Status IN ('Scheduled', 'Checked-In', 'Completed', 'Cancelled', 'Missed')),
        Remarks            VARCHAR(255),
        CancellationReason VARCHAR(255),
        CreatedAt          DATETIME DEFAULT GETDATE(),
        Gender             NVARCHAR(20) NULL,
        Age                INT NULL
    );
END
ELSE
BEGIN
    IF COL_LENGTH('Appointments', 'Gender') IS NULL ALTER TABLE Appointments ADD Gender NVARCHAR(20) NULL;
    IF COL_LENGTH('Appointments', 'Age') IS NULL ALTER TABLE Appointments ADD Age INT NULL;
    IF COL_LENGTH('Appointments', 'Phone') IS NULL ALTER TABLE Appointments ADD Phone VARCHAR(50) NULL;
    IF COL_LENGTH('Appointments', 'CNIC') IS NULL ALTER TABLE Appointments ADD CNIC NVARCHAR(50) NULL;
    IF COL_LENGTH('Appointments', 'Remarks') IS NULL ALTER TABLE Appointments ADD Remarks VARCHAR(255) NULL;
    IF COL_LENGTH('Appointments', 'CancellationReason') IS NULL ALTER TABLE Appointments ADD CancellationReason VARCHAR(255) NULL;
    IF COL_LENGTH('Appointments', 'AppointmentNo') IS NULL ALTER TABLE Appointments ADD AppointmentNo VARCHAR(50) NULL;
    IF COL_LENGTH('Appointments', 'PatientName') IS NULL ALTER TABLE Appointments ADD PatientName VARCHAR(150) NULL;
    IF COL_LENGTH('Appointments', 'Reason') IS NULL ALTER TABLE Appointments ADD Reason VARCHAR(255) NULL;
END
GO

IF OBJECT_ID('Purchases', 'U') IS NULL
BEGIN
    CREATE TABLE Purchases (
        PurchaseID    INT IDENTITY(1,1) PRIMARY KEY,
        InvoiceNumber VARCHAR(50) NOT NULL,
        PurchaseDate  DATETIME DEFAULT GETDATE(),
        SupplierID    INT FOREIGN KEY REFERENCES Suppliers(SupplierID),
        SupplierName  VARCHAR(150),
        TotalAmount   DECIMAL(12,2) DEFAULT 0,
        CreatedBy     INT NULL FOREIGN KEY REFERENCES Users(UserID),
        CreatedByName NVARCHAR(150) NULL,
        IsPosted      BIT NOT NULL DEFAULT 0,
        PostedAt      DATETIME2 NULL,
        ATax          DECIMAL(5,2) NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH('Purchases', 'SupplierName') IS NULL ALTER TABLE Purchases ADD SupplierName VARCHAR(150) NULL;
    IF COL_LENGTH('Purchases', 'CreatedBy') IS NULL ALTER TABLE Purchases ADD CreatedBy INT NULL FOREIGN KEY REFERENCES Users(UserID);
    IF COL_LENGTH('Purchases', 'CreatedByName') IS NULL ALTER TABLE Purchases ADD CreatedByName NVARCHAR(150) NULL;
    IF COL_LENGTH('Purchases', 'IsPosted') IS NULL
    BEGIN
        ALTER TABLE Purchases ADD IsPosted BIT NOT NULL CONSTRAINT DF_Purchases_IsPosted DEFAULT 0;
        -- Mark existing purchases as posted so they don't double-count stock
        EXEC('UPDATE Purchases SET IsPosted = 1');
    END
    IF COL_LENGTH('Purchases', 'PostedAt') IS NULL ALTER TABLE Purchases ADD PostedAt DATETIME2 NULL;
    IF COL_LENGTH('Purchases', 'ATax') IS NULL ALTER TABLE Purchases ADD ATax DECIMAL(5,2) NOT NULL CONSTRAINT DF_Purchases_ATax DEFAULT 0;
END
GO

IF OBJECT_ID('PurchaseItems', 'U') IS NULL
BEGIN
    CREATE TABLE PurchaseItems (
        PurchaseItemID INT IDENTITY(1,1) PRIMARY KEY,
        PurchaseID     INT FOREIGN KEY REFERENCES Purchases(PurchaseID) ON DELETE CASCADE,
        ProductID      INT FOREIGN KEY REFERENCES Products(ProductID),
        BatchNumber    VARCHAR(50),
        ExpiryDate     DATE,
        Quantity       INT NOT NULL,
        BonusQuantity  INT NOT NULL DEFAULT 0,
        PackageType    NVARCHAR(30) NOT NULL DEFAULT 'Box',
        PackageQuantity INT NOT NULL DEFAULT 0,
        UnitsPerPackage INT NOT NULL DEFAULT 1,
        PurchasePrice  DECIMAL(10,2),
        PackMRP        DECIMAL(18,2) NOT NULL DEFAULT 0,
        Discount       DECIMAL(10,2) DEFAULT 0,
        ExtraDiscount  DECIMAL(5,2) NOT NULL DEFAULT 0,
        Tax            DECIMAL(5,2) DEFAULT 0,
        ATax           DECIMAL(5,2) NOT NULL DEFAULT 0,
        CompanySalesTax DECIMAL(5,2) NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH('PurchaseItems', 'BonusQuantity') IS NULL ALTER TABLE PurchaseItems ADD BonusQuantity INT NOT NULL DEFAULT 0;
    IF COL_LENGTH('PurchaseItems', 'PackageType') IS NULL ALTER TABLE PurchaseItems ADD PackageType NVARCHAR(30) NOT NULL DEFAULT 'Box';
    IF COL_LENGTH('PurchaseItems', 'PackageQuantity') IS NULL ALTER TABLE PurchaseItems ADD PackageQuantity INT NOT NULL DEFAULT 0;
    IF COL_LENGTH('PurchaseItems', 'UnitsPerPackage') IS NULL ALTER TABLE PurchaseItems ADD UnitsPerPackage INT NOT NULL DEFAULT 1;
    IF COL_LENGTH('PurchaseItems', 'PackMRP') IS NULL ALTER TABLE PurchaseItems ADD PackMRP DECIMAL(18,2) NOT NULL DEFAULT 0;
    IF COL_LENGTH('PurchaseItems', 'ExtraDiscount') IS NULL ALTER TABLE PurchaseItems ADD ExtraDiscount DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseItems_ExtraDiscount DEFAULT 0;
    IF COL_LENGTH('PurchaseItems', 'ATax') IS NULL ALTER TABLE PurchaseItems ADD ATax DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseItems_ATax DEFAULT 0;
    IF COL_LENGTH('PurchaseItems', 'CompanySalesTax') IS NULL ALTER TABLE PurchaseItems ADD CompanySalesTax DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseItems_CompanySalesTax DEFAULT 0;
END
GO

IF OBJECT_ID('Sales', 'U') IS NULL
BEGIN
    CREATE TABLE Sales (
        SaleID          INT IDENTITY(1,1) PRIMARY KEY,
        InvoiceNumber   VARCHAR(50) NOT NULL,
        SaleDate        DATETIME DEFAULT GETDATE(),
        PatientID       INT FOREIGN KEY REFERENCES Patients(PatientID),
        PatientName     VARCHAR(150),
        ConsultationFee DECIMAL(10,2) DEFAULT 0,
        GrandTotal      DECIMAL(12,2) DEFAULT 0,
        PaymentMethod   VARCHAR(20) CHECK (PaymentMethod IN ('Cash', 'Card', 'Online')),
        IsPosted        BIT DEFAULT 0,
        ReceptionistId  INT NULL FOREIGN KEY REFERENCES Users(UserID),
        ReceptionistName NVARCHAR(150) NULL,
        IsActive        BIT NOT NULL DEFAULT 1,
        SalesTax        DECIMAL(5,2) NOT NULL DEFAULT 0,
        PostedAt        DATETIME2 NULL
    );
END
ELSE
BEGIN
    IF COL_LENGTH('Sales', 'PatientName') IS NULL ALTER TABLE Sales ADD PatientName VARCHAR(150) NULL;
    IF COL_LENGTH('Sales', 'ReceptionistId') IS NULL ALTER TABLE Sales ADD ReceptionistId INT NULL FOREIGN KEY REFERENCES Users(UserID);
    IF COL_LENGTH('Sales', 'ReceptionistName') IS NULL ALTER TABLE Sales ADD ReceptionistName NVARCHAR(150) NULL;
    IF COL_LENGTH('Sales', 'IsActive') IS NULL ALTER TABLE Sales ADD IsActive BIT NOT NULL DEFAULT 1;
    IF COL_LENGTH('Sales', 'SalesTax') IS NULL ALTER TABLE Sales ADD SalesTax DECIMAL(5,2) NOT NULL CONSTRAINT DF_Sales_SalesTax DEFAULT 0;
    IF COL_LENGTH('Sales', 'PostedAt') IS NULL ALTER TABLE Sales ADD PostedAt DATETIME2 NULL;
END
GO

IF OBJECT_ID('SaleItems', 'U') IS NULL
BEGIN
    CREATE TABLE SaleItems (
        SaleItemID INT IDENTITY(1,1) PRIMARY KEY,
        SaleID     INT FOREIGN KEY REFERENCES Sales(SaleID) ON DELETE CASCADE,
        ProductID INT FOREIGN KEY REFERENCES Products(ProductID),
        StockID INT NULL,
        Quantity   INT NOT NULL,
        UnitTypeSold NVARCHAR(20) NOT NULL DEFAULT 'Tablet',
        StockQuantity INT NOT NULL DEFAULT 0,
        UnitPrice DECIMAL(10,2) NOT NULL DEFAULT 0,
        Discount   DECIMAL(10,2) DEFAULT 0,
        Tax        DECIMAL(5,2) DEFAULT 0,
        LineTotal  DECIMAL(10,2) DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH('SaleItems', 'UnitTypeSold') IS NULL ALTER TABLE SaleItems ADD UnitTypeSold NVARCHAR(20) NOT NULL DEFAULT 'Tablet';
    IF COL_LENGTH('SaleItems', 'StockQuantity') IS NULL ALTER TABLE SaleItems ADD StockQuantity INT NOT NULL DEFAULT 0;
    IF COL_LENGTH('SaleItems', 'StockID') IS NULL ALTER TABLE SaleItems ADD StockID INT NULL;
    IF COL_LENGTH('SaleItems', 'UnitPrice') IS NULL ALTER TABLE SaleItems ADD UnitPrice DECIMAL(10,2) NOT NULL DEFAULT 0;
END
GO

IF OBJECT_ID('Settings', 'U') IS NULL
BEGIN
    CREATE TABLE Settings (
        SettingKey NVARCHAR(100) PRIMARY KEY,
        SettingValue NVARCHAR(MAX) NULL
    );
END
GO

IF OBJECT_ID('Prescriptions', 'U') IS NULL
BEGIN
    CREATE TABLE Prescriptions (
        PrescriptionID INT IDENTITY(1,1) PRIMARY KEY,
        PatientID      INT NOT NULL FOREIGN KEY REFERENCES Patients(PatientID),
        DoctorID       INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        AppointmentID  INT NULL FOREIGN KEY REFERENCES Appointments(AppointmentID),
        PharmacistID   INT NULL FOREIGN KEY REFERENCES Users(UserID),
        VisitDate      DATETIME NOT NULL,
        Diagnosis      NVARCHAR(MAX) NULL,
        Notes          NVARCHAR(MAX) NULL,
        LabTests       NVARCHAR(MAX) NULL,
        WorkflowStatus VARCHAR(30) NOT NULL DEFAULT 'Draft',
        SentToPharmacyAt DATETIME NULL,
        PrintedAt      DATETIME NULL,
        DispensedAt    DATETIME NULL,
        CreatedAt      DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

IF OBJECT_ID('PrescriptionItems', 'U') IS NULL
BEGIN
    CREATE TABLE PrescriptionItems (
        PrescriptionItemID INT IDENTITY(1,1) PRIMARY KEY,
        PrescriptionID     INT NOT NULL FOREIGN KEY REFERENCES Prescriptions(PrescriptionID) ON DELETE CASCADE,
        ProductID          INT NOT NULL FOREIGN KEY REFERENCES Products(ProductID),
        Quantity           INT NOT NULL,
        Dosage             NVARCHAR(255) NULL
    );
END
GO

IF OBJECT_ID('Returns', 'U') IS NULL
BEGIN
    CREATE TABLE Returns (
        ReturnId INT IDENTITY(1,1) PRIMARY KEY,
        ReturnNo NVARCHAR(50) NOT NULL,
        ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(ProductID),
        BatchNo NVARCHAR(50) NULL,
        Quantity INT NOT NULL,
        UnitType NVARCHAR(20) NOT NULL DEFAULT 'Tablet',
        StockQuantity INT NOT NULL DEFAULT 0,
        ReturnType NVARCHAR(50) NOT NULL,
        Reason NVARCHAR(200) NULL,
        Notes NVARCHAR(500) NULL,
        PatientId INT NULL FOREIGN KEY REFERENCES Patients(PatientID),
        SupplierId INT NULL FOREIGN KEY REFERENCES Suppliers(SupplierID),
        SaleId INT NULL FOREIGN KEY REFERENCES Sales(SaleID),
        RefundAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
        CreatedBy INT NULL FOREIGN KEY REFERENCES Users(UserID),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        IsPosted BIT NOT NULL DEFAULT 0,
        PostedAt DATETIME2 NULL
    );
END
ELSE
BEGIN
    IF COL_LENGTH('Returns', 'PatientId') IS NULL ALTER TABLE Returns ADD PatientId INT NULL;
    IF COL_LENGTH('Returns', 'SupplierId') IS NULL ALTER TABLE Returns ADD SupplierId INT NULL;
    IF COL_LENGTH('Returns', 'SaleId') IS NULL ALTER TABLE Returns ADD SaleId INT NULL;
    IF COL_LENGTH('Returns', 'RefundAmount') IS NULL ALTER TABLE Returns ADD RefundAmount DECIMAL(12,2) NOT NULL DEFAULT 0;
    IF COL_LENGTH('Returns', 'UnitType') IS NULL ALTER TABLE Returns ADD UnitType NVARCHAR(20) NOT NULL DEFAULT 'Tablet';
    IF COL_LENGTH('Returns', 'StockQuantity') IS NULL ALTER TABLE Returns ADD StockQuantity INT NOT NULL DEFAULT 0;
    IF COL_LENGTH('Returns', 'IsPosted') IS NULL
    BEGIN
        ALTER TABLE Returns ADD IsPosted BIT NOT NULL CONSTRAINT DF_Returns_IsPosted DEFAULT 0;
        -- Mark existing returns as posted so they don't double-count stock
        EXEC('UPDATE Returns SET IsPosted = 1');
    END
    IF COL_LENGTH('Returns', 'PostedAt') IS NULL ALTER TABLE Returns ADD PostedAt DATETIME2 NULL;
END
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('DiscountRefunds', 'U') IS NULL
BEGIN
    CREATE TABLE DiscountRefunds (
        RefundID          INT IDENTITY(1,1) PRIMARY KEY,
        PatientName       NVARCHAR(150) NOT NULL,
        TokenNumber       VARCHAR(50),
        OriginalFee       DECIMAL(10,2) NOT NULL,
        DiscountedFee     DECIMAL(10,2) NOT NULL,
        RefundAmount      AS (OriginalFee - DiscountedFee) PERSISTED,
        Notes             NVARCHAR(500),
        ApprovedByUserID  INT REFERENCES Users(UserID),
        ApprovedByName    NVARCHAR(150),
        ApprovedAt        DATETIME DEFAULT GETDATE(),
        CompletedByUserID INT REFERENCES Users(UserID),
        CompletedByName   NVARCHAR(150),
        CompletedAt       DATETIME,
        IsCompleted       BIT DEFAULT 0
    );
END
GO

IF OBJECT_ID('ActivityLogs', 'U') IS NULL
BEGIN
    CREATE TABLE ActivityLogs (
        ActivityID  INT IDENTITY(1,1) PRIMARY KEY,
        Title       VARCHAR(255) NOT NULL,
        Description VARCHAR(1000) NULL,
        Module      VARCHAR(100) NOT NULL,
        UserID      INT,
        UserName    VARCHAR(150),
        CreatedAt   DATETIME DEFAULT GETDATE()
    );
END
GO

-- ============================================================
--  INDEXES
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Patients_Name')
    CREATE INDEX IX_Patients_Name ON Patients(Name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Products_Name')
    CREATE INDEX IX_Products_Name ON Products(Name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Appointments_Date')
    CREATE INDEX IX_Appointments_Date ON Appointments(AppointmentDate);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_DiscountRefunds_IsCompleted')
    CREATE INDEX IX_DiscountRefunds_IsCompleted ON DiscountRefunds(IsCompleted);
GO

-- ============================================================
--  SEED DATA
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (Username, PasswordHash, Role, FullName, IsActive)
    VALUES (
        'admin',
        '$2a$11$u0LyGgHmhN2kTeoBK.a5m.FVHXHSUA/xHZFJ9tE1O4Oj4QvICWT.O',  -- Admin@123
        'Admin',
        'System Admin',
        1
    );
END
GO

-- ============================================================
--  MIGRATIONS: ADD NEW COLUMNS IDEMPOTENTLY
-- ============================================================
-- Add LabTests column to Prescriptions (new workflow: replaces free-text Diagnosis/Notes)
IF COL_LENGTH('Prescriptions', 'LabTests') IS NULL
    ALTER TABLE Prescriptions ADD LabTests NVARCHAR(MAX) NULL;
GO

IF COL_LENGTH('Prescriptions', 'AppointmentID') IS NULL
BEGIN
    ALTER TABLE Prescriptions ADD AppointmentID INT NULL;
    ALTER TABLE Prescriptions ADD CONSTRAINT FK_Prescriptions_Appointments FOREIGN KEY (AppointmentID) REFERENCES Appointments(AppointmentID);
END
GO

IF COL_LENGTH('Prescriptions', 'PharmacistID') IS NULL
BEGIN
    ALTER TABLE Prescriptions ADD PharmacistID INT NULL;
    ALTER TABLE Prescriptions ADD CONSTRAINT FK_Prescriptions_Pharmacist FOREIGN KEY (PharmacistID) REFERENCES Users(UserID);
END
GO

PRINT 'ClinicDB expanded schema created/updated successfully.';
GO

GO
-- ============================================================
--  MIGRATION: Add DiscountRefunds table
--  Run this ONCE on existing ClinicDB databases.
--  Safe to run multiple times (IF NOT EXISTS guard).
-- ============================================================
USE ClinicDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DiscountRefunds')
BEGIN
    CREATE TABLE DiscountRefunds (
        RefundID          INT IDENTITY(1,1) PRIMARY KEY,
        PatientName       NVARCHAR(150) NOT NULL,
        TokenNumber       VARCHAR(50),
        OriginalFee       DECIMAL(10,2) NOT NULL,
        DiscountedFee     DECIMAL(10,2) NOT NULL,
        RefundAmount      AS (OriginalFee - DiscountedFee) PERSISTED,
        Notes             NVARCHAR(500),
        ApprovedByUserID  INT REFERENCES Users(UserID),
        ApprovedByName    NVARCHAR(150),
        ApprovedAt        DATETIME DEFAULT GETDATE(),
        CompletedByUserID INT REFERENCES Users(UserID),
        CompletedByName   NVARCHAR(150),
        CompletedAt       DATETIME,
        IsCompleted       BIT DEFAULT 0
    );

    -- Also add PatientName column to Sales if not already present
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'PatientName')
        ALTER TABLE Sales ADD PatientName NVARCHAR(150);

    PRINT 'DiscountRefunds table created successfully.';
END
ELSE
BEGIN
    PRINT 'DiscountRefunds table already exists — no changes made.';
END
GO

GO

/* Migration: 002_AddPharmacistRole.sql */

GO
-- ============================================================
--  Migration 002: Keep the Users.Role CHECK constraint aligned with every
--  role supported by the application.
--  Run this once on any live ClinicDB instance.
-- ============================================================

USE ClinicDB;
GO

-- Drop the old CHECK constraint dynamically
DECLARE @DropRoleConstraintsSql NVARCHAR(MAX) = N'';
SELECT @DropRoleConstraintsSql = @DropRoleConstraintsSql
    + N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(name) + N';'
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
  AND CHARINDEX(N'Role', definition) > 0;

IF LEN(@DropRoleConstraintsSql) > 0
BEGIN
    EXEC sys.sp_executesql @DropRoleConstraintsSql;
END

-- Recreate with every role exposed by UserRegistryViewModel.RoleOptions.
ALTER TABLE dbo.Users WITH CHECK
    ADD CONSTRAINT CK_Users_Role
    CHECK (Role IN ('Doctor', 'Receptionist', 'Admin', 'Pharmacist', 'Assistant'));

PRINT 'CK_Users_Role updated — all supported roles are now allowed.';
GO

GO

/* Migration: 003_WorkflowUpdates.sql */

GO
-- Migration 003: Workflow & architecture updates
-- Run against ClinicDB (safe to re-run)

USE ClinicDB;
GO

-- Appointments
IF COL_LENGTH('Appointments', 'AppointmentNo') IS NULL
    ALTER TABLE Appointments ADD AppointmentNo VARCHAR(20) NULL;

IF COL_LENGTH('Appointments', 'PatientName') IS NULL
    ALTER TABLE Appointments ADD PatientName VARCHAR(150) NULL;

IF COL_LENGTH('Appointments', 'Phone') IS NULL
    ALTER TABLE Appointments ADD Phone VARCHAR(50) NULL;

IF COL_LENGTH('Appointments', 'Remarks') IS NULL
    ALTER TABLE Appointments ADD Remarks VARCHAR(255) NULL;

-- Make PatientID nullable
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Appointme__Patie__' + CAST(OBJECT_ID('Appointments') AS VARCHAR))
    PRINT 'Check FK manually if PatientID alter fails';

BEGIN TRY
    ALTER TABLE Appointments ALTER COLUMN PatientID INT NULL;
END TRY
BEGIN CATCH
    PRINT 'PatientID may already be nullable';
END CATCH

-- Drop old status constraint and apply new values
DECLARE @statusConstraint NVARCHAR(200);
SELECT @statusConstraint = dc.name
FROM sys.check_constraints dc
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID('Appointments') AND c.name = 'Status';

IF @statusConstraint IS NOT NULL
    EXEC('ALTER TABLE Appointments DROP CONSTRAINT ' + @statusConstraint);

UPDATE Appointments SET Status = 'Missed' WHERE Status IN ('No-Show', 'No Show');
-- Note: 'Checked-In' is a valid status used by the application; do NOT migrate it away.

ALTER TABLE Appointments ADD CONSTRAINT CK_Appointments_Status
    CHECK (Status IN ('Scheduled', 'Checked-In', 'Completed', 'Cancelled', 'Missed'));

-- Purchases: optional supplier + manual name
BEGIN TRY
    ALTER TABLE Purchases ALTER COLUMN SupplierID INT NULL;
END TRY
BEGIN CATCH
    PRINT 'SupplierID may already be nullable';
END CATCH

IF COL_LENGTH('Purchases', 'SupplierName') IS NULL
    ALTER TABLE Purchases ADD SupplierName VARCHAR(150) NULL;

UPDATE p SET p.SupplierName = s.Name
FROM Purchases p
JOIN Suppliers s ON p.SupplierID = s.SupplierID
WHERE p.SupplierName IS NULL;

-- Products: company/supplier names + barcode
IF COL_LENGTH('Products', 'CompanyName') IS NULL
    ALTER TABLE Products ADD CompanyName VARCHAR(150) NULL;

IF COL_LENGTH('Products', 'SupplierID') IS NULL
    ALTER TABLE Products ADD SupplierID INT NULL;

IF COL_LENGTH('Products', 'SupplierName') IS NULL
    ALTER TABLE Products ADD SupplierName VARCHAR(150) NULL;

IF COL_LENGTH('Products', 'Barcode') IS NULL
    ALTER TABLE Products ADD Barcode VARCHAR(50) NULL;

UPDATE m SET m.CompanyName = c.Name
FROM Products m
JOIN Companies c ON m.CompanyID = c.CompanyID
WHERE m.CompanyName IS NULL AND m.CompanyID IS NOT NULL;

-- Patients: next appointment fields
IF COL_LENGTH('Patients', 'NextAppointmentDate') IS NULL
    ALTER TABLE Patients ADD NextAppointmentDate DATE NULL;

IF COL_LENGTH('Patients', 'NextAppointmentTime') IS NULL
    ALTER TABLE Patients ADD NextAppointmentTime TIME NULL;

GO

GO

/* Migration: 004_AppointmentPatientSync.sql */

GO
-- Migration 004: Sync Appointments with Patient Waiting List
-- Run against ClinicDB (safe to re-run)

USE ClinicDB;
GO

-- Patients: add VisitStatus and LastVisitDate for waiting list tracking
IF COL_LENGTH('Patients', 'VisitStatus') IS NULL
    ALTER TABLE Patients ADD VisitStatus VARCHAR(20) NULL;
GO

IF COL_LENGTH('Patients', 'LastVisitDate') IS NULL
    ALTER TABLE Patients ADD LastVisitDate DATE NULL;
GO

-- NextAppointmentTime already added in 003, but guard just in case
IF COL_LENGTH('Patients', 'NextAppointmentTime') IS NULL
    ALTER TABLE Patients ADD NextAppointmentTime TIME NULL;
GO

-- Appointments: add Gender and Age columns for walk-in patients
IF COL_LENGTH('Appointments', 'Gender') IS NULL
    ALTER TABLE Appointments ADD Gender VARCHAR(10) NULL;
GO

IF COL_LENGTH('Appointments', 'Age') IS NULL
    ALTER TABLE Appointments ADD Age INT NULL;
GO

GO

/* Migration: 005_ModuleUpgrades.sql */

GO
USE ClinicDB;
GO

-- Granular product stock and safe archival flags.
IF COL_LENGTH('Products', 'TabletsPerBox') IS NULL ALTER TABLE Products ADD TabletsPerBox INT NOT NULL CONSTRAINT DF_Products_TabletsPerBox DEFAULT 1;
IF COL_LENGTH('Products', 'IsReturnable') IS NULL ALTER TABLE Products ADD IsReturnable BIT NOT NULL CONSTRAINT DF_Products_IsReturnable DEFAULT 1;
IF COL_LENGTH('Products', 'IsActive') IS NULL ALTER TABLE Products ADD IsActive BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1;
IF COL_LENGTH('Patients', 'IsActive') IS NULL ALTER TABLE Patients ADD IsActive BIT NOT NULL CONSTRAINT DF_Patients_IsActive DEFAULT 1;
GO

-- Transaction ownership and safe archival.
IF COL_LENGTH('Sales', 'ReceptionistId') IS NULL ALTER TABLE Sales ADD ReceptionistId INT NULL;
IF COL_LENGTH('Sales', 'ReceptionistName') IS NULL ALTER TABLE Sales ADD ReceptionistName NVARCHAR(150) NULL;
IF COL_LENGTH('Sales', 'IsActive') IS NULL ALTER TABLE Sales ADD IsActive BIT NOT NULL CONSTRAINT DF_Sales_IsActive DEFAULT 1;
IF COL_LENGTH('Purchases', 'CreatedBy') IS NULL ALTER TABLE Purchases ADD CreatedBy INT NULL;
IF COL_LENGTH('Purchases', 'CreatedByName') IS NULL ALTER TABLE Purchases ADD CreatedByName NVARCHAR(150) NULL;
GO

-- Package-aware purchases. Quantity remains populated for compatibility and stores total units.
IF COL_LENGTH('PurchaseItems', 'BonusQuantity') IS NULL ALTER TABLE PurchaseItems ADD BonusQuantity INT NOT NULL CONSTRAINT DF_PurchaseItems_BonusQuantity DEFAULT 0;
IF COL_LENGTH('PurchaseItems', 'PackageType') IS NULL ALTER TABLE PurchaseItems ADD PackageType NVARCHAR(30) NOT NULL CONSTRAINT DF_PurchaseItems_PackageType DEFAULT 'Box';
IF COL_LENGTH('PurchaseItems', 'PackageQuantity') IS NULL ALTER TABLE PurchaseItems ADD PackageQuantity INT NOT NULL CONSTRAINT DF_PurchaseItems_PackageQuantity DEFAULT 0;
IF COL_LENGTH('PurchaseItems', 'UnitsPerPackage') IS NULL ALTER TABLE PurchaseItems ADD UnitsPerPackage INT NOT NULL CONSTRAINT DF_PurchaseItems_UnitsPerPackage DEFAULT 1;
GO
UPDATE PurchaseItems SET PackageQuantity = Quantity WHERE PackageQuantity = 0;

-- Sale quantities are customer-facing quantities; StockQuantity is always individual stock units.
IF COL_LENGTH('SaleItems', 'UnitTypeSold') IS NULL ALTER TABLE SaleItems ADD UnitTypeSold NVARCHAR(20) NOT NULL CONSTRAINT DF_SaleItems_UnitTypeSold DEFAULT 'Tablet';
IF COL_LENGTH('SaleItems', 'StockQuantity') IS NULL ALTER TABLE SaleItems ADD StockQuantity INT NOT NULL CONSTRAINT DF_SaleItems_StockQuantity DEFAULT 0;
IF COL_LENGTH('SaleItems', 'UnitPrice') IS NULL ALTER TABLE SaleItems ADD UnitPrice DECIMAL(10,2) NOT NULL CONSTRAINT DF_SaleItems_UnitPrice DEFAULT 0;
GO
UPDATE SaleItems SET StockQuantity = Quantity WHERE StockQuantity = 0;
UPDATE si SET UnitPrice = CASE WHEN si.Quantity > 0 THEN si.LineTotal / si.Quantity ELSE 0 END FROM SaleItems si WHERE UnitPrice = 0;

-- One auditable table handles both patient and supplier returns.
IF COL_LENGTH('Returns', 'UnitType') IS NULL ALTER TABLE Returns ADD UnitType NVARCHAR(20) NOT NULL CONSTRAINT DF_Returns_UnitType DEFAULT 'Tablet';
IF COL_LENGTH('Returns', 'StockQuantity') IS NULL ALTER TABLE Returns ADD StockQuantity INT NOT NULL CONSTRAINT DF_Returns_StockQuantity DEFAULT 0;
GO
UPDATE Returns SET StockQuantity = Quantity WHERE StockQuantity = 0;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'ProductId', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'CreatedBy', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'PatientId', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_Patient FOREIGN KEY (PatientId) REFERENCES Patients(PatientID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'SupplierId', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_Supplier FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'SaleId', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_Sale FOREIGN KEY (SaleId) REFERENCES Sales(SaleID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sales_Date_Receptionist')
    CREATE INDEX IX_Sales_Date_Receptionist ON Sales(SaleDate, ReceptionistId) INCLUDE (GrandTotal, IsPosted, IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Returns_Date_Type')
    CREATE INDEX IX_Returns_Date_Type ON Returns(CreatedAt, ReturnType) INCLUDE (RefundAmount, StockQuantity);
GO

GO

/* Migration: 006_MediCompareOverhaul.sql */

GO
-- Migration 006: MediCompare clinical/pharma split and posting workflows
-- Non-destructive and safe to run repeatedly against ClinicDB.

USE ClinicDB;
GO

IF COL_LENGTH('Companies', 'CCode') IS NULL
BEGIN
    ALTER TABLE Companies ADD CCode INT NOT NULL CONSTRAINT DF_Companies_CCode DEFAULT 0;
END
GO

EXEC('UPDATE Companies SET CCode = CompanyID WHERE CCode = 0');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Companies_CCode')
    EXEC('CREATE UNIQUE INDEX UX_Companies_CCode ON Companies(CCode)');
GO

IF COL_LENGTH('Suppliers', 'SCode') IS NULL
BEGIN
    ALTER TABLE Suppliers ADD SCode INT NOT NULL CONSTRAINT DF_Suppliers_SCode DEFAULT 0;
END
GO

EXEC('UPDATE Suppliers SET SCode = SupplierID WHERE SCode = 0');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Suppliers_SCode')
    EXEC('CREATE UNIQUE INDEX UX_Suppliers_SCode ON Suppliers(SCode)');
GO

IF COL_LENGTH('Products', 'PCode') IS NULL
    ALTER TABLE Products ADD PCode INT NOT NULL CONSTRAINT DF_Products_PCode DEFAULT 0;
IF COL_LENGTH('Products', 'Packing') IS NULL
    ALTER TABLE Products ADD Packing NVARCHAR(100) NULL;
IF COL_LENGTH('Products', 'PiecesPerUnit') IS NULL
    ALTER TABLE Products ADD PiecesPerUnit INT NOT NULL CONSTRAINT DF_Products_PiecesPerUnit DEFAULT 1;
IF COL_LENGTH('Products', 'LastStockUpdateDate') IS NULL
    ALTER TABLE Products ADD LastStockUpdateDate DATE NULL;
GO
UPDATE Products SET Packing = Category WHERE Packing IS NULL AND Category IS NOT NULL;
UPDATE Products SET PiecesPerUnit = TabletsPerBox WHERE PiecesPerUnit = 1 AND TabletsPerBox > 1;
;WITH Codes AS (
    SELECT ProductID, ROW_NUMBER() OVER (PARTITION BY CompanyID ORDER BY ProductID) AS NewCode
    FROM Products
)
UPDATE p SET PCode = c.NewCode FROM Products p JOIN Codes c ON p.ProductID = c.ProductID WHERE p.PCode = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Products_Company_PCode')
    CREATE UNIQUE INDEX UX_Products_Company_PCode ON Products(CompanyID, PCode) WHERE CompanyID IS NOT NULL;
GO

IF COL_LENGTH('Appointments', 'CNIC') IS NULL
    ALTER TABLE Appointments ADD CNIC NVARCHAR(50) NULL;
GO

IF COL_LENGTH('Patients', 'PatientContext') IS NULL
    ALTER TABLE Patients ADD PatientContext NVARCHAR(20) NOT NULL CONSTRAINT DF_Patients_Context DEFAULT 'Clinical';
IF COL_LENGTH('Patients', 'ReasonOfVisit') IS NULL
    ALTER TABLE Patients ADD ReasonOfVisit NVARCHAR(500) NULL;
GO

IF COL_LENGTH('Purchases', 'IsPosted') IS NULL
BEGIN
    ALTER TABLE Purchases ADD IsPosted BIT NOT NULL CONSTRAINT DF_Purchases_IsPosted DEFAULT 0;
END
GO

-- Legacy purchases already changed stock, so migrate them as posted to prevent double-counting.
EXEC('UPDATE Purchases SET IsPosted = 1 WHERE IsPosted = 0');
GO

IF COL_LENGTH('Purchases', 'PostedAt') IS NULL
BEGIN
    ALTER TABLE Purchases ADD PostedAt DATETIME2 NULL;
END
GO

EXEC('UPDATE Purchases SET PostedAt = PurchaseDate WHERE IsPosted = 1 AND PostedAt IS NULL');
GO
IF COL_LENGTH('Purchases', 'ATax') IS NULL
    ALTER TABLE Purchases ADD ATax DECIMAL(5,2) NOT NULL CONSTRAINT DF_Purchases_ATax DEFAULT 0;
IF COL_LENGTH('PurchaseItems', 'ExtraDiscount') IS NULL
    ALTER TABLE PurchaseItems ADD ExtraDiscount DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseItems_ExtraDiscount DEFAULT 0;
IF COL_LENGTH('PurchaseItems', 'ATax') IS NULL
    ALTER TABLE PurchaseItems ADD ATax DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseItems_ATax DEFAULT 0;
GO

IF COL_LENGTH('Sales', 'SalesTax') IS NULL
    ALTER TABLE Sales ADD SalesTax DECIMAL(5,2) NOT NULL CONSTRAINT DF_Sales_SalesTax DEFAULT 0;
IF COL_LENGTH('Sales', 'PostedAt') IS NULL
    ALTER TABLE Sales ADD PostedAt DATETIME2 NULL;
GO

-- Preserve the existing clinical visit data model even when upgrading from a minimal schema.
IF OBJECT_ID('Prescriptions', 'U') IS NULL
BEGIN
    CREATE TABLE Prescriptions (
        PrescriptionID INT IDENTITY(1,1) PRIMARY KEY,
        PatientID INT NOT NULL FOREIGN KEY REFERENCES Patients(PatientID),
        DoctorID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        VisitDate DATETIME NOT NULL,
        Diagnosis NVARCHAR(MAX) NULL,
        Notes NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
IF OBJECT_ID('PrescriptionItems', 'U') IS NULL
BEGIN
    CREATE TABLE PrescriptionItems (
        PrescriptionItemID INT IDENTITY(1,1) PRIMARY KEY,
        PrescriptionID INT NOT NULL FOREIGN KEY REFERENCES Prescriptions(PrescriptionID) ON DELETE CASCADE,
        ProductID INT NOT NULL FOREIGN KEY REFERENCES Products(ProductID),
        Quantity INT NOT NULL,
        Dosage NVARCHAR(255) NULL
    );
END
GO

IF COL_LENGTH('Returns', 'IsPosted') IS NULL
BEGIN
    ALTER TABLE Returns ADD IsPosted BIT NOT NULL CONSTRAINT DF_Returns_IsPosted DEFAULT 0;
END
GO

-- Legacy returns already changed stock, so migrate them as posted.
EXEC('UPDATE Returns SET IsPosted = 1 WHERE IsPosted = 0');
GO

IF COL_LENGTH('Returns', 'PostedAt') IS NULL
BEGIN
    ALTER TABLE Returns ADD PostedAt DATETIME2 NULL;
END
GO

EXEC('UPDATE Returns SET PostedAt = CreatedAt WHERE IsPosted = 1 AND PostedAt IS NULL');
GO
GO
BEGIN TRY
    ALTER TABLE Returns ALTER COLUMN ProductId INT NULL;
END TRY
BEGIN CATCH
    PRINT 'Returns.ProductId could not be made nullable; existing schema may require manual constraint review.';
END CATCH;
GO

IF OBJECT_ID('ReturnItems', 'U') IS NULL
BEGIN
    CREATE TABLE ReturnItems (
        ReturnItemID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ReturnId INT NOT NULL,
        ProductId INT NOT NULL,
        Quantity INT NOT NULL,
        Reason NVARCHAR(200) NULL,
        RefundAmount DECIMAL(12,2) NOT NULL CONSTRAINT DF_ReturnItems_RefundAmount DEFAULT 0,
        CONSTRAINT CK_ReturnItems_Quantity CHECK (Quantity > 0),
        CONSTRAINT FK_ReturnItems_Return FOREIGN KEY (ReturnId) REFERENCES Returns(ReturnId) ON DELETE CASCADE,
        CONSTRAINT FK_ReturnItems_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductID)
    );
END
GO

IF EXISTS (SELECT 1 FROM Returns WHERE ProductId IS NOT NULL)
BEGIN
    INSERT INTO ReturnItems (ReturnId, ProductId, Quantity, Reason, RefundAmount)
    SELECT r.ReturnId, r.ProductId, CASE WHEN r.StockQuantity > 0 THEN r.StockQuantity ELSE r.Quantity END,
           r.Reason, r.RefundAmount
    FROM Returns r
    WHERE NOT EXISTS (SELECT 1 FROM ReturnItems ri WHERE ri.ReturnId = r.ReturnId);
END
GO

IF NOT EXISTS (SELECT 1 FROM Settings WHERE SettingKey = 'PharmacyName')
    INSERT INTO Settings (SettingKey, SettingValue) VALUES ('PharmacyName', 'DR ASIF PHARMA');
IF NOT EXISTS (SELECT 1 FROM Settings WHERE SettingKey = 'ClinicName')
    INSERT INTO Settings (SettingKey, SettingValue) VALUES ('ClinicName', 'DR ASIF PHARMA');
IF NOT EXISTS (SELECT 1 FROM Settings WHERE SettingKey = 'ClinicAddress')
    INSERT INTO Settings (SettingKey, SettingValue) VALUES ('ClinicAddress', 'Pirmahal, Near Imam Bargah');
IF NOT EXISTS (SELECT 1 FROM Settings WHERE SettingKey = 'Address')
    INSERT INTO Settings (SettingKey, SettingValue) VALUES ('Address', 'Pirmahal, Near Imam Bargah');
IF NOT EXISTS (SELECT 1 FROM Settings WHERE SettingKey = 'ReceptionistTheme')
    INSERT INTO Settings (SettingKey, SettingValue) VALUES ('ReceptionistTheme', 'System Default');
UPDATE Settings SET SettingValue = 'DR ASIF PHARMA'
WHERE SettingKey IN ('PharmacyName', 'ClinicName')
  AND SettingValue IN ('My Pharmacy', 'Care & Cure Clinic', 'MediCompare Pharmacy');
UPDATE Settings SET SettingValue = 'Pirmahal, Near Imam Bargah'
WHERE SettingKey IN ('Address', 'ClinicAddress')
  AND SettingValue IN ('123 Health Ave, Medical District', '');
GO

GO

/* Migration: 007_FixAppointmentStatus.sql */

GO
-- Migration 007: Restore 'Checked-In' as a valid Appointments status
-- The previous migration 003 incorrectly removed 'Checked-In' from the check
-- constraint. This migration corrects that for all existing databases.
-- Safe to run multiple times.

USE ClinicDB;
GO

-- Drop and recreate the status check constraint to include 'Checked-In'
DECLARE @con NVARCHAR(200);
SELECT @con = dc.name
FROM sys.check_constraints dc
JOIN sys.columns c
    ON dc.parent_object_id = c.object_id
    AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID('Appointments')
  AND c.name = 'Status';

IF @con IS NOT NULL
    EXEC('ALTER TABLE Appointments DROP CONSTRAINT [' + @con + ']');

-- The app uses 'Checked-In' in MainWindowViewModel and ClinicalDashboardViewModel.
-- Ensure the constraint allows all five statuses.
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('Appointments')
      AND name = 'CK_Appointments_Status'
)
BEGIN
    ALTER TABLE Appointments ADD CONSTRAINT CK_Appointments_Status
        CHECK (Status IN ('Scheduled', 'Checked-In', 'Completed', 'Cancelled', 'Missed'));
END
GO

-- Ensure the CNIC column is present (added in 006 but guarded here for safety)
IF COL_LENGTH('Appointments', 'CNIC') IS NULL
    ALTER TABLE Appointments ADD CNIC NVARCHAR(50) NULL;
GO

PRINT 'Migration 007 complete: Appointments.Status constraint updated to include Checked-In.';
GO

GO

/* Migration: 007_PrescriptionHandoff.sql */

GO
-- Doctor -> Pharmacist -> Receptionist prescription handoff
-- Safe to run repeatedly.

IF COL_LENGTH('Prescriptions', 'WorkflowStatus') IS NULL
    ALTER TABLE Prescriptions ADD WorkflowStatus VARCHAR(30) NOT NULL CONSTRAINT DF_Prescriptions_WorkflowStatus DEFAULT 'Draft';

IF COL_LENGTH('Prescriptions', 'SentToPharmacyAt') IS NULL
    ALTER TABLE Prescriptions ADD SentToPharmacyAt DATETIME NULL;

IF COL_LENGTH('Prescriptions', 'PrintedAt') IS NULL
    ALTER TABLE Prescriptions ADD PrintedAt DATETIME NULL;

IF COL_LENGTH('Prescriptions', 'DispensedAt') IS NULL
    ALTER TABLE Prescriptions ADD DispensedAt DATETIME NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Prescriptions_WorkflowStatus')
    CREATE INDEX IX_Prescriptions_WorkflowStatus ON Prescriptions(WorkflowStatus, SentToPharmacyAt DESC);
GO

GO

/* Migration: 008_PrescriptionWorkflowDetails.sql */

GO
-- Migration: Add AppointmentID and PharmacistID to Prescriptions for full-stack workflow tracking
-- Safe to run repeatedly.

IF COL_LENGTH('Prescriptions', 'AppointmentID') IS NULL
BEGIN
    ALTER TABLE Prescriptions ADD AppointmentID INT NULL;
    ALTER TABLE Prescriptions ADD CONSTRAINT FK_Prescriptions_Appointments FOREIGN KEY (AppointmentID) REFERENCES Appointments(AppointmentID);
END

IF COL_LENGTH('Prescriptions', 'PharmacistID') IS NULL
BEGIN
    ALTER TABLE Prescriptions ADD PharmacistID INT NULL;
    ALTER TABLE Prescriptions ADD CONSTRAINT FK_Prescriptions_Pharmacist FOREIGN KEY (PharmacistID) REFERENCES Users(UserID);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Prescriptions_AppointmentID')
BEGIN
    CREATE INDEX IX_Prescriptions_AppointmentID ON Prescriptions(AppointmentID);
END
GO

GO

/* Migration: 009_AddAssistantRole.sql */

GO
-- Migration: allow Assistant users in the Users.Role check constraint.
-- Safe to run against existing ClinicDB databases.

DECLARE @constraintName sysname;

SELECT TOP (1) @constraintName = cc.name
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Users')
  AND CHARINDEX(N'Role', cc.definition) > 0;

IF @constraintName IS NOT NULL
BEGIN
    DECLARE @dropRoleConstraintSql nvarchar(500) =
        N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(@constraintName) + N';';
    EXEC sys.sp_executesql @dropRoleConstraintSql;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
      AND name = N'CK_Users_Role')
BEGIN
    ALTER TABLE dbo.Users ADD CONSTRAINT CK_Users_Role
        CHECK (Role IN ('Doctor', 'Receptionist', 'Admin', 'Pharmacist', 'Assistant'));
END

PRINT 'CK_Users_Role updated - Assistant role now allowed.';

GO

/* Migration: 01_MedicineReturns.sql */

GO
USE ClinicDB;
GO

-- Add IsReturnable column to Products if it doesn't exist
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE Name = N'IsReturnable' AND Object_ID = Object_ID(N'Products')
)
BEGIN
    ALTER TABLE Products
    ADD IsReturnable BIT NOT NULL DEFAULT 1;
END
GO

-- Create ProductReturns table
IF OBJECT_ID('ProductReturns', 'U') IS NULL
BEGIN
    CREATE TABLE ProductReturns (
        ReturnId INT IDENTITY(1,1) PRIMARY KEY,
        SaleId INT NOT NULL FOREIGN KEY REFERENCES Sales(SaleId),
        ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(ProductId),
        PatientId INT NOT NULL FOREIGN KEY REFERENCES Patients(PatientId),
        QuantityReturned INT NOT NULL CHECK (QuantityReturned > 0),
        UnitPriceAtSale DECIMAL(10,2) NOT NULL,
        RefundAmount DECIMAL(12,2) NOT NULL,
        Reason VARCHAR(255) NOT NULL,
        ReturnDate DATETIME NOT NULL DEFAULT GETDATE(),
        ProcessedBy INT NULL FOREIGN KEY REFERENCES Users(UserID),
        Status VARCHAR(50) NOT NULL DEFAULT 'Completed'
    );
END
GO

GO

/* Migration: 010_PurchaseCompanySalesTax.sql */

GO
-- Migration: add company sales tax percentage to purchase invoice lines.
-- Safe to run against existing ClinicDB databases.

IF COL_LENGTH('PurchaseItems', 'CompanySalesTax') IS NULL
BEGIN
    ALTER TABLE PurchaseItems
        ADD CompanySalesTax DECIMAL(5,2) NOT NULL
            CONSTRAINT DF_PurchaseItems_CompanySalesTax DEFAULT 0;
END

GO

/* Migration: 011_AddProductRate.sql */

GO
-- ============================================================
--  Migration 011: Add Rate (Gross Trade Price) column to Products
--  Run against existing ClinicDB instances.
--  Idempotent — safe to execute multiple times.
-- ============================================================

USE ClinicDB;
GO

-- 1. Add the Rate column if it does not already exist
IF COL_LENGTH('Products', 'Rate') IS NULL
BEGIN
    ALTER TABLE Products
        ADD Rate DECIMAL(10,2) NULL CONSTRAINT DF_Products_Rate DEFAULT 0.00;

    -- Back-fill from SellingPrice as a safe starting point
    UPDATE Products SET Rate = SellingPrice WHERE Rate IS NULL;

    -- Tighten to NOT NULL now that all rows have a value
    ALTER TABLE Products
        ALTER COLUMN Rate DECIMAL(10,2) NOT NULL;

    PRINT 'Products.Rate column added and back-filled.';
END
ELSE
BEGIN
    PRINT 'Products.Rate already exists — no action taken.';
END
GO

GO

/* Migration: 012_ProductStockExpiryTracking.sql */

GO
-- Migration 012: Product Stock Expiry Tracking
-- Run against ClinicDB (safe to re-run)

USE ClinicDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductStock')
BEGIN
    CREATE TABLE ProductStock (
        StockID INT IDENTITY(1,1) PRIMARY KEY,
        ProductID INT NOT NULL,
        ExpiryDate DATE NOT NULL,
        QuantityAvailable INT NOT NULL DEFAULT 0,
        PurchasePrice DECIMAL(18,6) NOT NULL DEFAULT 0,
        MRP DECIMAL(18,2) NOT NULL DEFAULT 0,
        CONSTRAINT FK_ProductStock_Products FOREIGN KEY (ProductID) REFERENCES Products(ProductID) ON DELETE CASCADE
    );

    -- Create index for faster FEFO queries
    CREATE INDEX IX_ProductStock_ProductID_ExpiryDate ON ProductStock (ProductID, ExpiryDate, QuantityAvailable);
END
GO

-- Migrate existing stock from Products to ProductStock
IF COL_LENGTH('Products', 'Stock') IS NOT NULL
BEGIN
    -- Only migrate if ProductStock is empty to avoid duplicating stock on re-run
    IF NOT EXISTS (SELECT 1 FROM ProductStock)
    BEGIN
        -- Preserve legacy master-row stock using a far-future placeholder expiry date.
        -- Note: We could try to infer from PurchaseItems, but since old data didn't strictly link stock to expiry,
        -- setting a safe date ensures the existing stock remains sellable under the new FEFO logic.
        -- Dynamic SQL is required because SQL Server resolves column references for
        -- the whole batch even when the COL_LENGTH guard evaluates to false.
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.ProductStock
                (ProductID, ExpiryDate, QuantityAvailable, PurchasePrice, MRP)
            SELECT
                ProductID,
                COALESCE(ExpiryDate, CONVERT(date, ''2099-12-31'')),
                Stock,
                COALESCE(PurchasePrice, 0),
                COALESCE(SellingPrice, 0)
            FROM dbo.Products
            WHERE Stock > 0;';
    END

    -- Drop the old Stock column from Products (and its constraints if any)
    DECLARE @DefaultConstraintName NVARCHAR(200);
    SELECT @DefaultConstraintName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID('Products') AND c.name = 'Stock';

    IF @DefaultConstraintName IS NOT NULL
    BEGIN
        DECLARE @dropStockConstraintSql nvarchar(500) =
            N'ALTER TABLE dbo.Products DROP CONSTRAINT ' + QUOTENAME(@DefaultConstraintName) + N';';
        EXEC sys.sp_executesql @dropStockConstraintSql;
    END

    EXEC sys.sp_executesql N'ALTER TABLE dbo.Products DROP COLUMN Stock;';
END
GO

GO

/* Migration: 013_ProductStockUniqueConstraint.sql */

GO
-- Migration 013: Add UNIQUE constraint to ProductStock (ProductID, ExpiryDate)
USE ClinicDB;
GO

-- Step 1: Merge quantities for duplicate (ProductID, ExpiryDate) pairs
;WITH Dupes AS (
    SELECT ProductID, ExpiryDate,
           MIN(StockID) AS KeepStockID,
           SUM(QuantityAvailable) AS TotalQty,
           MAX(PurchasePrice) AS LatestPurchasePrice,
           MAX(MRP) AS LatestMRP
    FROM ProductStock
    GROUP BY ProductID, ExpiryDate
    HAVING COUNT(*) > 1
)
UPDATE ps
SET ps.QuantityAvailable = d.TotalQty,
    ps.PurchasePrice     = d.LatestPurchasePrice,
    ps.MRP               = d.LatestMRP
FROM ProductStock ps
JOIN Dupes d ON ps.ProductID = d.ProductID AND ps.ExpiryDate = d.ExpiryDate
WHERE ps.StockID = d.KeepStockID;
GO

-- Step 2: Delete duplicate rows
;WITH Dupes AS (
    SELECT ProductID, ExpiryDate, MIN(StockID) AS KeepStockID
    FROM ProductStock
    GROUP BY ProductID, ExpiryDate
    HAVING COUNT(*) > 1
)
DELETE ps
FROM ProductStock ps
JOIN Dupes d ON ps.ProductID = d.ProductID AND ps.ExpiryDate = d.ExpiryDate
WHERE ps.StockID <> d.KeepStockID;
GO

-- Step 3: Drop old non-unique index if it exists
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductStock_ProductID_ExpiryDate' AND object_id = OBJECT_ID('ProductStock'))
    DROP INDEX IX_ProductStock_ProductID_ExpiryDate ON ProductStock;
GO

-- Step 4: Add UNIQUE constraint
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_ProductStock_ProductID_ExpiryDate' AND object_id = OBJECT_ID('ProductStock'))
    ALTER TABLE ProductStock ADD CONSTRAINT UQ_ProductStock_ProductID_ExpiryDate UNIQUE (ProductID, ExpiryDate);
GO

GO

/* Migration: 014_ProductStockUpdatedAt.sql */

GO
-- Migration 014: audit timestamp for batch-level inventory adjustments
IF OBJECT_ID('ProductStock', 'U') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE ProductStock ADD UpdatedAt DATETIME2 NULL;
END
GO

GO

/* Migration: 015_SaleItemStockBatch.sql */

GO
-- Persist the exact inventory batch used by each new sale line.
IF OBJECT_ID('SaleItems', 'U') IS NOT NULL
   AND COL_LENGTH('SaleItems', 'StockID') IS NULL
BEGIN
    ALTER TABLE SaleItems ADD StockID INT NULL;
END
GO

GO

/* Migration: 016_ProductStockArchive.sql */

GO
-- Soft-archive batch rows so inactive batches never reappear in POS selection.
IF OBJECT_ID('ProductStock', 'U') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'IsArchived') IS NULL
BEGIN
    ALTER TABLE ProductStock ADD IsArchived BIT NOT NULL CONSTRAINT DF_ProductStock_IsArchived DEFAULT 0;
END
GO

GO

/* Migration: 017_ReturnItemBatchPricing.sql */

GO
-- Store the selected stock batch and the exact price/unit used by every return.
-- Existing return history remains valid; these columns are nullable/defaulted for old rows.
IF COL_LENGTH('ReturnItems', 'StockID') IS NULL
    ALTER TABLE ReturnItems ADD StockID INT NULL;

IF COL_LENGTH('ReturnItems', 'EnteredQuantity') IS NULL
    ALTER TABLE ReturnItems ADD EnteredQuantity INT NULL;

IF COL_LENGTH('ReturnItems', 'UnitType') IS NULL
    ALTER TABLE ReturnItems ADD UnitType NVARCHAR(20) NULL;

IF COL_LENGTH('ReturnItems', 'UnitPrice') IS NULL
    ALTER TABLE ReturnItems ADD UnitPrice DECIMAL(18, 4) NULL;

IF COL_LENGTH('ReturnItems', 'PiecesPerPack') IS NULL
    ALTER TABLE ReturnItems ADD PiecesPerPack INT NULL;

GO

/* Migration: 018_ReadQueryPerformanceIndexes.sql */

GO
-- Read-only performance indexes. The application schema uses ProductStock/ProductID
-- and Products.IsActive (not EF's ProductStocks/IsArchived convention).
IF OBJECT_ID('ProductStock', 'U') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'ProductID') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'IsArchived') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'QuantityAvailable') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'ExpiryDate') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'PurchasePrice') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'MRP') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ProductStock') AND name = 'IX_ProductStock_ActiveLookup')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProductStock_ActiveLookup
        ON ProductStock (ProductID, IsArchived, QuantityAvailable)
        INCLUDE (ExpiryDate, PurchasePrice, MRP);
END
GO

IF OBJECT_ID('Products', 'U') IS NOT NULL
   AND COL_LENGTH('Products', 'IsActive') IS NOT NULL
   AND COL_LENGTH('Products', 'Name') IS NOT NULL
   AND COL_LENGTH('Products', 'PCode') IS NOT NULL
   AND COL_LENGTH('Products', 'CompanyID') IS NOT NULL
   AND COL_LENGTH('Products', 'PiecesPerUnit') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Products') AND name = 'IX_Products_ActiveName')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Products_ActiveName
        ON Products (IsActive, Name)
        INCLUDE (PCode, CompanyID, PiecesPerUnit);
END
GO

GO

/* Migration: 019_NotificationReadStates.sql */

GO
-- Persists workflow-notification read status for each signed-in user.
-- Notifications themselves are generated from Prescription workflow events, so a
-- user/key read-state table avoids duplicating those business records.
IF OBJECT_ID('NotificationReadStates', 'U') IS NULL
BEGIN
    CREATE TABLE NotificationReadStates (
        UserID INT NOT NULL,
        NotificationKey NVARCHAR(250) NOT NULL,
        ReadAt DATETIME2 NOT NULL CONSTRAINT DF_NotificationReadStates_ReadAt DEFAULT SYSDATETIME(),
        CONSTRAINT PK_NotificationReadStates PRIMARY KEY (UserID, NotificationKey),
        CONSTRAINT FK_NotificationReadStates_Users FOREIGN KEY (UserID) REFERENCES Users(UserID)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_NotificationReadStates_UserID'
      AND object_id = OBJECT_ID('NotificationReadStates'))
BEGIN
    CREATE INDEX IX_NotificationReadStates_UserID
        ON NotificationReadStates (UserID, ReadAt DESC);
END
GO

GO

/* Migration: AddCNIC.sql */

GO
IF OBJECT_ID('Patients', 'U') IS NOT NULL AND COL_LENGTH('Patients', 'CNIC') IS NULL
    ALTER TABLE Patients ADD CNIC NVARCHAR(50) NULL;
GO
IF OBJECT_ID('Suppliers', 'U') IS NOT NULL AND COL_LENGTH('Suppliers', 'CNIC') IS NULL
    ALTER TABLE Suppliers ADD CNIC NVARCHAR(50) NULL;
GO

