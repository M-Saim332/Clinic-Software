-- Migration 004: Sync Appointments with Patient Waiting List
-- Run against ClinicDB (safe to re-run)

USE ClinicDB;
GO

-- Patients: add VisitStatus and LastVisitDate for waiting list tracking
IF COL_LENGTH('Patients', 'VisitStatus') IS NULL
    ALTER TABLE Patients ADD VisitStatus VARCHAR(20) NULL;
GO

IF COL_LENGTH('Patients', 'LastVisitDate') IS NULL
    ALTER TABLE Patients ADD LastVisitDate DATE NULL;
GO

-- NextAppointmentTime already added in 003, but guard just in case
IF COL_LENGTH('Patients', 'NextAppointmentTime') IS NULL
    ALTER TABLE Patients ADD NextAppointmentTime TIME NULL;
GO

-- Appointments: add Gender and Age columns for walk-in patients
IF COL_LENGTH('Appointments', 'Gender') IS NULL
    ALTER TABLE Appointments ADD Gender VARCHAR(10) NULL;
GO

IF COL_LENGTH('Appointments', 'Age') IS NULL
    ALTER TABLE Appointments ADD Age INT NULL;
GO
