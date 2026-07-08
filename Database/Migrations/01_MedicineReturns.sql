USE ClinicDB;
GO

-- Add IsReturnable column to Medicines if it doesn't exist
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE Name = N'IsReturnable' AND Object_ID = Object_ID(N'Medicines')
)
BEGIN
    ALTER TABLE Medicines
    ADD IsReturnable BIT NOT NULL DEFAULT 1;
END
GO

-- Create MedicineReturns table
IF OBJECT_ID('MedicineReturns', 'U') IS NULL
BEGIN
    CREATE TABLE MedicineReturns (
        ReturnId INT IDENTITY(1,1) PRIMARY KEY,
        SaleId INT NOT NULL FOREIGN KEY REFERENCES Sales(SaleId),
        MedicineId INT NOT NULL FOREIGN KEY REFERENCES Medicines(MedicineId),
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
