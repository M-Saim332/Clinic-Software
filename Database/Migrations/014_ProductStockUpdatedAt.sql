-- Migration 014: audit timestamp for batch-level inventory adjustments
IF OBJECT_ID('ProductStock', 'U') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE ProductStock ADD UpdatedAt DATETIME2 NULL;
END
GO
