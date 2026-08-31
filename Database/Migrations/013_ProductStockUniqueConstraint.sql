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
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_ProductStock_ProductID_ExpiryDate'' AND object_id = OBJECT_ID(''ProductStock''))
    DROP INDEX IX_ProductStock_ProductID_ExpiryDate ON ProductStock;
GO

-- Step 4: Add UNIQUE constraint
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''UQ_ProductStock_ProductID_ExpiryDate'' AND object_id = OBJECT_ID(''ProductStock''))
    ALTER TABLE ProductStock ADD CONSTRAINT UQ_ProductStock_ProductID_ExpiryDate UNIQUE (ProductID, ExpiryDate);
GO
