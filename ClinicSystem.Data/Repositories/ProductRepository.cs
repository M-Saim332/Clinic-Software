using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class ProductRepository
{
    private readonly DatabaseSession _session;
    private const string ProductSelect = @"
        SELECT p.ProductID, p.PCode, p.Name, p.GenericName, p.Barcode, p.CompanyID,
               c.Name AS CompanyName, p.SupplierID, s.Name AS SupplierName,
               p.BatchNumber, p.Type, p.Packing, p.PurchasePrice, p.SellingPrice,
               p.PiecesPerUnit, p.Stock, p.MinimumStockLevel, p.IsReturnable,
               p.IsActive, p.LastStockUpdateDate,
               (SELECT MIN(pi.ExpiryDate) FROM PurchaseItems pi JOIN Purchases pu ON pu.PurchaseID=pi.PurchaseID
                WHERE pi.ProductID=p.ProductID AND pu.IsPosted=1 AND pi.ExpiryDate>=CAST(GETDATE() AS DATE)) AS ExpiryDate
        FROM Products p
        LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
        LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID";

    public ProductRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Product> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>($"{ProductSelect} WHERE p.IsActive = 1 ORDER BY c.CCode, p.PCode, p.Name");
    }

    public int GetCount()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Products WHERE IsActive = 1");
    }

    public decimal GetTotalStockValue()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>("SELECT ISNULL(SUM((SellingPrice / NULLIF(PiecesPerUnit, 0)) * Stock), 0) FROM Products WHERE IsActive = 1");
    }

    public Product? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Product>($"{ProductSelect} WHERE p.ProductID = @id AND p.IsActive = 1", new { id });
    }

    public IEnumerable<Product> GetByCompany(int companyId)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>($"{ProductSelect} WHERE p.CompanyID = @companyId AND p.IsActive = 1 ORDER BY p.PCode, p.Name", new { companyId });
    }

    public IEnumerable<Product> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>($@"{ProductSelect}
            WHERE p.IsActive = 1 AND (p.Name LIKE @like OR p.GenericName LIKE @like OR c.Name LIKE @like
              OR c.CCode = TRY_CONVERT(INT, @raw) OR p.PCode = TRY_CONVERT(INT, @raw) OR p.Barcode = @raw)
            ORDER BY c.CCode, p.PCode, p.Name", new { like = $"%{term}%", raw = term.Trim() });
    }

    public IEnumerable<Product> GetExpired() => GetByExpiryWindow(expired: true, 0);

    public IEnumerable<Product> GetLowStock()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>($"{ProductSelect} WHERE p.IsActive = 1 AND p.Stock <= p.MinimumStockLevel ORDER BY p.Stock");
    }

    public IEnumerable<Product> GetExpiringSoon(int days) => GetByExpiryWindow(expired: false, days);

    private IEnumerable<Product> GetByExpiryWindow(bool expired, int days)
    {
        using var conn = _session.CreateConnection();
        var predicate = expired
            ? "pi.ExpiryDate < CAST(GETDATE() AS DATE)"
            : "pi.ExpiryDate >= CAST(GETDATE() AS DATE) AND pi.ExpiryDate <= DATEADD(day, @days, CAST(GETDATE() AS DATE))";
        return conn.Query<Product>($@"{ProductSelect}
            WHERE p.IsActive=1 AND EXISTS (
                SELECT 1 FROM PurchaseItems pi JOIN Purchases pu ON pu.PurchaseID=pi.PurchaseID
                WHERE pi.ProductID=p.ProductID AND pu.IsPosted=1 AND {predicate})
            ORDER BY p.Name", new { days });
    }

    public IEnumerable<Product> GetPrescribable()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Product>($"{ProductSelect} WHERE p.IsActive=1 AND p.Stock>0 ORDER BY p.Name");
    }

    public int GetNextPCode(int companyId)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT ISNULL(MAX(PCode), 0) + 1 FROM Products WHERE CompanyID=@companyId", new { companyId });
    }

    public int Insert(Product product)
    {
        if (!product.CompanyID.HasValue) throw new InvalidOperationException("A company must be selected before adding a product.");
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        product.PCode = conn.ExecuteScalar<int>("SELECT ISNULL(MAX(PCode),0)+1 FROM Products WITH (UPDLOCK,HOLDLOCK) WHERE CompanyID=@CompanyID", product, tx);
        product.Stock = 0;
        var id = conn.ExecuteScalar<int>(@"INSERT INTO Products
            (PCode,Name,GenericName,Barcode,CompanyID,CompanyName,SupplierID,SupplierName,BatchNumber,Type,Packing,
             PurchasePrice,SellingPrice,PiecesPerUnit,Stock,MinimumStockLevel,IsReturnable,IsActive,LastStockUpdateDate)
            VALUES (@PCode,@Name,@GenericName,@Barcode,@CompanyID,@CompanyName,@SupplierID,@SupplierName,@BatchNumber,@Type,@Packing,
             @PurchasePrice,@SellingPrice,@PiecesPerUnit,@Stock,@MinimumStockLevel,@IsReturnable,1,@LastStockUpdateDate);
            SELECT CONVERT(INT,SCOPE_IDENTITY());", product, tx);
        tx.Commit();
        return id;
    }

    public void Update(Product product)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(@"UPDATE Products SET Name=@Name,GenericName=@GenericName,Barcode=@Barcode,CompanyID=@CompanyID,
            CompanyName=@CompanyName,SupplierID=@SupplierID,SupplierName=@SupplierName,BatchNumber=@BatchNumber,Type=@Type,
            Packing=@Packing,PurchasePrice=@PurchasePrice,SellingPrice=@SellingPrice,PiecesPerUnit=@PiecesPerUnit,
            IsReturnable=@IsReturnable,MinimumStockLevel=@MinimumStockLevel,LastStockUpdateDate=@LastStockUpdateDate
            WHERE ProductID=@ProductID AND IsActive=1", product);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.Execute("UPDATE Products SET IsActive=0 WHERE ProductID=@id AND IsActive=1", new { id }) == 1;
    }

    public int SoftDeleteAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Execute("UPDATE Products SET IsActive=0 WHERE IsActive=1");
    }

    public void DecrementStock(int productId, int quantity)
    {
        using var conn = _session.CreateConnection();
        var updated = conn.Execute(@"UPDATE Products SET Stock=Stock-@quantity,LastStockUpdateDate=CAST(GETDATE() AS DATE)
            WHERE ProductID=@productId AND Stock>=@quantity AND IsActive=1", new { quantity, productId });
        if (updated != 1) throw new InvalidOperationException("Insufficient stock.");
    }

    public void AddStock(int productId, int quantity)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(@"UPDATE Products SET Stock=Stock+@quantity,LastStockUpdateDate=CAST(GETDATE() AS DATE)
            WHERE ProductID=@productId AND IsActive=1", new { quantity, productId });
    }

    public void AdjustStock(int productId, int quantity, DateTime updateDate)
    {
        using var conn=_session.CreateConnection();
        var updated=conn.Execute(@"UPDATE Products SET Stock=Stock+@quantity,LastStockUpdateDate=@updateDate
            WHERE ProductID=@productId AND IsActive=1 AND Stock+@quantity>=0",new{productId,quantity,updateDate=updateDate.Date});
        if(updated!=1) throw new InvalidOperationException("The stock update would create a negative balance.");
    }
}
