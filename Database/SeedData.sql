-- ============================================================
--  Clinic Management System -- Real Seed Data
--  Idempotent: Safe to run multiple times on any existing DB.
--  Uses realistic Pakistani clinic data.
-- ============================================================

USE ClinicDB;
GO

SET NOCOUNT ON;

-- ============================================================
--  1. COMPANIES (Pharmaceutical Companies)
-- ============================================================
PRINT 'Seeding Companies...';

IF NOT EXISTS (SELECT 1 FROM Companies WHERE Name = 'Abbott Laboratories')
    INSERT INTO Companies (Name, Address, Phone, Email) VALUES ('Abbott Laboratories', 'Plot 8, Korangi Industrial Area, Karachi', '021-35060011', 'info@abbott.com.pk');

IF NOT EXISTS (SELECT 1 FROM Companies WHERE Name = 'Getz Pharma')
    INSERT INTO Companies (Name, Address, Phone, Email) VALUES ('Getz Pharma', 'Getz House, 7 Tipu Sultan Road, Karachi', '021-38771000', 'info@getzpharma.com');

IF NOT EXISTS (SELECT 1 FROM Companies WHERE Name = 'Ferozsons Laboratories')
    INSERT INTO Companies (Name, Address, Phone, Email) VALUES ('Ferozsons Laboratories', '50-B, Tipu Block, Garden Town, Lahore', '042-35761551', 'info@ferozsons.com.pk');

IF NOT EXISTS (SELECT 1 FROM Companies WHERE Name = 'Sami Pharmaceuticals')
    INSERT INTO Companies (Name, Address, Phone, Email) VALUES ('Sami Pharmaceuticals', 'Plot B-23, S.I.T.E., Karachi', '021-32573012', 'info@sami.com.pk');

IF NOT EXISTS (SELECT 1 FROM Companies WHERE Name = 'Martin Dow')
    INSERT INTO Companies (Name, Address, Phone, Email) VALUES ('Martin Dow', '7 Ahmed Block, Garden Town, Lahore', '042-35752920', 'info@martindow.com');

IF NOT EXISTS (SELECT 1 FROM Companies WHERE Name = 'Pfizer Pakistan')
    INSERT INTO Companies (Name, Address, Phone, Email) VALUES ('Pfizer Pakistan', 'Plot 23, Sector 22, Korangi Industrial Area, Karachi', '021-35121111', 'pk.medinfo@pfizer.com');

IF NOT EXISTS (SELECT 1 FROM Companies WHERE Name = 'Searle Pakistan')
    INSERT INTO Companies (Name, Address, Phone, Email) VALUES ('Searle Pakistan', 'Searle House, 15-Dockyard Road, Karachi', '021-32312580', 'info@searle.com.pk');
GO

-- ============================================================
--  2. SUPPLIERS (Medicine Distributors)
-- ============================================================
PRINT 'Seeding Suppliers...';

IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE Name = 'Fazal Din Pharma')
    INSERT INTO Suppliers (Name, Address, Phone, Email) VALUES ('Fazal Din Pharma', 'Main Hafeez Centre, Gulberg III, Lahore', '042-35762100', 'orders@fazaldin.com.pk');

IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE Name = 'Shaheen Medical Store')
    INSERT INTO Suppliers (Name, Address, Phone, Email) VALUES ('Shaheen Medical Store', 'Near DHQ Hospital, Rawalpindi', '051-5580123', 'shaheen.medical@gmail.com');

IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE Name = 'Al-Madina Pharmaceuticals')
    INSERT INTO Suppliers (Name, Address, Phone, Email) VALUES ('Al-Madina Pharmaceuticals', 'Urdu Bazaar, Lahore', '042-37320011', 'almadina.pharma@gmail.com');

IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE Name = 'Haider Brothers Medical')
    INSERT INTO Suppliers (Name, Address, Phone, Email) VALUES ('Haider Brothers Medical', 'Saddar, Karachi', '021-32720456', 'haiderbrothers.med@gmail.com');
GO

-- ============================================================
--  3. PRODUCTS (Common Pakistani Medicines)
-- ============================================================
PRINT 'Seeding Products...';

