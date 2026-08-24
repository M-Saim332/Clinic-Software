-- Migration: allow Assistant users in the Users.Role check constraint.
-- Safe to run against existing ClinicDB databases.

DECLARE @constraintName sysname;

SELECT @constraintName = cc.name
FROM sys.check_constraints cc
JOIN sys.columns c
  ON c.object_id = cc.parent_object_id
 AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('Users')
  AND c.name = 'Role';

IF @constraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE Users DROP CONSTRAINT ' + QUOTENAME(@constraintName));
END

ALTER TABLE Users ADD CONSTRAINT CK_Users_Role
    CHECK (Role IN ('Doctor', 'Receptionist', 'Admin', 'Pharmacist', 'Assistant'));

PRINT 'CK_Users_Role updated - Assistant role now allowed.';
