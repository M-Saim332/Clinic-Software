-- ============================================================
--  Clinic Management System -- Temporary Test Data
--  Run this script to populate the database with sample data
-- ============================================================

USE ClinicDB;
GO

-- 1. Add Companies
INSERT INTO Companies (Name, Address, Phone, Email)
VALUES 
    ('PharmaCorp', '123 Health St', '555-0101', 'contact@pharmacorp.com'),
    ('MediLife', '456 Wellness Blvd', '555-0102', 'info@medilife.com');

-- 2. Add Suppliers
INSERT INTO Suppliers (Name, Address, Phone, Email)
VALUES 
    ('Global Meds', '789 Supply Chain Rd', '555-0201', 'sales@globalmeds.com'),
    ('Local Med Supply', '321 Local Ave', '555-0202', 'orders@localmedsupply.com');

-- 3. Add Products (associated with Companies)
INSERT INTO Products (CompanyID, Name, PurchaseRate, SellingPrice, Tax, StockQuantity)
VALUES 
    (1, 'Surgical Masks (Box of 50)', 5.00, 10.00, 0.50, 100),
    (2, 'Syringes (Pack of 100)', 15.00, 25.00, 1.25, 50),
    (1, 'First Aid Kit', 20.00, 35.00, 1.75, 20);

-- 4. Add Medicines (associated with Companies)
INSERT INTO Medicines (Name, GenericName, CompanyID, BatchNumber, ExpiryDate, PurchasePrice, SellingPrice, Stock, MinimumStockLevel)
VALUES 
    ('Paracetamol 500mg', 'Paracetamol', 1, 'B001', '2027-12-31', 1.00, 2.50, 200, 50),
    ('Amoxicillin 250mg', 'Amoxicillin', 2, 'B002', '2026-10-31', 3.00, 8.00, 150, 30),
    ('Ibuprofen 400mg', 'Ibuprofen', 1, 'B003', '2028-01-15', 2.00, 5.00, 100, 20),
    ('Lisinopril 10mg', 'Lisinopril', 2, 'B004', '2025-05-31', 4.00, 12.00, 80, 15);

-- 5. Add Patients
INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount)
VALUES 
    ('John Doe', 45, 'Male', '555-1001', '12 Main St', 'Hypertension', 'Diet change, Lisinopril', 50.00, 0),
    ('Jane Smith', 32, 'Female', '555-1002', '34 Elm St', 'Migraine', 'Rest and hydration, Ibuprofen', 50.00, 5.00),
    ('Alice Johnson', 28, 'Female', '555-1003', '56 Oak St', 'Common Cold', 'Paracetamol', 40.00, 0);

-- 6. Add Appointments (Assuming Admin User ID 1 exists)
IF EXISTS (SELECT 1 FROM Users WHERE UserID = 1)
BEGIN
    INSERT INTO Appointments (PatientID, DoctorID, AppointmentDate, AppointmentTime, Reason, Status)
    VALUES 
        (1, 1, CAST(GETDATE() AS DATE), '10:00', 'Routine checkup', 'Scheduled'),
        (2, 1, CAST(GETDATE() AS DATE), '11:00', 'Severe headache', 'Checked-In'),
        (3, 1, CAST(DATEADD(day, 1, GETDATE()) AS DATE), '09:00', 'Fever and cough', 'Scheduled');
END

-- 7. Add Purchases (from Suppliers)
INSERT INTO Purchases (InvoiceNumber, PurchaseDate, SupplierID, TotalAmount)
VALUES 
    ('INV-1001', GETDATE(), 1, 100.00),
    ('INV-1002', DATEADD(day, -2, GETDATE()), 2, 250.00);

-- 8. Add Purchase Items
INSERT INTO PurchaseItems (PurchaseID, ProductID, BatchNumber, ExpiryDate, Quantity, PurchasePrice, Discount, Tax)
VALUES 
    ((SELECT TOP 1 PurchaseID FROM Purchases WHERE InvoiceNumber = 'INV-1001'), 1, 'M001', '2030-01-01', 10, 5.00, 0, 0.50),
    ((SELECT TOP 1 PurchaseID FROM Purchases WHERE InvoiceNumber = 'INV-1002'), 2, 'S001', '2028-06-01', 5, 15.00, 0, 1.25);

-- 9. Add Sales
INSERT INTO Sales (InvoiceNumber, SaleDate, PatientID, ConsultationFee, GrandTotal, PaymentMethod, IsPosted)
VALUES 
    ('SALE-001', GETDATE(), 1, 50.00, 55.00, 'Cash', 1),
    ('SALE-002', DATEADD(day, -1, GETDATE()), 2, 45.00, 53.00, 'Card', 1);

-- 10. Add Sale Items
INSERT INTO SaleItems (SaleID, MedicineID, Quantity, Discount, Tax, LineTotal)
VALUES 
    ((SELECT TOP 1 SaleID FROM Sales WHERE InvoiceNumber = 'SALE-001'), 1, 2, 0, 0, 5.00),
    ((SELECT TOP 1 SaleID FROM Sales WHERE InvoiceNumber = 'SALE-002'), 3, 1, 0, 0, 8.00);

PRINT 'Test data inserted successfully.';
GO
