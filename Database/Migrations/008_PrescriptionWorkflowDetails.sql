-- Migration: Add AppointmentID and PharmacistID to Prescriptions for full-stack workflow tracking
-- Safe to run repeatedly.

IF COL_LENGTH('Prescriptions', 'AppointmentID') IS NULL
BEGIN
    ALTER TABLE Prescriptions ADD AppointmentID INT NULL;
    ALTER TABLE Prescriptions ADD CONSTRAINT FK_Prescriptions_Appointments FOREIGN KEY (AppointmentID) REFERENCES Appointments(AppointmentID);
END

IF COL_LENGTH('Prescriptions', 'PharmacistID') IS NULL
BEGIN
    ALTER TABLE Prescriptions ADD PharmacistID INT NULL;
    ALTER TABLE Prescriptions ADD CONSTRAINT FK_Prescriptions_Pharmacist FOREIGN KEY (PharmacistID) REFERENCES Users(UserID);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Prescriptions_AppointmentID')
BEGIN
    CREATE INDEX IX_Prescriptions_AppointmentID ON Prescriptions(AppointmentID);
END
GO
