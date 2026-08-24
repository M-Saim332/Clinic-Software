-- Migration: add company sales tax percentage to purchase invoice lines.
-- Safe to run against existing ClinicDB databases.

IF COL_LENGTH('PurchaseItems', 'CompanySalesTax') IS NULL
BEGIN
    ALTER TABLE PurchaseItems
        ADD CompanySalesTax DECIMAL(5,2) NOT NULL
            CONSTRAINT DF_PurchaseItems_CompanySalesTax DEFAULT 0;
END
