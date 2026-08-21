-- Doctor -> Pharmacist -> Receptionist prescription handoff
-- Safe to run repeatedly.

IF COL_LENGTH('Prescriptions', 'WorkflowStatus') IS NULL
    ALTER TABLE Prescriptions ADD WorkflowStatus VARCHAR(30) NOT NULL CONSTRAINT DF_Prescriptions_WorkflowStatus DEFAULT 'Draft';

IF COL_LENGTH('Prescriptions', 'SentToPharmacyAt') IS NULL
    ALTER TABLE Prescriptions ADD SentToPharmacyAt DATETIME NULL;

IF COL_LENGTH('Prescriptions', 'PrintedAt') IS NULL
    ALTER TABLE Prescriptions ADD PrintedAt DATETIME NULL;

IF COL_LENGTH('Prescriptions', 'DispensedAt') IS NULL
    ALTER TABLE Prescriptions ADD DispensedAt DATETIME NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Prescriptions_WorkflowStatus')
    CREATE INDEX IX_Prescriptions_WorkflowStatus ON Prescriptions(WorkflowStatus, SentToPharmacyAt DESC);
GO
