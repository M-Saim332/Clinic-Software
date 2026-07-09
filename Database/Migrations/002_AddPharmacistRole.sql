-- ============================================================
--  Migration 002: Add 'Pharmacist' to Users.Role CHECK constraint
--  Run this once on any live ClinicDB instance.
-- ============================================================

USE ClinicDB;
GO

-- Drop the old CHECK constraint dynamically
DECLARE @ConstraintName NVARCHAR(256);
SELECT @ConstraintName = name
FROM   sys.check_constraints
WHERE  parent_object_id = OBJECT_ID('Users')
AND    CHARINDEX('Role', definition) > 0;

IF @ConstraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE Users DROP CONSTRAINT [' + @ConstraintName + ']');
    PRINT 'Dropped old Role CHECK constraint: ' + @ConstraintName;
END

-- Recreate with Pharmacist included
ALTER TABLE Users
    ADD CONSTRAINT CK_Users_Role
    CHECK (Role IN ('Doctor', 'Receptionist', 'Admin', 'Pharmacist'));

PRINT 'CK_Users_Role updated — Pharmacist role now allowed.';
GO
