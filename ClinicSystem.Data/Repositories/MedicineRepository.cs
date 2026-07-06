using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class MedicineRepository
{
    private readonly DatabaseSession _session;
    private bool _schemaEnsured;

    public MedicineRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Medicine> GetAll()
    {
        EnsurePharmacyColumns();
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>("SELECT * FROM Medicines ORDER BY Name");
    }

    public Medicine? GetById(int id)
    {
        EnsurePharmacyColumns();
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Medicine>(
            "SELECT * FROM Medicines WHERE MedicineID = @id", new { id });
    }

    public IEnumerable<Medicine> Search(string term)
    {
        EnsurePharmacyColumns();
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            @"SELECT * FROM Medicines
              WHERE Name LIKE @term
                 OR Manufacturer LIKE @term
                 OR Formula LIKE @term
                 OR Company LIKE @term
                 OR SupplierName LIKE @term
              ORDER BY Name",
            new { term = $"%{term}%" });
    }

    public IEnumerable<Medicine> GetExpired()
    {
        EnsurePharmacyColumns();
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            "SELECT * FROM Medicines WHERE ExpiryDate IS NOT NULL AND ExpiryDate <= CAST(GETDATE() AS DATE) ORDER BY ExpiryDate");
    }

    public IEnumerable<Medicine> GetLowStock()
    {
        EnsurePharmacyColumns();
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            "SELECT * FROM Medicines WHERE Stock <= MinStock ORDER BY Stock");
    }

    /// <summary>Returns medicines valid for prescribing (not expired, in stock).</summary>
    public IEnumerable<Medicine> GetPrescribable()
    {
        EnsurePharmacyColumns();
        using var conn = _session.CreateConnection();
        return conn.Query<Medicine>(
            @"SELECT * FROM Medicines
              WHERE Stock > 0
                AND (ExpiryDate IS NULL OR ExpiryDate > CAST(GETDATE() AS DATE))
              ORDER BY Name");
    }

    public int Insert(Medicine m)
    {
        EnsurePharmacyColumns();
        m.Price = m.SellingPrice;
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Medicines
                (Name, Formula, Stock, MinStock, ExpiryDate, Price, BuyingPrice, SellingPrice,
                 Manufacturer, Company, SupplierName, Category, BuyingDate, UnitsBought, StockType)
              VALUES
                (@Name, @Formula, @Stock, @MinStock, @ExpiryDate, @Price, @BuyingPrice, @SellingPrice,
                 @Manufacturer, @Company, @SupplierName, @Category, @BuyingDate, @UnitsBought, @StockType);
              SELECT SCOPE_IDENTITY();", m);
    }

    public void Update(Medicine m)
    {
        EnsurePharmacyColumns();
        m.UpdatedAt = DateTime.Now;
        m.Price = m.SellingPrice;
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Medicines SET
                Name = @Name, Formula = @Formula,
                Stock = @Stock, MinStock = @MinStock,
                ExpiryDate = @ExpiryDate, Price = @Price,
                BuyingPrice = @BuyingPrice, SellingPrice = @SellingPrice,
                Manufacturer = @Manufacturer, Company = @Company,
                SupplierName = @SupplierName, Category = @Category,
                BuyingDate = @BuyingDate, UnitsBought = @UnitsBought, StockType = @StockType,
                UpdatedAt = @UpdatedAt
              WHERE MedicineID = @MedicineID", m);
    }

    public bool Delete(int id)
    {
        EnsurePharmacyColumns();
        using var conn = _session.CreateConnection();
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM PrescriptionItems WHERE MedicineID = @id", new { id });
        if (count > 0) return false;
        conn.Execute("DELETE FROM Medicines WHERE MedicineID = @id", new { id });
        return true;
    }

    public void DecrementStock(int medicineId, int quantity)
    {
        EnsurePharmacyColumns();
        using var conn = _session.CreateConnection();
        conn.Execute(
            "UPDATE Medicines SET Stock = Stock - @quantity WHERE MedicineID = @medicineId",
            new { quantity, medicineId });
    }

    public void AddStock(int medicineId, int quantity, decimal buyingPrice, decimal sellingPrice,
        string? supplierName, DateTime? buyingDate, string stockType)
    {
        EnsurePharmacyColumns();
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Medicines SET
                Stock = Stock + @quantity,
                UnitsBought = UnitsBought + @quantity,
                BuyingPrice = @buyingPrice,
                SellingPrice = @sellingPrice,
                Price = @sellingPrice,
                SupplierName = @supplierName,
                BuyingDate = @buyingDate,
                StockType = @stockType,
                UpdatedAt = GETDATE()
              WHERE MedicineID = @medicineId",
            new { medicineId, quantity, buyingPrice, sellingPrice, supplierName, buyingDate, stockType });
    }

    private void EnsurePharmacyColumns()
    {
        if (_schemaEnsured) return;

        using var conn = _session.CreateConnection();
        AddColumnIfMissing(conn, "Formula", "VARCHAR(150) NULL");
        AddColumnIfMissing(conn, "BuyingPrice", "DECIMAL(10,2) NOT NULL CONSTRAINT DF_Medicines_BuyingPrice DEFAULT 0.00");
        AddColumnIfMissing(conn, "SellingPrice", "DECIMAL(10,2) NOT NULL CONSTRAINT DF_Medicines_SellingPrice DEFAULT 0.00");
        AddColumnIfMissing(conn, "Company", "VARCHAR(150) NULL");
        AddColumnIfMissing(conn, "SupplierName", "VARCHAR(150) NULL");
        AddColumnIfMissing(conn, "BuyingDate", "DATE NULL");
        AddColumnIfMissing(conn, "UnitsBought", "INT NOT NULL CONSTRAINT DF_Medicines_UnitsBought DEFAULT 0");
        AddColumnIfMissing(conn, "StockType", "VARCHAR(20) NOT NULL CONSTRAINT DF_Medicines_StockType DEFAULT 'Bought'");

        conn.Execute(
            @"UPDATE Medicines
              SET SellingPrice = CASE WHEN SellingPrice = 0 THEN Price ELSE SellingPrice END,
                  UnitsBought = CASE WHEN UnitsBought = 0 THEN Stock ELSE UnitsBought END
              WHERE SellingPrice = 0 OR UnitsBought = 0");
        _schemaEnsured = true;
    }

    private static void AddColumnIfMissing(System.Data.IDbConnection conn, string columnName, string definition)
    {
        conn.Execute(
            $@"IF COL_LENGTH('Medicines', '{columnName}') IS NULL
                   ALTER TABLE Medicines ADD {columnName} {definition};");
    }
}