DECLARE @AbbottID INT = (SELECT TOP 1 CompanyID FROM Companies WHERE Name = 'Abbott Laboratories');
DECLARE @GetzID   INT = (SELECT TOP 1 CompanyID FROM Companies WHERE Name = 'Getz Pharma');
DECLARE @FeroID   INT = (SELECT TOP 1 CompanyID FROM Companies WHERE Name = 'Ferozsons Laboratories');
DECLARE @SamiID   INT = (SELECT TOP 1 CompanyID FROM Companies WHERE Name = 'Sami Pharmaceuticals');
DECLARE @MartinID INT = (SELECT TOP 1 CompanyID FROM Companies WHERE Name = 'Martin Dow');
DECLARE @PfizerID INT = (SELECT TOP 1 CompanyID FROM Companies WHERE Name = 'Pfizer Pakistan');
DECLARE @SearleID INT = (SELECT TOP 1 CompanyID FROM Companies WHERE Name = 'Searle Pakistan');

DECLARE @Sup1ID INT = (SELECT TOP 1 SupplierID FROM Suppliers WHERE Name = 'Fazal Din Pharma');
DECLARE @Sup2ID INT = (SELECT TOP 1 SupplierID FROM Suppliers WHERE Name = 'Shaheen Medical Store');

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Panadol 500mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Panadol 500mg', 'Paracetamol', @GetzID, 'Getz Pharma', @Sup1ID, 'Fazal Din Pharma', 'GZ-2025-001', 'Tablet', 'Analgesic/Antipyretic', 'A-1', '2027-06-30', 35.00, 65.00, 10, 500, 50, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Brufen 400mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Brufen 400mg', 'Ibuprofen', @AbbottID, 'Abbott Laboratories', @Sup1ID, 'Fazal Din Pharma', 'AB-2025-012', 'Tablet', 'NSAID/Analgesic', 'A-2', '2027-09-30', 45.00, 90.00, 10, 300, 30, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Amoxil 500mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Amoxil 500mg', 'Amoxicillin', @GetzID, 'Getz Pharma', @Sup2ID, 'Shaheen Medical Store', 'GZ-2025-044', 'Capsule', 'Antibiotic', 'B-1', '2027-03-31', 80.00, 150.00, 10, 200, 20, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Risek 20mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Risek 20mg', 'Omeprazole', @GetzID, 'Getz Pharma', @Sup1ID, 'Fazal Din Pharma', 'GZ-2025-088', 'Capsule', 'Proton Pump Inhibitor', 'B-2', '2027-12-31', 60.00, 120.00, 14, 280, 28, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Glucophage 500mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Glucophage 500mg', 'Metformin', @MartinID, 'Martin Dow', @Sup1ID, 'Fazal Din Pharma', 'MD-2025-007', 'Tablet', 'Antidiabetic', 'C-1', '2028-01-31', 30.00, 55.00, 20, 400, 40, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Concor 5mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Concor 5mg', 'Bisoprolol', @MartinID, 'Martin Dow', @Sup2ID, 'Shaheen Medical Store', 'MD-2025-019', 'Tablet', 'Beta Blocker/Cardiac', 'C-2', '2027-08-31', 90.00, 160.00, 30, 180, 18, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Calpol 120mg Syrup')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Calpol 120mg Syrup', 'Paracetamol', @GetzID, 'Getz Pharma', @Sup1ID, 'Fazal Din Pharma', 'GZ-2025-102', 'Syrup', 'Analgesic/Antipyretic', 'D-1', '2026-11-30', 55.00, 100.00, 1, 120, 12, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Augmentin 625mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Augmentin 625mg', 'Amoxicillin + Clavulanate', @GetzID, 'Getz Pharma', @Sup2ID, 'Shaheen Medical Store', 'GZ-2025-055', 'Tablet', 'Antibiotic', 'B-3', '2027-05-31', 140.00, 260.00, 7, 140, 14, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Lipitor 20mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Lipitor 20mg', 'Atorvastatin', @PfizerID, 'Pfizer Pakistan', @Sup1ID, 'Fazal Din Pharma', 'PF-2025-033', 'Tablet', 'Statin/Lipid Lowering', 'C-3', '2028-03-31', 110.00, 200.00, 30, 150, 15, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Norvasc 5mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Norvasc 5mg', 'Amlodipine', @PfizerID, 'Pfizer Pakistan', @Sup2ID, 'Shaheen Medical Store', 'PF-2025-041', 'Tablet', 'Calcium Channel Blocker', 'C-4', '2028-06-30', 85.00, 155.00, 30, 200, 20, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Flagyl 400mg')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Flagyl 400mg', 'Metronidazole', @SamiID, 'Sami Pharmaceuticals', @Sup1ID, 'Fazal Din Pharma', 'SM-2025-021', 'Tablet', 'Antibiotic/Antiprotozoal', 'B-4', '2027-04-30', 25.00, 50.00, 10, 350, 35, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'Clarinase Repetabs')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('Clarinase Repetabs', 'Loratadine + Pseudoephedrine', @SearleID, 'Searle Pakistan', @Sup1ID, 'Fazal Din Pharma', 'SR-2025-009', 'Tablet', 'Antihistamine/Decongestant', 'E-1', '2027-10-31', 70.00, 130.00, 10, 100, 10, 1, 1);

