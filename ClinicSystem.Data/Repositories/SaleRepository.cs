using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class SaleRepository
{
    private readonly DatabaseSession _session;
    public SaleRepository(DatabaseSession session) => _session = session;

    private const string SaleSelect = @"SELECT s.*,COALESCE(u.FullName,s.ReceptionistName) ReceptionistName
        FROM Sales s LEFT JOIN Users u ON s.ReceptionistId=u.UserID";

    public IEnumerable<Sale> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Sale>($"{SaleSelect} WHERE s.IsActive=1 ORDER BY s.SaleDate DESC,s.SaleID DESC");
    }

    public IEnumerable<Sale> GetSalesByReceptionist(int userId)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Sale>($"{SaleSelect} WHERE s.IsActive=1 AND s.ReceptionistId=@userId ORDER BY s.SaleDate DESC", new { userId });
    }

    public IEnumerable<SaleInvoiceSummaryRow> GetInvoiceSummary(DateTime from, DateTime to, int? receptionistId=null)
    {
        using var conn=_session.CreateConnection();
        return conn.Query<SaleInvoiceSummaryRow>(@"SELECT s.SaleID,s.InvoiceNumber,s.SaleDate,s.PatientName,
            COALESCE(u.FullName,s.ReceptionistName) ReceptionistName,s.GrandTotal FROM Sales s
            LEFT JOIN Users u ON s.ReceptionistId=u.UserID WHERE s.IsPosted=1 AND s.IsActive=1
            AND s.SaleDate>=@from AND s.SaleDate<@to AND (@receptionistId IS NULL OR s.ReceptionistId=@receptionistId)
            ORDER BY s.SaleDate DESC",new{from,to,receptionistId});
    }

    public IEnumerable<BestSellingProductRow> GetBestSellingProducts(DateTime from, DateTime to, int? receptionistId=null)
    {
        using var conn=_session.CreateConnection();
        return conn.Query<BestSellingProductRow>(@"SELECT p.ProductID,p.Name ProductName,SUM(si.StockQuantity) PiecesSold,
            SUM(si.LineTotal) Revenue FROM Sales s JOIN SaleItems si ON s.SaleID=si.SaleID JOIN Products p ON si.ProductID=p.ProductID
            WHERE s.IsPosted=1 AND s.IsActive=1 AND s.SaleDate>=@from AND s.SaleDate<@to
            AND (@receptionistId IS NULL OR s.ReceptionistId=@receptionistId)
            GROUP BY p.ProductID,p.Name ORDER BY PiecesSold DESC",new{from,to,receptionistId});
    }

    public IEnumerable<CompanySaleRow> GetCompanyWiseSales(DateTime from, DateTime to, int? receptionistId=null)
    {
        using var conn=_session.CreateConnection();
        return conn.Query<CompanySaleRow>(@"SELECT c.CompanyID,COALESCE(c.Name,'Unassigned') CompanyName,
            SUM(si.StockQuantity) PiecesSold,SUM(si.LineTotal) Revenue,
            SUM(si.StockQuantity*(p.PurchasePrice/NULLIF(p.PiecesPerUnit,0))) Cost
            FROM Sales s JOIN SaleItems si ON s.SaleID=si.SaleID JOIN Products p ON si.ProductID=p.ProductID
            LEFT JOIN Companies c ON p.CompanyID=c.CompanyID WHERE s.IsPosted=1 AND s.IsActive=1
            AND s.SaleDate>=@from AND s.SaleDate<@to AND (@receptionistId IS NULL OR s.ReceptionistId=@receptionistId)
            GROUP BY c.CompanyID,c.Name ORDER BY Revenue DESC",new{from,to,receptionistId});
    }

    public IEnumerable<Sale> GetByRange(DateTime fromInclusive, DateTime toExclusive, string? receptionistName = null)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Sale>($@"{SaleSelect} WHERE s.IsActive=1 AND s.SaleDate>=@fromInclusive AND s.SaleDate<@toExclusive
            AND (@receptionistName IS NULL OR COALESCE(u.FullName,s.ReceptionistName)=@receptionistName)
            ORDER BY s.SaleDate DESC", new { fromInclusive, toExclusive, receptionistName });
    }

    public (int UnitsSold, decimal CostOfGoods) GetItemMetricsByRange(DateTime fromInclusive, DateTime toExclusive, string? receptionistName = null)
    {
        using var conn = _session.CreateConnection();
        var row = conn.QuerySingle(@"SELECT ISNULL(SUM(si.StockQuantity),0) UnitsSold,
            ISNULL(SUM(si.StockQuantity*(p.PurchasePrice/NULLIF(p.PiecesPerUnit,0))),0) CostOfGoods
            FROM Sales s JOIN SaleItems si ON s.SaleID=si.SaleID JOIN Products p ON si.ProductID=p.ProductID
            LEFT JOIN Users u ON s.ReceptionistId=u.UserID WHERE s.IsPosted=1 AND s.IsActive=1
            AND s.SaleDate>=@fromInclusive AND s.SaleDate<@toExclusive
            AND (@receptionistName IS NULL OR COALESCE(u.FullName,s.ReceptionistName)=@receptionistName)",
            new { fromInclusive, toExclusive, receptionistName });
        return ((int)row.UnitsSold, (decimal)row.CostOfGoods);
    }

    public IEnumerable<string> GetReceptionistNames()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<string>(@"SELECT DISTINCT COALESCE(u.FullName,s.ReceptionistName) FROM Sales s
            LEFT JOIN Users u ON s.ReceptionistId=u.UserID WHERE COALESCE(u.FullName,s.ReceptionistName) IS NOT NULL ORDER BY 1");
    }

    public int? GetReceptionistIdByName(string name)
    {
        using var conn=_session.CreateConnection();
        return conn.QueryFirstOrDefault<int?>(@"SELECT TOP 1 COALESCE(u.UserID,s.ReceptionistId) FROM Sales s
            LEFT JOIN Users u ON s.ReceptionistId=u.UserID WHERE COALESCE(u.FullName,s.ReceptionistName)=@name",new{name});
    }

    public int GetCountForDate(DateTime date)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Sales WHERE IsActive=1 AND CAST(SaleDate AS DATE)=@date", new { date=date.Date });
    }

    public decimal GetTodayRevenue() => Scalar("SELECT ISNULL(SUM(GrandTotal),0) FROM Sales WHERE IsPosted=1 AND IsActive=1 AND CAST(SaleDate AS DATE)=CAST(GETDATE() AS DATE)");
    public decimal GetTotalRevenue() => Scalar("SELECT ISNULL(SUM(GrandTotal),0) FROM Sales WHERE IsPosted=1 AND IsActive=1");
    public decimal GetTotalRevenueLast30Days() => Scalar("SELECT ISNULL(SUM(GrandTotal),0) FROM Sales WHERE IsPosted=1 AND IsActive=1 AND SaleDate>=DATEADD(day,-29,CAST(GETDATE() AS DATE))");
    public decimal GetTodayCostOfGoodsSold() => Scalar(@"SELECT ISNULL(SUM(si.StockQuantity*(p.PurchasePrice/NULLIF(p.PiecesPerUnit,0))),0)
        FROM Sales s JOIN SaleItems si ON s.SaleID=si.SaleID JOIN Products p ON si.ProductID=p.ProductID
        WHERE s.IsPosted=1 AND s.IsActive=1 AND CAST(s.SaleDate AS DATE)=CAST(GETDATE() AS DATE)");
    public decimal GetTotalCostOfGoodsSold() => Scalar(@"SELECT ISNULL(SUM(si.StockQuantity*(p.PurchasePrice/NULLIF(p.PiecesPerUnit,0))),0)
        FROM Sales s JOIN SaleItems si ON s.SaleID=si.SaleID JOIN Products p ON si.ProductID=p.ProductID WHERE s.IsPosted=1 AND s.IsActive=1");

    private decimal Scalar(string sql)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(sql);
    }

    public IEnumerable<(DateTime Date, decimal Revenue)> GetDailyRevenueLast30Days()
    {
        using var conn = _session.CreateConnection();
        var rows = conn.Query(@"SELECT CAST(SaleDate AS DATE) SaleDay,ISNULL(SUM(GrandTotal),0) Revenue
            FROM Sales WHERE IsPosted=1 AND IsActive=1 AND SaleDate>=DATEADD(day,-29,CAST(GETDATE() AS DATE))
            GROUP BY CAST(SaleDate AS DATE) ORDER BY SaleDay");
        return rows.Select(r => ((DateTime)r.SaleDay, (decimal)r.Revenue)).ToList();
    }

    public IEnumerable<(DateTime Date, decimal Cogs)> GetDailyCostOfGoodsSoldLast30Days()
    {
        using var conn = _session.CreateConnection();
        var rows = conn.Query(@"SELECT CAST(s.SaleDate AS DATE) SaleDay,
            ISNULL(SUM(si.StockQuantity*(p.PurchasePrice/NULLIF(p.PiecesPerUnit,0))),0) Cogs
            FROM Sales s JOIN SaleItems si ON s.SaleID=si.SaleID JOIN Products p ON si.ProductID=p.ProductID
            WHERE s.IsPosted=1 AND s.IsActive=1 AND s.SaleDate>=DATEADD(day,-29,CAST(GETDATE() AS DATE))
            GROUP BY CAST(s.SaleDate AS DATE) ORDER BY SaleDay");
        return rows.Select(r => ((DateTime)r.SaleDay, (decimal)r.Cogs)).ToList();
    }

    public Sale? GetByIdWithItems(int id)
    {
        using var conn = _session.CreateConnection();
        var sale = conn.QuerySingleOrDefault<Sale>($"{SaleSelect} WHERE s.SaleID=@id AND s.IsActive=1", new { id });
        if (sale == null) return null;
        sale.Items = conn.Query<SaleItem>(@"SELECT si.*,p.Name ProductName FROM SaleItems si
            JOIN Products p ON si.ProductID=p.ProductID WHERE si.SaleID=@id", new { id }).ToList();
        return sale;
    }

    public Sale? GetLatestSaleForProduct(int productId, int? patientId = null)
    {
        using var conn = _session.CreateConnection();
        var sql = $@"{SaleSelect} JOIN SaleItems si ON s.SaleID = si.SaleID 
            WHERE s.IsPosted=1 AND s.IsActive=1 AND si.ProductID=@productId
            {(patientId.HasValue ? " AND s.PatientID=@patientId" : "")}
            ORDER BY s.SaleDate DESC";
        var sale = conn.QueryFirstOrDefault<Sale>(sql, new { productId, patientId });
        if (sale == null) return null;
        sale.Items = conn.Query<SaleItem>(@"SELECT si.*,p.Name ProductName FROM SaleItems si
            JOIN Products p ON si.ProductID=p.ProductID WHERE si.SaleID=@id", new { id = sale.SaleID }).ToList();
        return sale;
    }

    public IEnumerable<Sale> GetByPatientIdWithItems(int patientId) => Array.Empty<Sale>();

    public int Insert(Sale sale)
    {
        var postAfterSave = sale.IsPosted;
        sale.IsPosted = false;
        sale.InvoiceNumber = string.Empty;
        sale.PatientID = null;
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        var id = conn.ExecuteScalar<int>(@"INSERT INTO Sales
            (InvoiceNumber,SaleDate,PatientID,PatientName,GrandTotal,PaymentMethod,IsPosted,ReceptionistId,ReceptionistName,IsActive,SalesTax,PostedAt)
            VALUES ('',@SaleDate,NULL,@PatientName,@GrandTotal,@PaymentMethod,0,@ReceptionistId,@ReceptionistName,1,@SalesTax,NULL);
            SELECT CONVERT(INT,SCOPE_IDENTITY());", sale, tx);
        ReplaceItems(conn, tx, id, sale.Items);
        tx.Commit();
        if (postAfterSave)
        {
            sale.InvoiceNumber = PostSale(id);
            sale.IsPosted = true;
            sale.PostedAt = DateTime.Now;
        }
        return id;
    }

    public void Update(Sale sale)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        var posted = conn.ExecuteScalar<bool?>("SELECT IsPosted FROM Sales WHERE SaleID=@SaleID AND IsActive=1", sale, tx);
        if (!posted.HasValue) throw new InvalidOperationException("Sale was not found.");
        if (posted.Value) throw new InvalidOperationException("Posted sales cannot be edited.");
        sale.PatientID = null;
        conn.Execute(@"UPDATE Sales SET SaleDate=@SaleDate,PatientID=NULL,PatientName=@PatientName,GrandTotal=@GrandTotal,
            PaymentMethod=@PaymentMethod,ReceptionistId=@ReceptionistId,ReceptionistName=@ReceptionistName,SalesTax=@SalesTax WHERE SaleID=@SaleID", sale, tx);
        conn.Execute("DELETE FROM SaleItems WHERE SaleID=@SaleID", sale, tx);
        ReplaceItems(conn, tx, sale.SaleID, sale.Items);
        tx.Commit();
    }

    private static void ReplaceItems(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, int saleId, IEnumerable<SaleItem> items)
    {
        foreach (var item in items)
        {
            item.SaleID = saleId;
            item.UnitTypeSold = "Pieces";
            item.StockQuantity = item.Quantity;
            conn.Execute(@"INSERT INTO SaleItems (SaleID,ProductID,Quantity,UnitTypeSold,StockQuantity,UnitPrice,Discount,Tax,LineTotal)
                VALUES (@SaleID,@ProductID,@Quantity,@UnitTypeSold,@StockQuantity,@UnitPrice,@Discount,@Tax,@LineTotal)", item, tx);
        }
    }

    public string PostSale(int saleId)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var sale = conn.QuerySingleOrDefault<Sale>("SELECT * FROM Sales WITH (UPDLOCK,HOLDLOCK) WHERE SaleID=@saleId AND IsActive=1", new { saleId }, tx)
            ?? throw new InvalidOperationException("Sale was not found.");
        if (sale.IsPosted) { tx.Commit(); return sale.InvoiceNumber; }
        var items = conn.Query<SaleItem>("SELECT * FROM SaleItems WHERE SaleID=@saleId", new { saleId }, tx).ToList();
        if (items.Count == 0) throw new InvalidOperationException("A sale must contain at least one item.");
        foreach (var item in items)
        {
            var updated = conn.Execute(@"UPDATE Products SET Stock=Stock-@StockQuantity,LastStockUpdateDate=CAST(GETDATE() AS DATE)
                WHERE ProductID=@ProductID AND IsActive=1 AND Stock>=@StockQuantity", item, tx);
            if (updated != 1) throw new InvalidOperationException($"Insufficient stock for product #{item.ProductID}.");
        }
        var next = conn.ExecuteScalar<int>("SELECT ISNULL(MAX(SaleID),0) FROM Sales WITH (UPDLOCK,HOLDLOCK)", transaction: tx);
        var invoice = $"SAL-{next:D6}";
        conn.Execute("UPDATE Sales SET InvoiceNumber=@invoice,IsPosted=1,PostedAt=SYSDATETIME() WHERE SaleID=@saleId", new { invoice, saleId }, tx);
        tx.Commit();
        return invoice;
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.Execute("UPDATE Sales SET IsActive=0 WHERE SaleID=@id AND IsPosted=0 AND IsActive=1", new { id }) == 1;
    }

    public int SoftDeleteAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Execute("UPDATE Sales SET IsActive=0 WHERE IsActive=1 AND IsPosted=0");
    }
}
