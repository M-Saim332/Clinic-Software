USE ClinicDB;
GO

-- Granular product stock and safe archival flags.
IF COL_LENGTH('Products', 'TabletsPerBox') IS NULL ALTER TABLE Products ADD TabletsPerBox INT NOT NULL CONSTRAINT DF_Products_TabletsPerBox DEFAULT 1;
IF COL_LENGTH('Products', 'IsReturnable') IS NULL ALTER TABLE Products ADD IsReturnable BIT NOT NULL CONSTRAINT DF_Products_IsReturnable DEFAULT 1;
IF COL_LENGTH('Products', 'IsActive') IS NULL ALTER TABLE Products ADD IsActive BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1;
IF COL_LENGTH('Patients', 'IsActive') IS NULL ALTER TABLE Patients ADD IsActive BIT NOT NULL CONSTRAINT DF_Patients_IsActive DEFAULT 1;
GO

-- Transaction ownership and safe archival.
IF COL_LENGTH('Sales', 'ReceptionistId') IS NULL ALTER TABLE Sales ADD ReceptionistId INT NULL;
IF COL_LENGTH('Sales', 'ReceptionistName') IS NULL ALTER TABLE Sales ADD ReceptionistName NVARCHAR(150) NULL;
IF COL_LENGTH('Sales', 'IsActive') IS NULL ALTER TABLE Sales ADD IsActive BIT NOT NULL CONSTRAINT DF_Sales_IsActive DEFAULT 1;
IF COL_LENGTH('Purchases', 'CreatedBy') IS NULL ALTER TABLE Purchases ADD CreatedBy INT NULL;
IF COL_LENGTH('Purchases', 'CreatedByName') IS NULL ALTER TABLE Purchases ADD CreatedByName NVARCHAR(150) NULL;
GO

-- Package-aware purchases. Quantity remains populated for compatibility and stores total units.
IF COL_LENGTH('PurchaseItems', 'BonusQuantity') IS NULL ALTER TABLE PurchaseItems ADD BonusQuantity INT NOT NULL CONSTRAINT DF_PurchaseItems_BonusQuantity DEFAULT 0;
IF COL_LENGTH('PurchaseItems', 'PackageType') IS NULL ALTER TABLE PurchaseItems ADD PackageType NVARCHAR(30) NOT NULL CONSTRAINT DF_PurchaseItems_PackageType DEFAULT 'Box';
IF COL_LENGTH('PurchaseItems', 'PackageQuantity') IS NULL ALTER TABLE PurchaseItems ADD PackageQuantity INT NOT NULL CONSTRAINT DF_PurchaseItems_PackageQuantity DEFAULT 0;
IF COL_LENGTH('PurchaseItems', 'UnitsPerPackage') IS NULL ALTER TABLE PurchaseItems ADD UnitsPerPackage INT NOT NULL CONSTRAINT DF_PurchaseItems_UnitsPerPackage DEFAULT 1;
GO
UPDATE PurchaseItems SET PackageQuantity = Quantity WHERE PackageQuantity = 0;

-- Sale quantities are customer-facing quantities; StockQuantity is always individual stock units.
IF COL_LENGTH('SaleItems', 'UnitTypeSold') IS NULL ALTER TABLE SaleItems ADD UnitTypeSold NVARCHAR(20) NOT NULL CONSTRAINT DF_SaleItems_UnitTypeSold DEFAULT 'Tablet';
IF COL_LENGTH('SaleItems', 'StockQuantity') IS NULL ALTER TABLE SaleItems ADD StockQuantity INT NOT NULL CONSTRAINT DF_SaleItems_StockQuantity DEFAULT 0;
IF COL_LENGTH('SaleItems', 'UnitPrice') IS NULL ALTER TABLE SaleItems ADD UnitPrice DECIMAL(10,2) NOT NULL CONSTRAINT DF_SaleItems_UnitPrice DEFAULT 0;
GO
UPDATE SaleItems SET StockQuantity = Quantity WHERE StockQuantity = 0;
UPDATE si SET UnitPrice = CASE WHEN si.Quantity > 0 THEN si.LineTotal / si.Quantity ELSE 0 END FROM SaleItems si WHERE UnitPrice = 0;

-- One auditable table handles both patient and supplier returns.
IF COL_LENGTH('Returns', 'UnitType') IS NULL ALTER TABLE Returns ADD UnitType NVARCHAR(20) NOT NULL CONSTRAINT DF_Returns_UnitType DEFAULT 'Tablet';
IF COL_LENGTH('Returns', 'StockQuantity') IS NULL ALTER TABLE Returns ADD StockQuantity INT NOT NULL CONSTRAINT DF_Returns_StockQuantity DEFAULT 0;
GO
UPDATE Returns SET StockQuantity = Quantity WHERE StockQuantity = 0;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'ProductId', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'CreatedBy', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_User FOREIGN KEY (CreatedBy) REFERENCES Users(UserID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'PatientId', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_Patient FOREIGN KEY (PatientId) REFERENCES Patients(PatientID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'SupplierId', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_Supplier FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE parent_object_id = OBJECT_ID('Returns') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('Returns'), 'SaleId', 'ColumnId'))
    ALTER TABLE Returns ADD CONSTRAINT FK_Returns_Sale FOREIGN KEY (SaleId) REFERENCES Sales(SaleID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sales_Date_Receptionist')
    CREATE INDEX IX_Sales_Date_Receptionist ON Sales(SaleDate, ReceptionistId) INCLUDE (GrandTotal, IsPosted, IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Returns_Date_Type')
    CREATE INDEX IX_Returns_Date_Type ON Returns(CreatedAt, ReturnType) INCLUDE (RefundAmount, StockQuantity);
GO
