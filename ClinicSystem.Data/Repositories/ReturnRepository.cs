using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class ReturnRepository
{
    private readonly DatabaseSession _session;
    public ReturnRepository(DatabaseSession session) => _session = session;

    public IEnumerable<ProductReturn> GetAll()
    {
        using var conn = _session.CreateConnection();
        var returns = conn.Query<ProductReturn>(@"SELECT r.*,u.FullName CreatedByName,pat.Name PatientName,sup.Name SupplierName
            FROM Returns r LEFT JOIN Users u ON r.CreatedBy=u.UserID LEFT JOIN Patients pat ON r.PatientId=pat.PatientID
            LEFT JOIN Suppliers sup ON r.SupplierId=sup.SupplierID WHERE r.IsPosted = 1 ORDER BY r.CreatedAt DESC").ToList();
        LoadItems(conn, returns);
        return returns;
    }

    public IEnumerable<ProductReturn> GetByRange(DateTime fromInclusive, DateTime toExclusive, string? processedByName = null)
    {
        using var conn = _session.CreateConnection();
        var returns = conn.Query<ProductReturn>(@"SELECT r.*,u.FullName CreatedByName,pat.Name PatientName,sup.Name SupplierName
            FROM Returns r LEFT JOIN Users u ON r.CreatedBy=u.UserID LEFT JOIN Patients pat ON r.PatientId=pat.PatientID
            LEFT JOIN Suppliers sup ON r.SupplierId=sup.SupplierID WHERE r.CreatedAt>=@fromInclusive AND r.CreatedAt<@toExclusive
            AND (@processedByName IS NULL OR u.FullName=@processedByName) ORDER BY r.CreatedAt DESC",
            new { fromInclusive, toExclusive, processedByName }).ToList();
        LoadItems(conn, returns);
        return returns;
    }

    private static void LoadItems(System.Data.IDbConnection conn, List<ProductReturn> returns)
    {
        if (returns.Count == 0) return;
        var ids = returns.Select(r => r.ReturnId).ToArray();
        var items = conn.Query<ReturnItem>(@"SELECT ri.*,p.Name ProductName,p.Type ProductType FROM ReturnItems ri
            JOIN Products p ON ri.ProductId=p.ProductID WHERE ri.ReturnId IN @ids", new { ids }).ToList();
        foreach (var ret in returns)
        {
            ret.Items = items.Where(i => i.ReturnId == ret.ReturnId).ToList();
            var first = ret.Items.FirstOrDefault();
            ret.ProductName = first?.ProductName; ret.ProductId = first?.ProductId ?? ret.ProductId;
            ret.Quantity = ret.Items.Sum(i => i.Quantity); ret.StockQuantity = ret.Quantity;
            ret.EnteredQuantity = ret.Items.Sum(i => i.EnteredQuantity);
            ret.RefundAmount = ret.Items.Sum(i => i.RefundAmount);
        }
    }

    public int Insert(ProductReturn ret)
    {
        if (ret.Items.Count == 0 && ret.ProductId > 0)
            ret.Items.Add(new ReturnItem { ProductId=ret.ProductId, Quantity=ret.StockQuantity > 0 ? ret.StockQuantity : ret.Quantity, Reason=ret.Reason, RefundAmount=ret.RefundAmount });
        if (ret.Items.Count == 0) throw new InvalidOperationException("Add at least one return item.");
        var postAfterSave = ret.IsPosted;
        ret.IsPosted = false;
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        ret.ReturnNo = string.IsNullOrWhiteSpace(ret.ReturnNo) ? $"RET-{DateTime.Now:yyyyMMddHHmmssfff}" : ret.ReturnNo;
        var first = ret.Items[0];
        ret.ReturnId = conn.ExecuteScalar<int>(@"INSERT INTO Returns
            (ReturnNo,ProductId,BatchNo,Quantity,UnitType,StockQuantity,ReturnType,Reason,Notes,PatientId,SupplierId,SaleId,
             RefundAmount,CreatedBy,CreatedAt,IsPosted,PostedAt)
            VALUES (@ReturnNo,@ProductId,@BatchNo,@Quantity,@UnitType,@StockQuantity,@ReturnType,@Reason,@Notes,@PatientId,@SupplierId,@SaleId,
             @RefundAmount,@CreatedBy,@CreatedAt,0,NULL); SELECT CONVERT(INT,SCOPE_IDENTITY());",
            new { ret.ReturnNo, ProductId=first.ProductId, ret.BatchNo, Quantity=ret.Items.Sum(i=>i.Quantity), StockQuantity=ret.Items.Sum(i=>i.Quantity),
                ret.UnitType, ret.ReturnType, ret.Reason, ret.Notes, ret.PatientId, ret.SupplierId, ret.SaleId,
                RefundAmount=ret.Items.Sum(i=>i.RefundAmount), ret.CreatedBy, ret.CreatedAt }, tx);
        foreach (var item in ret.Items)
        {
            if (item.Quantity <= 0) throw new InvalidOperationException("Return quantities must be greater than zero.");
            item.ReturnId = ret.ReturnId;
            conn.Execute(@"INSERT INTO ReturnItems (ReturnId,ProductId,StockID,Quantity,EnteredQuantity,UnitType,UnitPrice,PiecesPerPack,Reason,RefundAmount)
                VALUES (@ReturnId,@ProductId,@StockID,@Quantity,@EnteredQuantity,@UnitType,@UnitPrice,@PiecesPerPack,@Reason,@RefundAmount)", item, tx);
        }
        tx.Commit();
        if (postAfterSave) PostReturn(ret.ReturnId);
        return ret.ReturnId;
    }

    public void PostReturn(int returnId)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var ret = conn.QuerySingleOrDefault<ProductReturn>("SELECT * FROM Returns WITH (UPDLOCK,HOLDLOCK) WHERE ReturnId=@returnId", new { returnId }, tx)
            ?? throw new InvalidOperationException("Return was not found.");
        if (ret.IsPosted) { tx.Commit(); return; }
        var items = conn.Query<ReturnItem>("SELECT * FROM ReturnItems WHERE ReturnId=@returnId", new { returnId }, tx).ToList();
        foreach (var item in items)
        {
            // ReturnItems does not currently persist an expiry. A date-shaped BatchNo
            // is honored when present; otherwise the latest active product batch is
            // used. This keeps all stock movement in ProductStock, never Products.
            DateTime? expiryDate = DateTime.TryParse(ret.BatchNo, out var parsedExpiry)
                ? parsedExpiry.Date
                : null;
            var stockId = item.StockID ?? conn.ExecuteScalar<int?>(@"
                SELECT TOP (1) StockID
                FROM ProductStock WITH (UPDLOCK, HOLDLOCK)
                WHERE ProductID = @ProductId
                  AND QuantityAvailable > 0
                  AND IsArchived = 0
                  AND ExpiryDate >= CAST(GETDATE() AS DATE)
                  AND (@ExpiryDate IS NULL OR ExpiryDate = @ExpiryDate)
                ORDER BY ExpiryDate DESC, StockID DESC",
                new { item.ProductId, ExpiryDate = expiryDate }, tx);

            if (!stockId.HasValue)
                throw new InvalidOperationException($"No active stock batch was found for product #{item.ProductId}.");

            var affected = ret.ReturnType == "Patient Return"
                ? conn.Execute(@"UPDATE ProductStock
                    SET QuantityAvailable = QuantityAvailable + @Quantity,
                        UpdatedAt = CURRENT_TIMESTAMP
                    WHERE StockID = @StockID", new { item.Quantity, StockID = stockId.Value }, tx)
                : conn.Execute(@"UPDATE ProductStock
                    SET QuantityAvailable = QuantityAvailable - @Quantity,
                        UpdatedAt = CURRENT_TIMESTAMP
                    WHERE StockID = @StockID AND QuantityAvailable >= @Quantity",
                    new { item.Quantity, StockID = stockId.Value }, tx);

            if (affected != 1)
                throw new InvalidOperationException($"Unable to update stock batch for product #{item.ProductId}.");
        }
        conn.Execute("UPDATE Returns SET IsPosted=1,PostedAt=SYSDATETIME() WHERE ReturnId=@returnId", new { returnId }, tx);
        tx.Commit();
    }

    public decimal GetTodayTotalPatientReturns() => GetTodayTotal("Patient Return");
    public decimal GetTodayTotalSupplierReturns() => GetTodayTotal("Supplier Return");
    private decimal GetTodayTotal(string type)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal?>("SELECT SUM(RefundAmount) FROM Returns WHERE IsPosted=1 AND ReturnType=@type AND CAST(CreatedAt AS DATE)=CAST(GETDATE() AS DATE)", new { type }) ?? 0;
    }
    public IEnumerable<dynamic> GetDailyPatientReturnsLast30Days() => GetDaily("Patient Return");
    public IEnumerable<dynamic> GetDailySupplierReturnsLast30Days() => GetDaily("Supplier Return");
    private IEnumerable<dynamic> GetDaily(string type)
    {
        using var conn = _session.CreateConnection();
        return conn.Query(@"SELECT CAST(CreatedAt AS DATE) Date,SUM(RefundAmount) Total FROM Returns
            WHERE IsPosted=1 AND ReturnType=@type AND CreatedAt>=DATEADD(day,-30,GETDATE()) GROUP BY CAST(CreatedAt AS DATE)", new { type }).ToList();
    }
}
