-- ============================================================
--  Clinic Management System -- Abundant Test Data Generation
--  Run this script to populate the database with a large
--  amount of sample data for testing features.
-- ============================================================

USE ClinicDB;
GO

SET NOCOUNT ON;

PRINT 'Starting data generation...';

-- 1. Add Companies (10 Companies)
PRINT 'Generating Companies...';
DECLARE @CompanyCount INT = 1;
WHILE @CompanyCount <= 10
BEGIN
    INSERT INTO Companies (Name, Address, Phone, Email)
    VALUES (
        'Company ' + CAST(@CompanyCount AS VARCHAR),
        'Address ' + CAST(@CompanyCount AS VARCHAR),
        '555-' + RIGHT('0000' + CAST(@CompanyCount AS VARCHAR), 4),
        'contact' + CAST(@CompanyCount AS VARCHAR) + '@company.com'
    );
    SET @CompanyCount = @CompanyCount + 1;
END

-- 2. Add Suppliers (15 Suppliers)
PRINT 'Generating Suppliers...';
DECLARE @SupplierCount INT = 1;
WHILE @SupplierCount <= 15
BEGIN
    INSERT INTO Suppliers (Name, Address, Phone, Email, CNIC)
    VALUES (
        'Supplier ' + CAST(@SupplierCount AS VARCHAR),
        'Supplier Address ' + CAST(@SupplierCount AS VARCHAR),
        '555-S' + RIGHT('000' + CAST(@SupplierCount AS VARCHAR), 3),
        'info' + CAST(@SupplierCount AS VARCHAR) + '@supplier.com',
        '12345-1234567-' + CAST(@SupplierCount % 10 AS VARCHAR)
    );
    SET @SupplierCount = @SupplierCount + 1;
END

-- 3. Add Products (100 Products)
PRINT 'Generating Products...';
DECLARE @ProductCount INT = 1;
DECLARE @CompID INT, @SuppID INT;

WHILE @ProductCount <= 100
BEGIN
    SET @CompID = (SELECT TOP 1 CompanyID FROM Companies ORDER BY NEWID());
    SET @SuppID = (SELECT TOP 1 SupplierID FROM Suppliers ORDER BY NEWID());

    INSERT INTO Products (Name, GenericName, CompanyID, CompanyName, SupplierID, SupplierName, BatchNumber, Type, Category, Rack, ExpiryDate, PurchasePrice, SellingPrice, TabletsPerBox, Stock, MinimumStockLevel, IsReturnable, IsActive)
    VALUES (
        'Product ' + CAST(@ProductCount AS VARCHAR),
        'Generic ' + CAST(@ProductCount AS VARCHAR),
        @CompID,
        (SELECT Name FROM Companies WHERE CompanyID = @CompID),
        @SuppID,
        (SELECT Name FROM Suppliers WHERE SupplierID = @SuppID),
        'B' + CAST((1000 + @ProductCount) AS VARCHAR),
        CHOOSE((@ProductCount % 4) + 1, 'Tablet', 'Syrup', 'Injection', 'Capsule'),
        CHOOSE((@ProductCount % 3) + 1, 'Painkiller', 'Antibiotic', 'Vitamin'),
        'Rack ' + CAST((@ProductCount % 5) + 1 AS VARCHAR),
        DATEADD(day, (RAND() * 1000) + 30, GETDATE()), -- Expiry in future
        (RAND() * 50) + 10,
        (RAND() * 50) + 60,
        CHOOSE((@ProductCount % 2) + 1, 10, 1),
        (RAND() * 500) + 50,
        20,
        1,
        1
    );
    SET @ProductCount = @ProductCount + 1;
END