IF NOT EXISTS (SELECT 1 FROM Products WHERE Name = 'ORS Sachets (Lemon)')
    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES ('ORS Sachets (Lemon)', 'Oral Rehydration Salts', @FeroID, 'Ferozsons Laboratories', @Sup2ID, 'Shaheen Medical Store', 'FE-2025-018', 'Sachet', 'Electrolyte', 'E-2', '2028-01-31', 10.00, 20.00, 1, 600, 60, 1, 1);
GO

-- ============================================================
--  4. PATIENTS (Real Pakistani Names)
-- ============================================================
PRINT 'Seeding Patients...';

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Muhammad Asif Khan')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Muhammad Asif Khan', 52, 'Male', '0300-4512345', 'House 12, Block D, Model Town, Lahore', 'Hypertension, Type-2 Diabetes', 'Concor 5mg OD, Glucophage 500mg BD', 500.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Fatima Bibi')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Fatima Bibi', 38, 'Female', '0311-2233445', 'House 5, Street 7, Iqbal Town, Lahore', 'Migraine, Gastritis', 'Brufen 400mg TDS, Risek 20mg OD', 500.00, 50.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Syed Waqar Hussain')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Syed Waqar Hussain', 65, 'Male', '0321-9988776', 'Flat 3B, Defence View Apartments, Karachi', 'Atrial Fibrillation, Hypercholesterolemia', 'Concor 5mg OD, Lipitor 20mg OD', 700.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Nazia Parveen')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Nazia Parveen', 29, 'Female', '0333-7654321', 'Street 14, G-9/4, Islamabad', 'Upper Respiratory Tract Infection', 'Amoxil 500mg TDS x 5 days, Panadol 500mg PRN', 400.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Tariq Mahmood')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Tariq Mahmood', 44, 'Male', '0345-1122334', 'House 88, Peoples Colony, Faisalabad', 'Peptic Ulcer Disease', 'Risek 20mg BD, Flagyl 400mg TDS x 7 days', 500.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Ayesha Siddiqui')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Ayesha Siddiqui', 6, 'Female', '0300-8877665', 'House 22, Gulshan-e-Iqbal, Karachi', 'Viral Fever', 'Calpol 120mg Syrup TDS, ORS PRN', 400.00, 100.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Ghulam Mustafa')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Ghulam Mustafa', 71, 'Male', '0312-5544332', 'Village Chak 22, Tehsil Gojra, Toba Tek Singh', 'Hypertension, Angina', 'Norvasc 5mg OD, Concor 5mg OD, Lipitor 20mg OD', 500.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Rubina Khatoon')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Rubina Khatoon', 33, 'Female', '0322-4433221', 'House 9, Street 3, Satellite Town, Rawalpindi', 'Seasonal Allergy, Sinusitis', 'Clarinase Repetabs OD x 10 days', 500.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Imran ul Haq')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Imran ul Haq', 48, 'Male', '0334-6655443', 'Flat 7, Askari 10, Lahore Cantt', 'Type-2 Diabetes, Hypertension', 'Glucophage 500mg BD, Norvasc 5mg OD', 600.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Saima Akhtar')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Saima Akhtar', 25, 'Female', '0301-3344556', 'House 44, Johar Town, Lahore', 'Acute Gastroenteritis', 'Flagyl 400mg TDS x 5 days, ORS x 3 days', 400.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Abdul Rauf Sheikh')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Abdul Rauf Sheikh', 57, 'Male', '0300-9988112', 'House 3, Block 13-D/1, Gulshan-e-Iqbal, Karachi', 'Chest Infection, Fever', 'Augmentin 625mg BD x 7 days, Panadol 500mg TDS', 600.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Hina Nawaz')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Hina Nawaz', 41, 'Female', '0313-2244668', 'House 67, F-10/3, Islamabad', 'Iron Deficiency Anemia', 'Ferrous Sulphate BD, Vitamin C OD', 500.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Khalid Mehmood Butt')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Khalid Mehmood Butt', 60, 'Male', '0320-7766554', 'House 19, Gulberg II, Lahore', 'Chronic Back Pain, Osteoarthritis', 'Brufen 400mg TDS, Panadol 500mg PRN', 500.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Zainab Farooq')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Zainab Farooq', 18, 'Female', '0346-8899001', 'House 11, Bahria Town, Lahore', 'Acute Pharyngitis (Strep Throat)', 'Amoxil 500mg TDS x 7 days, Panadol 500mg TDS', 400.00, 0.00, 'Completed', 1);

