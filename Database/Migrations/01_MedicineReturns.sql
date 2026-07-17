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