-- 4. Add Patients (100 Patients)
PRINT 'Generating Patients...';
DECLARE @PatientCount INT = 1;
WHILE @PatientCount <= 100
BEGIN
    INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, IsActive, VisitStatus)
    VALUES (
        'Patient ' + CAST(@PatientCount AS VARCHAR),
        (RAND() * 60) + 10,
        CHOOSE((@PatientCount % 2) + 1, 'Male', 'Female'),
        '555-P' + RIGHT('000' + CAST(@PatientCount AS VARCHAR), 3),
        'Patient Address ' + CAST(@PatientCount AS VARCHAR),
        'Diagnosis ' + CAST(@PatientCount AS VARCHAR),
        'Prescription ' + CAST(@PatientCount AS VARCHAR),
        (RAND() * 100) + 50,
        (RAND() * 10),
        1,
        'Completed'
    );
    SET @PatientCount = @PatientCount + 1;
END

-- 5. Add Users (Doctors, Receptionists) (10 Users)
PRINT 'Generating Additional Users...';
DECLARE @UserCount INT = 1;
WHILE @UserCount <= 10
BEGIN
    INSERT INTO Users (Username, PasswordHash, Role, FullName, IsActive, CreatedAt)
    VALUES (
        'user' + CAST(@UserCount AS VARCHAR),
        '$2a$11$u0LyGgHmhN2kTeoBK.a5m.FVHXHSUA/xHZFJ9tE1O4Oj4QvICWT.O', -- Admin@123
        CHOOSE((@UserCount % 3) + 1, 'Doctor', 'Receptionist', 'Pharmacist'),
        'User FullName ' + CAST(@UserCount AS VARCHAR),
        1,
        GETDATE()
    );
    SET @UserCount = @UserCount + 1;
END

-- 6. Add Appointments (200 Appointments spanning past and future)
PRINT 'Generating Appointments...';
DECLARE @ApptCount INT = 1;
DECLARE @PatID INT, @DocID INT;
DECLARE @ApptDate DATE;

WHILE @ApptCount <= 200
BEGIN
    SET @PatID = (SELECT TOP 1 PatientID FROM Patients ORDER BY NEWID());
    SET @DocID = (SELECT TOP 1 UserID FROM Users WHERE Role = 'Doctor' ORDER BY NEWID());
    IF @DocID IS NULL SET @DocID = 1; -- Fallback to Admin if no doctor

    SET @ApptDate = DATEADD(day, (RAND() * 60) - 30, GETDATE()); -- Past 30 days to future 30 days

    INSERT INTO Appointments (AppointmentNo, PatientID, PatientName, Phone, DoctorID, AppointmentDate, AppointmentTime, Reason, Status, CreatedAt)
    VALUES (
        'APT-' + CAST(@ApptCount AS VARCHAR),
        @PatID,
        (SELECT Name FROM Patients WHERE PatientID = @PatID),
        (SELECT Phone FROM Patients WHERE PatientID = @PatID),
        @DocID,
        @ApptDate,
        TIMEFROMPARTS((RAND() * 8) + 9, (RAND() * 4) * 15, 0, 0, 0), -- 9 AM to 5 PM
        'Reason for appointment ' + CAST(@ApptCount AS VARCHAR),
        CHOOSE((@ApptCount % 4) + 1, 'Scheduled', 'Completed', 'Cancelled', 'No-Show'),
        GETDATE()
    );
    SET @ApptCount = @ApptCount + 1;
END

-- 7. Add Purchases (50 Purchases)
PRINT 'Generating Purchases...';
DECLARE @PurchCount INT = 1;
DECLARE @TotalPurchAmount DECIMAL(12,2);
DECLARE @PurchID INT;

