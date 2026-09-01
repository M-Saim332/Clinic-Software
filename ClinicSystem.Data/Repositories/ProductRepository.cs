using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class ProductRepository
{
    private readonly DatabaseSession _session;
    private const string ProductSelect = @"
        SELECT p.ProductID, p.PCode, p.Name, p.GenericName, p.Barcode, p.CompanyID,
               c.Name AS CompanyName, p.SupplierID, s.Name AS SupplierName,
               p.Type, p.Packing, p.Rate, p.PurchasePrice, p.SellingPrice,
               p.PiecesPerUnit, p.MinimumStockLevel, p.IsReturnable,
               p.IsActive, p.LastStockUpdateDate,
               (SELECT ISNULL(SUM(QuantityAvailable), 0) FROM ProductStock ps WHERE ps.ProductID = p.ProductID) AS TotalStock,
               (SELECT MIN(ExpiryDate) FROM ProductStock ps WHERE ps.ProductID = p.ProductID AND QuantityAvailable > 0) AS EarliestExpiry
        FROM Products p
        LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
        LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID";

    public ProductRepository(DatabaseSession session) => _session = session;

    private IEnumerable<Product> PopulateStock(IEnumerable<Product> products)
    {
        var list = products.ToList();
        if (list.Count == 0) return list;
        using var conn = _session.CreateConnection();
        var stocks = conn.Query<ProductStock>("SELECT * FROM ProductStock WHERE QuantityAvailable > 0");
        var lookup = stocks.GroupBy(s => s.ProductID).ToDictionary(g => g.Key, g => g.OrderBy(x => x.ExpiryDate).ToList());
        foreach (var p in list)
        {
            if (lookup.TryGetValue(p.ProductID, out var entries))
                p.StockEntries = entries;
        }
        return list;
    }

    public IEnumerable<Product> GetAll()
    {
        using var conn = _session.CreateConnection();
        return PopulateStock(conn.Query<Product>($"{ProductSelect} WHERE p.IsActive = 1 ORDER BY c.CCode, p.PCode, p.Name"));
    }

    /// <summary>Returns one row for each non-empty ProductStock expiry batch.</summary>
    public IEnumerable<ProductStockBatchDto> GetProductInventory()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<ProductStockBatchDto>(@"
            SELECT p.ProductID, ps.StockID, p.PCode,
                   p.Name AS ProductName, p.CompanyID, c.Name AS CompanyName,
                   p.Type, p.Packing, p.PiecesPerUnit,
                   ps.QuantityAvailable AS StockQuantity, ps.ExpiryDate,
                   ps.PurchasePrice AS RateTP, ps.MRP
            FROM ProductStock ps
            INNER JOIN Products p ON ps.ProductID = p.ProductID
            LEFT JOIN Companies c ON p.CompanyID = c.CompanyID
            WHERE p.IsActive = 1 AND ps.QuantityAvailable > 0
            ORDER BY p.Name ASC, ps.ExpiryDate ASC");
    }

    public int GetCount()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Products WHERE IsActive = 1");
    }

    public decimal GetTotalStockValue()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(@"
            SELECT ISNULL(SUM(ps.PurchasePrice * ps.QuantityAvailable), 0)
            FROM ProductStock ps
            JOIN Products p ON ps.ProductID = p.ProductID
            WHERE p.IsActive = 1 AND ps.QuantityAvailable > 0");
    }

    /// <summary>
    /// Gets the inventory counters from the same batch rows used for valuation.
    /// A low-stock threshold is stored in packs, so it is converted to pieces
    /// before it is compared with a batch's available quantity.
    /// </summary>
    public InventoryMetrics GetInventoryMetrics()
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingle<InventoryMetrics>(@"
            SELECT
                COUNT(CASE WHEN ps.QuantityAvailable > 0 THEN 1 END) AS TotalStockItems,
                COUNT(CASE WHEN ps.QuantityAvailable > 0
                                  AND ps.QuantityAvailable <= p.MinimumStockLevel *
                                      CASE WHEN ISNULL(p.PiecesPerUnit, 0) > 0 THEN p.PiecesPerUnit ELSE 1 END
                           THEN 1 END) AS LowStockItems,
                COUNT(CASE WHEN ps.QuantityAvailable = 0 THEN 1 END) AS OutOfStockItems,
                COUNT(CASE WHEN ps.ExpiryDate < CAST(GETDATE() AS DATE) THEN 1 END) AS ExpiredBatches
            FROM ProductStock ps
            INNER JOIN Products p ON p.ProductID = ps.ProductID
            WHERE p.IsActive = 1;");
    }

    public Product? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        var p = conn.QuerySingleOrDefault<Product>($"{ProductSelect} WHERE p.ProductID = @id AND p.IsActive = 1", new { id });
        if (p != null) PopulateStock(new[] { p });
        return p;
    }

    public IEnumerable<Product> GetByCompany(int companyId)
    {
        using var conn = _session.CreateConnection();
        return PopulateStock(conn.Query<Product>($"{ProductSelect} WHERE p.CompanyID = @companyId AND p.IsActive = 1 ORDER BY p.PCode, p.Name", new { companyId }));
    }

    public IEnumerable<Product> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return PopulateStock(conn.Query<Product>($@"{ProductSelect}
            WHERE p.IsActive = 1 AND (p.Name LIKE @like OR p.GenericName LIKE @like OR c.Name LIKE @like
              OR c.CCode = TRY_CONVERT(INT, @raw) OR p.PCode = TRY_CONVERT(INT, @raw) OR p.Barcode = @raw)
            ORDER BY c.CCode, p.PCode, p.Name", new { like = $"%{term}%", raw = term.Trim() }));
    }

    public IEnumerable<Product> GetExpired() => GetByExpiryWindow(expired: true, 0);

    public IEnumerable<Product> GetLowStock()
    {
        using var conn = _session.CreateConnection();
        return PopulateStock(conn.Query<Product>($"{ProductSelect} WHERE p.IsActive = 1 AND (SELECT ISNULL(SUM(QuantityAvailable), 0) FROM ProductStock ps WHERE ps.ProductID = p.ProductID) <= (p.MinimumStockLevel * p.PiecesPerUnit) ORDER BY p.Name"));
    }

    public IEnumerable<Product> GetExpiringSoon(int days) => GetByExpiryWindow(expired: false, days);

    private IEnumerable<Product> GetByExpiryWindow(bool expired, int days)
    {
        using var conn = _session.CreateConnection();
        var predicate = expired
            ? "ps.ExpiryDate < CAST(GETDATE() AS DATE)"
            : "ps.ExpiryDate >= CAST(GETDATE() AS DATE) AND ps.ExpiryDate <= DATEADD(day, @days, CAST(GETDATE() AS DATE))";
        return PopulateStock(conn.Query<Product>($@"{ProductSelect}
            WHERE p.IsActive=1 AND EXISTS (
                SELECT 1 FROM ProductStock ps
                WHERE ps.ProductID=p.ProductID AND ps.QuantityAvailable > 0 AND {predicate})
            ORDER BY p.Name", new { days }));
    }

    public IEnumerable<Product> GetPrescribable()
    {
        using var conn = _session.CreateConnection();
        return PopulateStock(conn.Query<Product>($"{ProductSelect} WHERE p.IsActive=1 AND (SELECT ISNULL(SUM(QuantityAvailable), 0) FROM ProductStock ps WHERE ps.ProductID = p.ProductID) > 0 ORDER BY p.Name"));
    }

    public int GetNextPCode(int companyId)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT ISNULL(MAX(PCode), 0) + 1 FROM Products");
    }

    public int GetNextPCode()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT ISNULL(MAX(PCode), 0) + 1 FROM Products");
    }

    public int Insert(Product product)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        
        // Prevent duplication of products with the same name under the same company
        var existingId = conn.ExecuteScalar<int?>(
            "SELECT TOP 1 ProductID FROM Products WITH (UPDLOCK,HOLDLOCK) WHERE Name = @Name AND CompanyID = @CompanyID AND IsActive = 1",
            new { product.Name, product.CompanyID }, transaction: tx);
            
        if (existingId.HasValue)
        {
            tx.Commit();
            throw new InvalidOperationException($"A product with the name '{product.Name}' already exists under the selected company.");
        }

        product.PCode = conn.ExecuteScalar<int>("SELECT ISNULL(MAX(PCode),0)+1 FROM Products WITH (UPDLOCK,HOLDLOCK)", transaction: tx);
        var id = conn.ExecuteScalar<int>(@"INSERT INTO Products
            (PCode,Name,GenericName,Barcode,CompanyID,CompanyName,SupplierID,SupplierName,Type,Packing,
             Rate,PurchasePrice,SellingPrice,PiecesPerUnit,MinimumStockLevel,IsReturnable,IsActive,LastStockUpdateDate)
            VALUES (@PCode,@Name,@GenericName,@Barcode,@CompanyID,@CompanyName,@SupplierID,@SupplierName,@Type,@Packing,
             @Rate,@PurchasePrice,@SellingPrice,@PiecesPerUnit,@MinimumStockLevel,@IsReturnable,1,@LastStockUpdateDate);
            SELECT CONVERT(INT,SCOPE_IDENTITY());", product, tx);
        tx.Commit();
        return id;
    }

    public void Update(Product product)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(@"UPDATE Products SET Name=@Name,GenericName=@GenericName,Barcode=@Barcode,CompanyID=@CompanyID,
            CompanyName=@CompanyName,SupplierID=@SupplierID,SupplierName=@SupplierName,Type=@Type,
            Packing=@Packing,Rate=@Rate,PurchasePrice=@PurchasePrice,SellingPrice=@SellingPrice,PiecesPerUnit=@PiecesPerUnit,
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
        // Decrement is now handled directly by SaleRepository (FEFO logic).
        // If this method is called, we should ideally use the FEFO logic.
        throw new NotSupportedException("Use SaleRepository FEFO deduction for DecrementStock");
    }

    public void AddStock(int productId, int quantity)
    {
        // Used only by quick adjustments? Add a dummy ProductStock entry if needed, but not standard.
        throw new NotSupportedException("Use PurchaseRepository for adding stock");
    }

    /// <summary>
    /// Insert or update a ProductStock row for the given (ProductID, ExpiryDate) pair.
    /// If a row already exists for that combination, the quantity is ADDED (not overwritten).
    /// This prevents duplicate rows and correctly stacks same-expiry stock.
    /// </summary>
    public void InsertStock(ProductStock stock)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var existingId = conn.ExecuteScalar<int?>(
            "SELECT StockID FROM ProductStock WITH (UPDLOCK,HOLDLOCK) WHERE ProductID=@ProductID AND ExpiryDate=CAST(@ExpiryDate AS DATE)",
            new { stock.ProductID, stock.ExpiryDate }, tx);
        if (existingId.HasValue)
        {
            conn.Execute(
                "UPDATE ProductStock SET QuantityAvailable = QuantityAvailable + @Qty, PurchasePrice = @PurchasePrice, MRP = @MRP WHERE StockID = @StockID",
                new { Qty = stock.QuantityAvailable, stock.PurchasePrice, stock.MRP, StockID = existingId.Value }, tx);
        }
        else
        {
            conn.Execute(@"
                INSERT INTO ProductStock (ProductID, ExpiryDate, QuantityAvailable, PurchasePrice, MRP)
                VALUES (@ProductID, CAST(@ExpiryDate AS DATE), @QuantityAvailable, @PurchasePrice, @MRP)", stock, tx);
        }
        tx.Commit();
    }

    /// <summary>
    /// Sets the quantity for one expiry batch. Stock is deliberately never stored on
    /// the Products row; totals are calculated from ProductStock instead.
    /// </summary>
    public void AdjustStock(int stockId, int newQuantity)
    {
        if (newQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(newQuantity), "Stock cannot be negative.");

        using var conn = _session.CreateConnection();
        var updated = conn.Execute(@"
            UPDATE ProductStock
            SET QuantityAvailable = @newQuantity,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE StockID = @stockId",
            new { stockId, newQuantity });

        if (updated != 1)
            throw new InvalidOperationException("The selected stock batch was not found.");
    }

    /// <summary>Returns the selectable, non-expired batches for one product.</summary>
    public IEnumerable<ProductStock> GetActiveStockBatches(int productId)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<ProductStock>(@"
            SELECT *
            FROM ProductStock
            WHERE ProductID = @productId
              AND QuantityAvailable > 0
              AND ExpiryDate >= CAST(GETDATE() AS DATE)
            ORDER BY ExpiryDate, StockID", new { productId });
    }

    /// <summary>
    /// Removes one batch from active inventory without archiving its master product.
    /// History is retained in the batch row for transaction traceability.
    /// </summary>
    public bool ArchiveStockBatch(int stockId)
    {
        using var conn = _session.CreateConnection();
        return conn.Execute(@"
            UPDATE ProductStock
            SET QuantityAvailable = 0,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE StockID = @stockId", new { stockId }) == 1;
    }

    /// <summary>
    /// Finds the batch used by the aggregate inventory adjustment screen. Prefer a
    /// non-expired batch and, when more than one is available, use the latest expiry.
    /// </summary>
    public ProductStock? GetLatestActiveStock(int productId)
    {
        using var conn = _session.CreateConnection();
        return conn.QueryFirstOrDefault<ProductStock>(@"
            SELECT TOP (1) *
            FROM ProductStock
            WHERE ProductID = @productId
              AND QuantityAvailable > 0
              AND ExpiryDate >= CAST(GETDATE() AS DATE)
            ORDER BY ExpiryDate DESC, StockID DESC", new { productId });
    }
}
