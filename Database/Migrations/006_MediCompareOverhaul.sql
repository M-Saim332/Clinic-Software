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
