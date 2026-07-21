USE ClinicDB;
GO

DECLARE @StartDate DATETIME = DATEADD(DAY, -29, GETDATE());
DECLARE @EndDate DATETIME = GETDATE();
DECLARE @CurrentDate DATETIME = @StartDate;

DECLARE @MinPatientID INT = (SELECT MIN(PatientID) FROM Patients);
DECLARE @MinSupplierID INT = (SELECT MIN(SupplierID) FROM Suppliers);

IF @MinPatientID IS NULL OR @MinSupplierID IS NULL
BEGIN
    PRINT 'No patients or suppliers found!';
    RETURN;
END

WHILE @CurrentDate <= @EndDate
BEGIN
    -- Insert a Purchase for each day
    INSERT INTO Purchases (InvoiceNumber, PurchaseDate, SupplierID, TotalAmount)
    VALUES ('PUR-' + FORMAT(@CurrentDate, 'yyyyMMdd'), @CurrentDate, @MinSupplierID, RAND() * 1000 + 500);

    -- Insert a Sale for each day
    INSERT INTO Sales (InvoiceNumber, SaleDate, PatientID, ConsultationFee, GrandTotal, PaymentMethod, IsPosted)
    VALUES ('INV-' + FORMAT(@CurrentDate, 'yyyyMMdd'), @CurrentDate, @MinPatientID, 500, RAND() * 2000 + 1000, 'Cash', 1);

    -- Advance date
    SET @CurrentDate = DATEADD(DAY, 1, @CurrentDate);
END

PRINT 'Successfully generated 30 days of Sales and Purchases.';
GO
