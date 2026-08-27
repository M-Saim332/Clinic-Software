-- ============================================================
--  Migration 011: Add Rate (Gross Trade Price) column to Products
--  Run against existing ClinicDB instances.
--  Idempotent — safe to execute multiple times.
-- ============================================================

USE ClinicDB;
GO

-- 1. Add the Rate column if it does not already exist
IF COL_LENGTH('Products', 'Rate') IS NULL
BEGIN
    ALTER TABLE Products
        ADD Rate DECIMAL(10,2) NULL CONSTRAINT DF_Products_Rate DEFAULT 0.00;

    -- Back-fill from SellingPrice as a safe starting point
    UPDATE Products SET Rate = SellingPrice WHERE Rate IS NULL;

    -- Tighten to NOT NULL now that all rows have a value
    ALTER TABLE Products
        ALTER COLUMN Rate DECIMAL(10,2) NOT NULL;

    PRINT 'Products.Rate column added and back-filled.';
END
ELSE
BEGIN
    PRINT 'Products.Rate already exists — no action taken.';
END
GO
