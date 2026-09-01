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