IF NOT EXISTS (SELECT 1 FROM Patients WHERE Name = 'Mian Shahzad Ahmad')
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, IsActive)
    VALUES ('Mian Shahzad Ahmad', 55, 'Male', '0302-5566778', 'House 5, DHA Phase 4, Lahore', 'Hyperlipidemia, Hypertension', 'Lipitor 20mg OD, Norvasc 5mg OD', 700.00, 0.00, 'Completed', 1);
GO

-- ============================================================
--  5. APPOINTMENTS (Today's Schedule)
-- ============================================================
PRINT 'Seeding Appointments...';

IF EXISTS (SELECT 1 FROM Users WHERE UserID = 1)
BEGIN
    DELETE FROM Appointments
    WHERE AppointmentNo IN ('APT-001', 'APT-002', 'APT-003', 'APT-004', 'APT-005');

    DECLARE @P1 INT = (SELECT TOP 1 PatientID FROM Patients WHERE Name = 'Muhammad Asif Khan');
    DECLARE @P2 INT = (SELECT TOP 1 PatientID FROM Patients WHERE Name = 'Fatima Bibi');
    DECLARE @P3 INT = (SELECT TOP 1 PatientID FROM Patients WHERE Name = 'Nazia Parveen');
    DECLARE @P4 INT = (SELECT TOP 1 PatientID FROM Patients WHERE Name = 'Tariq Mahmood');
    DECLARE @P5 INT = (SELECT TOP 1 PatientID FROM Patients WHERE Name = 'Ayesha Siddiqui');

    INSERT INTO Appointments (AppointmentNo, PatientID, PatientName, Phone, DoctorID, AppointmentDate, AppointmentTime, Reason, Status)
    VALUES
        ('APT-001', @P1, 'Muhammad Asif Khan', '0300-4512345', 1, CAST(GETDATE() AS DATE), '09:00', 'Follow-up: Blood pressure and diabetes review', 'Scheduled'),
        ('APT-002', @P2, 'Fatima Bibi',        '0311-2233445', 1, CAST(GETDATE() AS DATE), '09:30', 'Follow-up: Migraine and stomach pain', 'Scheduled'),
        ('APT-003', @P3, 'Nazia Parveen',      '0333-7654321', 1, CAST(GETDATE() AS DATE), '10:00', 'New visit: Sore throat and fever since 3 days', 'Scheduled'),
        ('APT-004', @P4, 'Tariq Mahmood',      '0345-1122334', 1, CAST(GETDATE() AS DATE), '10:30', 'Follow-up: Stomach ulcer', 'Scheduled'),
        ('APT-005', @P5, 'Ayesha Siddiqui',    '0300-8877665', 1, CAST(GETDATE() AS DATE), '11:00', 'New visit: Child fever and cold', 'Scheduled');
END
GO

PRINT '';
PRINT '============================================================';
PRINT '  Seed data inserted successfully.';
PRINT '  - Companies   : 7 (real pharma companies)';
PRINT '  - Suppliers   : 4 (real distributors)';
PRINT '  - Products    : 13 (common Pakistani medicines)';
PRINT '  - Patients    : 15 (real Pakistani names)';
PRINT '  - Appointments: 5 (scheduled for today)';
PRINT '============================================================';
GO
