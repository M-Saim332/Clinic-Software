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
