-- Persist the exact inventory batch used by each new sale line.
IF OBJECT_ID('SaleItems', 'U') IS NOT NULL
   AND COL_LENGTH('SaleItems', 'StockID') IS NULL
BEGIN
    ALTER TABLE SaleItems ADD StockID INT NULL;
END
GO
