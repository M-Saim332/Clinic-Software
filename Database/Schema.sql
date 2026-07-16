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
--  DROP TABLES (if they exist, in reverse dependency order)
-- ============================================================
IF OBJECT_ID('PrescriptionItems', 'U') IS NOT NULL DROP TABLE PrescriptionItems;
IF OBJECT_ID('Prescriptions', 'U') IS NOT NULL DROP TABLE Prescriptions;
IF OBJECT_ID('SaleItems', 'U') IS NOT NULL DROP TABLE SaleItems;
IF OBJECT_ID('Sales', 'U') IS NOT NULL DROP TABLE Sales;
IF OBJECT_ID('PurchaseItems', 'U') IS NOT NULL DROP TABLE PurchaseItems;
IF OBJECT_ID('Purchases', 'U') IS NOT NULL DROP TABLE Purchases;
IF OBJECT_ID('Appointments', 'U') IS NOT NULL DROP TABLE Appointments;
IF OBJECT_ID('Patients', 'U') IS NOT NULL DROP TABLE Patients;
IF OBJECT_ID('Medicines', 'U') IS NOT NULL DROP TABLE Medicines;
IF OBJECT_ID('Products', 'U') IS NOT NULL DROP TABLE Products;
IF OBJECT_ID('Suppliers', 'U') IS NOT NULL DROP TABLE Suppliers;
IF OBJECT_ID('Companies', 'U') IS NOT NULL DROP TABLE Companies;
IF OBJECT_ID('DiscountRefunds', 'U') IS NOT NULL DROP TABLE DiscountRefunds;
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
GO

-- ============================================================
--  CREATE TABLES
-- ============================================================

CREATE TABLE Companies (
    CompanyID INT IDENTITY(1,1) PRIMARY KEY,
    Name      VARCHAR(150) NOT NULL,
    Address   VARCHAR(255),
    Phone     VARCHAR(50),
    Email     VARCHAR(150)
);
GO

CREATE TABLE Suppliers (
    SupplierID INT IDENTITY(1,1) PRIMARY KEY,
    Name       VARCHAR(150) NOT NULL,
    Address    VARCHAR(255),
    Phone      VARCHAR(50),
    Email      VARCHAR(150)
);
GO

CREATE TABLE Products (
    ProductID     INT IDENTITY(1,1) PRIMARY KEY,
    CompanyID     INT FOREIGN KEY REFERENCES Companies(CompanyID),
    Name          VARCHAR(150) NOT NULL,
    PurchaseRate  DECIMAL(10,2) DEFAULT 0,
    SellingPrice  DECIMAL(10,2) DEFAULT 0,
    Tax           DECIMAL(5,2) DEFAULT 0,
    StockQuantity INT DEFAULT 0
);
GO

CREATE TABLE Medicines (
    MedicineID        INT IDENTITY(1,1) PRIMARY KEY,
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
    PurchasePrice     DECIMAL(10,2) DEFAULT 0,
    SellingPrice      DECIMAL(10,2) DEFAULT 0,
    Stock             INT DEFAULT 0,
    MinimumStockLevel INT DEFAULT 0
);
GO

CREATE TABLE Patients (
    PatientID       INT IDENTITY(1,1) PRIMARY KEY,
    Name            VARCHAR(150) NOT NULL,
    Age             INT,
    Gender          VARCHAR(10) CHECK (Gender IN ('Male', 'Female', 'Other')),
    Phone           VARCHAR(50),
    Address         VARCHAR(255),
    Diagnosis       TEXT,
    Prescription    TEXT,
    ConsultationFee DECIMAL(10,2) DEFAULT 0,
    Discount        DECIMAL(10,2) DEFAULT 0,
    NextAppointmentDate DATE,
    NextAppointmentTime TIME
);
GO

CREATE TABLE Users (
    UserID       INT IDENTITY(1,1) PRIMARY KEY,
    Username     VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    Role         VARCHAR(20)  NOT NULL CHECK (Role IN ('Doctor', 'Receptionist', 'Admin', 'Pharmacist')),
    FullName     VARCHAR(150) NULL,
    IsActive     BIT DEFAULT 1,
    Permissions  VARCHAR(1000) NULL,
    CreatedAt    DATETIME DEFAULT GETDATE()
);
GO

