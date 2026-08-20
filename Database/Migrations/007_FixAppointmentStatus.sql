-- Migration 007: Restore 'Checked-In' as a valid Appointments status
-- The previous migration 003 incorrectly removed 'Checked-In' from the check
-- constraint. This migration corrects that for all existing databases.
-- Safe to run multiple times.

USE ClinicDB;
GO

-- Drop and recreate the status check constraint to include 'Checked-In'
DECLARE @con NVARCHAR(200);
SELECT @con = dc.name
FROM sys.check_constraints dc
JOIN sys.columns c
    ON dc.parent_object_id = c.object_id
    AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID('Appointments')
  AND c.name = 'Status';

IF @con IS NOT NULL
    EXEC('ALTER TABLE Appointments DROP CONSTRAINT [' + @con + ']');

-- The app uses 'Checked-In' in MainWindowViewModel and ClinicalDashboardViewModel.
-- Ensure the constraint allows all five statuses.
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('Appointments')
      AND name = 'CK_Appointments_Status'
)
BEGIN
    ALTER TABLE Appointments ADD CONSTRAINT CK_Appointments_Status
        CHECK (Status IN ('Scheduled', 'Checked-In', 'Completed', 'Cancelled', 'Missed'));
END
GO

-- Ensure the CNIC column is present (added in 006 but guarded here for safety)
IF COL_LENGTH('Appointments', 'CNIC') IS NULL
    ALTER TABLE Appointments ADD CNIC NVARCHAR(50) NULL;
GO

PRINT 'Migration 007 complete: Appointments.Status constraint updated to include Checked-In.';
GO
