-- Persists workflow-notification read status for each signed-in user.
-- Notifications themselves are generated from Prescription workflow events, so a
-- user/key read-state table avoids duplicating those business records.
IF OBJECT_ID('NotificationReadStates', 'U') IS NULL
BEGIN
    CREATE TABLE NotificationReadStates (
        UserID INT NOT NULL,
        NotificationKey NVARCHAR(250) NOT NULL,
        ReadAt DATETIME2 NOT NULL CONSTRAINT DF_NotificationReadStates_ReadAt DEFAULT SYSDATETIME(),
        CONSTRAINT PK_NotificationReadStates PRIMARY KEY (UserID, NotificationKey),
        CONSTRAINT FK_NotificationReadStates_Users FOREIGN KEY (UserID) REFERENCES Users(UserID)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_NotificationReadStates_UserID'
      AND object_id = OBJECT_ID('NotificationReadStates'))
BEGIN
    CREATE INDEX IX_NotificationReadStates_UserID
        ON NotificationReadStates (UserID, ReadAt DESC);
END
GO
