-- Migration: allow Assistant users in the Users.Role check constraint.
-- Safe to run against existing ClinicDB databases.

DECLARE @constraintName sysname;

SELECT TOP (1) @constraintName = cc.name
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Users')
  AND CHARINDEX(N'Role', cc.definition) > 0;

IF @constraintName IS NOT NULL
BEGIN
    DECLARE @dropRoleConstraintSql nvarchar(500) =
        N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(@constraintName) + N';';
    EXEC sys.sp_executesql @dropRoleConstraintSql;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
      AND name = N'CK_Users_Role')
BEGIN
    ALTER TABLE dbo.Users ADD CONSTRAINT CK_Users_Role
        CHECK (Role IN ('Doctor', 'Receptionist', 'Admin', 'Pharmacist', 'Assistant'));
END

PRINT 'CK_Users_Role updated - Assistant role now allowed.';
