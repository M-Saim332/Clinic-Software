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
UPDATE Appointments SET Status = 'Scheduled' WHERE Status = 'Checked-In';

ALTER TABLE Appointments ADD CONSTRAINT CK_Appointments_Status
    CHECK (Status IN ('Scheduled', 'Completed', 'Cancelled', 'Missed'));

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
