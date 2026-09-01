-- Soft-archive batch rows so inactive batches never reappear in POS selection.
IF OBJECT_ID('ProductStock', 'U') IS NOT NULL
   AND COL_LENGTH('ProductStock', 'IsArchived') IS NULL
BEGIN
    ALTER TABLE ProductStock ADD IsArchived BIT NOT NULL CONSTRAINT DF_ProductStock_IsArchived DEFAULT 0;
END
GO
