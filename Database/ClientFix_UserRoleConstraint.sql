/*
 Immediate client repair for CK_Users_Role.

 Safe to run against an existing ClinicDB. No user records are deleted or
 modified. Only the Users.Role validation constraint is replaced.
*/
USE [ClinicDB];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @DropRoleConstraintsSql NVARCHAR(MAX) = N'';

SELECT @DropRoleConstraintsSql = @DropRoleConstraintsSql
    + N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(name) + N';'
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
  AND CHARINDEX(N'Role', definition) > 0;

IF LEN(@DropRoleConstraintsSql) > 0
    EXEC sys.sp_executesql @DropRoleConstraintsSql;

ALTER TABLE dbo.Users WITH CHECK
    ADD CONSTRAINT CK_Users_Role
    CHECK (Role IN ('Doctor', 'Receptionist', 'Admin', 'Pharmacist', 'Assistant'));

COMMIT TRANSACTION;
GO

SELECT name, definition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
  AND name = N'CK_Users_Role';
GO
