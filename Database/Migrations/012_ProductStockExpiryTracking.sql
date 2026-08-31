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
        -- Insert existing stock into ProductStock with a dummy far-future expiry date
        -- Note: We could try to infer from PurchaseItems, but since old data didn't strictly link stock to expiry,
        -- setting a safe date ensures the existing stock remains sellable under the new FEFO logic.
        INSERT INTO ProductStock (ProductID, ExpiryDate, QuantityAvailable, PurchasePrice, MRP)
        SELECT 
            ProductID, 
            COALESCE(ExpiryDate, '2099-12-31'), -- Use product's ExpiryDate if exists, else far future
            Stock, 
            PurchasePrice, 
            SellingPrice
        FROM Products
        WHERE Stock > 0;
    END

    -- Drop the old Stock column from Products (and its constraints if any)
    DECLARE @DefaultConstraintName NVARCHAR(200);
    SELECT @DefaultConstraintName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID('Products') AND c.name = 'Stock';

    IF @DefaultConstraintName IS NOT NULL
        EXEC('ALTER TABLE Products DROP CONSTRAINT ' + @DefaultConstraintName);

    ALTER TABLE Products DROP COLUMN Stock;
END
GO
