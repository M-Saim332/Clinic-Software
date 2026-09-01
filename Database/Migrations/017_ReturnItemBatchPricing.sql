-- Store the selected stock batch and the exact price/unit used by every return.
-- Existing return history remains valid; these columns are nullable/defaulted for old rows.
IF COL_LENGTH('ReturnItems', 'StockID') IS NULL
    ALTER TABLE ReturnItems ADD StockID INT NULL;

IF COL_LENGTH('ReturnItems', 'EnteredQuantity') IS NULL
    ALTER TABLE ReturnItems ADD EnteredQuantity INT NULL;

IF COL_LENGTH('ReturnItems', 'UnitType') IS NULL
    ALTER TABLE ReturnItems ADD UnitType NVARCHAR(20) NULL;

IF COL_LENGTH('ReturnItems', 'UnitPrice') IS NULL
    ALTER TABLE ReturnItems ADD UnitPrice DECIMAL(18, 4) NULL;

IF COL_LENGTH('ReturnItems', 'PiecesPerPack') IS NULL
    ALTER TABLE ReturnItems ADD PiecesPerPack INT NULL;