WHILE @PurchCount <= 50
BEGIN
    SET @SuppID = (SELECT TOP 1 SupplierID FROM Suppliers ORDER BY NEWID());
    
    INSERT INTO Purchases (InvoiceNumber, PurchaseDate, SupplierID, SupplierName, TotalAmount)
    VALUES (
        'PUR-INV-' + CAST(@PurchCount AS VARCHAR),
        DATEADD(day, -(RAND() * 60), GETDATE()), -- Past 60 days
        @SuppID,
        (SELECT Name FROM Suppliers WHERE SupplierID = @SuppID),
        0 -- Will update later
    );
    
    SET @PurchID = SCOPE_IDENTITY();
    SET @TotalPurchAmount = 0;

    -- Add 1 to 5 Items per Purchase
    DECLARE @ItemCount INT = (RAND() * 5) + 1;
    DECLARE @I INT = 1;
    WHILE @I <= @ItemCount
    BEGIN
        DECLARE @ProdID INT = (SELECT TOP 1 ProductID FROM Products ORDER BY NEWID());
        DECLARE @Qty INT = (RAND() * 50) + 10;
        DECLARE @PPrice DECIMAL(10,2) = (SELECT PurchasePrice FROM Products WHERE ProductID = @ProdID);

        INSERT INTO PurchaseItems (PurchaseID, ProductID, BatchNumber, ExpiryDate, Quantity, PackageType, PackageQuantity, UnitsPerPackage, PurchasePrice)
        VALUES (
            @PurchID,
            @ProdID,
            'B' + CAST(CAST((RAND() * 1000) AS INT) AS VARCHAR),
            DATEADD(day, (RAND() * 1000) + 30, GETDATE()),
            @Qty,
            'Box',
            @Qty / 10 + 1,
            10,
            @PPrice
        );
        SET @TotalPurchAmount = @TotalPurchAmount + (@Qty * @PPrice);
        SET @I = @I + 1;
    END

    UPDATE Purchases SET TotalAmount = @TotalPurchAmount WHERE PurchaseID = @PurchID;
    
    SET @PurchCount = @PurchCount + 1;
END

-- 8. Add Sales (100 Sales)
PRINT 'Generating Sales...';
DECLARE @SaleCount INT = 1;
DECLARE @TotalSaleAmount DECIMAL(12,2);
DECLARE @SaleID INT;

WHILE @SaleCount <= 100
BEGIN
    SET @PatID = (SELECT TOP 1 PatientID FROM Patients ORDER BY NEWID());
    
    INSERT INTO Sales (InvoiceNumber, SaleDate, PatientID, PatientName, ConsultationFee, GrandTotal, PaymentMethod, IsPosted)
    VALUES (
        'SALE-INV-' + CAST(@SaleCount AS VARCHAR),
        DATEADD(day, -(RAND() * 60), GETDATE()), -- Past 60 days
        @PatID,
        (SELECT Name FROM Patients WHERE PatientID = @PatID),
        (RAND() * 100) + 50,
        0, -- Will update later
        CHOOSE((@SaleCount % 3) + 1, 'Cash', 'Card', 'Online'),
        1
    );
    
    SET @SaleID = SCOPE_IDENTITY();
    SET @TotalSaleAmount = (SELECT ConsultationFee FROM Sales WHERE SaleID = @SaleID);

    -- Add 1 to 5 Items per Sale
    DECLARE @SItemCount INT = (RAND() * 5) + 1;
    DECLARE @J INT = 1;
    WHILE @J <= @SItemCount
    BEGIN
        DECLARE @SProdID INT = (SELECT TOP 1 ProductID FROM Products ORDER BY NEWID());
        DECLARE @SQty INT = (RAND() * 5) + 1;
        DECLARE @SPrice DECIMAL(10,2) = (SELECT SellingPrice FROM Products WHERE ProductID = @SProdID);
        DECLARE @LTotal DECIMAL(10,2) = @SQty * @SPrice;

        INSERT INTO SaleItems (SaleID, ProductID, Quantity, UnitTypeSold, StockQuantity, UnitPrice, LineTotal)
        VALUES (
            @SaleID,
            @SProdID,
            @SQty,
            'Tablet',
            @SQty,
            @SPrice,
            @LTotal
        );
        SET @TotalSaleAmount = @TotalSaleAmount + @LTotal;
        SET @J = @J + 1;
    END

    UPDATE Sales SET GrandTotal = @TotalSaleAmount WHERE SaleID = @SaleID;
    
    SET @SaleCount = @SaleCount + 1;
END

PRINT 'Data generation complete! Abundant records added.';
GO