CREATE TABLE Appointments (
    AppointmentID      INT IDENTITY(1,1) PRIMARY KEY,
    AppointmentNo      VARCHAR(50) NOT NULL,
    PatientID          INT FOREIGN KEY REFERENCES Patients(PatientID),
    PatientName        VARCHAR(150),
    Phone              VARCHAR(50),
    DoctorID           INT FOREIGN KEY REFERENCES Users(UserID),
    AppointmentDate    DATE NOT NULL,
    AppointmentTime    TIME NOT NULL,
    Reason             VARCHAR(255),
    Status             VARCHAR(20) NOT NULL DEFAULT 'Scheduled' CHECK (Status IN ('Scheduled', 'Completed', 'Cancelled', 'Missed')),
    Remarks            VARCHAR(255),
    CancellationReason VARCHAR(255),
    CreatedAt          DATETIME DEFAULT GETDATE()
);
GO

CREATE TABLE Purchases (
    PurchaseID    INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceNumber VARCHAR(50) NOT NULL,
    PurchaseDate  DATETIME DEFAULT GETDATE(),
    SupplierID    INT FOREIGN KEY REFERENCES Suppliers(SupplierID),
    SupplierName  VARCHAR(150),
    TotalAmount   DECIMAL(12,2) DEFAULT 0
);
GO

CREATE TABLE PurchaseItems (
    PurchaseItemID INT IDENTITY(1,1) PRIMARY KEY,
    PurchaseID     INT FOREIGN KEY REFERENCES Purchases(PurchaseID) ON DELETE CASCADE,
    ProductID      INT FOREIGN KEY REFERENCES Products(ProductID),
    BatchNumber    VARCHAR(50),
    ExpiryDate     DATE,
    Quantity       INT NOT NULL,
    PurchasePrice  DECIMAL(10,2),
    Discount       DECIMAL(10,2) DEFAULT 0,
    Tax            DECIMAL(5,2) DEFAULT 0
);
GO

CREATE TABLE Sales (
    SaleID          INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceNumber   VARCHAR(50) NOT NULL,
    SaleDate        DATETIME DEFAULT GETDATE(),
    PatientID       INT FOREIGN KEY REFERENCES Patients(PatientID),
    PatientName     VARCHAR(150),
    ConsultationFee DECIMAL(10,2) DEFAULT 0,
    GrandTotal      DECIMAL(12,2) DEFAULT 0,
    PaymentMethod   VARCHAR(20) CHECK (PaymentMethod IN ('Cash', 'Card', 'Online')),
    IsPosted        BIT DEFAULT 0
);
GO

CREATE TABLE SaleItems (
    SaleItemID INT IDENTITY(1,1) PRIMARY KEY,
    SaleID     INT FOREIGN KEY REFERENCES Sales(SaleID) ON DELETE CASCADE,
    MedicineID INT FOREIGN KEY REFERENCES Medicines(MedicineID),
    Quantity   INT NOT NULL,
    Discount   DECIMAL(10,2) DEFAULT 0,
    Tax        DECIMAL(5,2) DEFAULT 0,
    LineTotal  DECIMAL(10,2) DEFAULT 0
);
GO

-- DiscountRefunds: doctor-approved refunds with full audit trail
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
GO

-- ============================================================
--  INDEXES
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Patients_Name')
    CREATE INDEX IX_Patients_Name ON Patients(Name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Medicines_Name')
    CREATE INDEX IX_Medicines_Name ON Medicines(Name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Appointments_Date')
    CREATE INDEX IX_Appointments_Date ON Appointments(AppointmentDate);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_DiscountRefunds_IsCompleted')
    CREATE INDEX IX_DiscountRefunds_IsCompleted ON DiscountRefunds(IsCompleted);
GO

-- ============================================================
--  SEED DATA — Default admin user (Doctor)
--  Password: Admin@123
--  BCrypt hash generated offline; change after first login.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (Username, PasswordHash, Role, FullName, IsActive)
    VALUES (
        'admin',
        '$2a$11$u0LyGgHmhN2kTeoBK.a5m.FVHXHSUA/xHZFJ9tE1O4Oj4QvICWT.O',  -- Admin@123
        'Doctor',
        'System Admin',
        1
    );
END
GO

PRINT 'ClinicDB expanded schema created successfully.';
GO
