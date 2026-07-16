-- ============================================================
--  MIGRATION: Add DiscountRefunds table
--  Run this ONCE on existing ClinicDB databases.
--  Safe to run multiple times (IF NOT EXISTS guard).
-- ============================================================
USE ClinicDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DiscountRefunds')
BEGIN
    CREATE TABLE DiscountRefunds (
        RefundID          INT IDENTITY(1,1) PRIMARY KEY,
        PatientName       NVARCHAR(150) NOT NULL,
        TokenNumber       VARCHAR(50),
        OriginalFee       DECIMAL(10,2) NOT NULL,
        DiscountedFee     DECIMAL(10,2) NOT NULL,
        RefundAmount      AS (OriginalFee - DiscountedFee) PERSISTED,
        Notes             NVARCHAR(500),
        ApprovedByUserID  INT REFERENCES Users(UserID),
        ApprovedByName    NVARCHAR(150),
        ApprovedAt        DATETIME DEFAULT GETDATE(),
        CompletedByUserID INT REFERENCES Users(UserID),
        CompletedByName   NVARCHAR(150),
        CompletedAt       DATETIME,
        IsCompleted       BIT DEFAULT 0
    );

    -- Also add PatientName column to Sales if not already present
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'PatientName')
        ALTER TABLE Sales ADD PatientName NVARCHAR(150);

    PRINT 'DiscountRefunds table created successfully.';
END
ELSE
BEGIN
    PRINT 'DiscountRefunds table already exists — no changes made.';
END
GO
