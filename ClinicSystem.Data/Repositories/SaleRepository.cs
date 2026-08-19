using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class SaleRepository
{
    private readonly DatabaseSession _session;

    public SaleRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Sale> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Sale>(
            @"SELECT s.*, COALESCE(p.Name, s.PatientName) AS PatientName,
                     COALESCE(u.FullName, s.ReceptionistName) AS ReceptionistName
              FROM Sales s
              LEFT JOIN Patients p ON s.PatientID = p.PatientID
              LEFT JOIN Users u ON s.ReceptionistId = u.UserID
              WHERE s.IsActive = 1
              ORDER BY s.SaleDate DESC");
    }

    public IEnumerable<Sale> GetByRange(DateTime fromInclusive, DateTime toExclusive, string? receptionistName = null)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Sale>(
            @"SELECT s.*, COALESCE(p.Name, s.PatientName) AS PatientName,
                     COALESCE(u.FullName, s.ReceptionistName) AS ReceptionistName
              FROM Sales s
              LEFT JOIN Patients p ON s.PatientID = p.PatientID
              LEFT JOIN Users u ON s.ReceptionistId = u.UserID
              WHERE s.SaleDate >= @fromInclusive AND s.SaleDate < @toExclusive
                AND (@receptionistName IS NULL OR COALESCE(u.FullName, s.ReceptionistName) = @receptionistName)
              ORDER BY s.SaleDate DESC", new { fromInclusive, toExclusive, receptionistName });
    }

    public (int UnitsSold, decimal CostOfGoods) GetItemMetricsByRange(DateTime fromInclusive, DateTime toExclusive, string? receptionistName = null)
    {
        using var conn = _session.CreateConnection();
        var row = conn.QuerySingle(
            @"SELECT ISNULL(SUM(si.StockQuantity), 0) AS UnitsSold,
                     ISNULL(SUM(si.StockQuantity * (p.PurchasePrice / NULLIF(p.TabletsPerBox, 0))), 0) AS CostOfGoods
              FROM Sales s
              JOIN SaleItems si ON s.SaleID = si.SaleID
              JOIN Products p ON si.ProductID = p.ProductID
              LEFT JOIN Users u ON s.ReceptionistId = u.UserID
              WHERE s.IsPosted = 1 AND s.SaleDate >= @fromInclusive AND s.SaleDate < @toExclusive
                AND (@receptionistName IS NULL OR COALESCE(u.FullName, s.ReceptionistName) = @receptionistName)",
            new { fromInclusive, toExclusive, receptionistName });
        return ((int)row.UnitsSold, (decimal)row.CostOfGoods);
    }

    public IEnumerable<string> GetReceptionistNames()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<string>(@"SELECT DISTINCT COALESCE(u.FullName, s.ReceptionistName)
            FROM Sales s LEFT JOIN Users u ON s.ReceptionistId = u.UserID
            WHERE COALESCE(u.FullName, s.ReceptionistName) IS NOT NULL ORDER BY 1");
    }

    public int GetCountForDate(DateTime date)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Sales WHERE IsActive = 1 AND CAST(SaleDate AS DATE) = @date",
            new { date });
    }

    public decimal GetTodayRevenue()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(
            "SELECT ISNULL(SUM(GrandTotal), 0) FROM Sales WHERE IsPosted = 1 AND CAST(SaleDate AS DATE) = CAST(GETDATE() AS DATE)");
    }

    public decimal GetTodayCostOfGoodsSold()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(
            @"SELECT ISNULL(SUM(si.StockQuantity * (p.PurchasePrice / NULLIF(p.TabletsPerBox, 0))), 0)
              FROM Sales s
              JOIN SaleItems si ON s.SaleID = si.SaleID
              JOIN Products p ON si.ProductID = p.ProductID
              WHERE s.IsPosted = 1 AND CAST(s.SaleDate AS DATE) = CAST(GETDATE() AS DATE)");
    }

    public decimal GetTotalRevenue()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>("SELECT ISNULL(SUM(GrandTotal), 0) FROM Sales WHERE IsPosted = 1");
    }

    public decimal GetTotalCostOfGoodsSold()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(
            @"SELECT ISNULL(SUM(si.StockQuantity * (p.PurchasePrice / NULLIF(p.TabletsPerBox, 0))), 0)
              FROM Sales s
              JOIN SaleItems si ON s.SaleID = si.SaleID
              JOIN Products p ON si.ProductID = p.ProductID
              WHERE s.IsPosted = 1");
    }

    public decimal GetTotalRevenueLast30Days()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(
            "SELECT ISNULL(SUM(GrandTotal), 0) FROM Sales WHERE IsPosted = 1 AND SaleDate >= DATEADD(day, -29, CAST(GETDATE() AS DATE))");
    }

    /// <summary>Returns the total consultation fees from all posted sales (all-time).</summary>
    public decimal GetTotalConsultationFee()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>(
            "SELECT ISNULL(SUM(ConsultationFee), 0) FROM Sales WHERE IsPosted = 1");
    }

    /// <summary>Returns daily (date, revenue, consultationFee) for the last 30 days for chart plotting.</summary>
    public IEnumerable<(DateTime Date, decimal Revenue, decimal Consultation)> GetDailyRevenueLast30Days()
    {
        using var conn = _session.CreateConnection();
        var rows = conn.Query(
            @"SELECT CAST(SaleDate AS DATE) AS SaleDay,
                     ISNULL(SUM(GrandTotal), 0)        AS Revenue,
                     ISNULL(SUM(ConsultationFee), 0)   AS Consultation
              FROM Sales
              WHERE IsPosted = 1
                AND SaleDate >= DATEADD(day, -29, CAST(GETDATE() AS DATE))
              GROUP BY CAST(SaleDate AS DATE)
              ORDER BY SaleDay");
        return rows.Select(r => ((DateTime)r.SaleDay, (decimal)r.Revenue, (decimal)r.Consultation)).ToList();
    }

    /// <summary>Returns daily COGS for the last 30 days for accurate profit chart plotting.</summary>
    public IEnumerable<(DateTime Date, decimal Cogs)> GetDailyCostOfGoodsSoldLast30Days()
    {
        using var conn = _session.CreateConnection();
        var rows = conn.Query(
            @"SELECT CAST(s.SaleDate AS DATE) AS SaleDay,
                     ISNULL(SUM(si.StockQuantity * (p.PurchasePrice / NULLIF(p.TabletsPerBox, 0))), 0) AS Cogs
              FROM Sales s
              JOIN SaleItems si ON s.SaleID = si.SaleID
              JOIN Products p ON si.ProductID = p.ProductID
              WHERE s.IsPosted = 1
                AND s.SaleDate >= DATEADD(day, -29, CAST(GETDATE() AS DATE))
              GROUP BY CAST(s.SaleDate AS DATE)
              ORDER BY SaleDay");
        return rows.Select(r => ((DateTime)r.SaleDay, (decimal)r.Cogs)).ToList();
    }


    public Sale? GetByIdWithItems(int id)
    {
        using var conn = _session.CreateConnection();
        var sale = conn.QuerySingleOrDefault<Sale>(
            @"SELECT s.*, COALESCE(p.Name, s.PatientName) AS PatientName,
                     COALESCE(u.FullName, s.ReceptionistName) AS ReceptionistName
              FROM Sales s
              LEFT JOIN Patients p ON s.PatientID = p.PatientID
              LEFT JOIN Users u ON s.ReceptionistId = u.UserID
              WHERE s.SaleID = @id AND s.IsActive = 1", new { id });

        if (sale == null) return null;

        sale.Items = conn.Query<SaleItem>(
            @"SELECT si.*, m.Name AS ProductName
              FROM SaleItems si
              JOIN Products m ON si.ProductID = m.ProductID
              WHERE si.SaleID = @id", new { id }).ToList();

        return sale;
    }

    public IEnumerable<Sale> GetByPatientIdWithItems(int patientId)
    {
        using var conn = _session.CreateConnection();
        var sales = conn.Query<Sale>(
            @"SELECT s.*, p.Name AS PatientName
              FROM Sales s
              LEFT JOIN Patients p ON s.PatientID = p.PatientID
              WHERE s.PatientID = @patientId AND s.IsActive = 1 AND s.IsPosted = 1
              ORDER BY s.SaleDate DESC", new { patientId }).ToList();

        if (sales.Any())
        {
            var saleIds = sales.Select(s => s.SaleID).ToArray();
            var items = conn.Query<SaleItem>(
                @"SELECT si.*, m.Name AS ProductName
                  FROM SaleItems si
                  JOIN Products m ON si.ProductID = m.ProductID
                  WHERE si.SaleID IN @saleIds", new { saleIds }).ToList();

            foreach (var sale in sales)
            {
                sale.Items = items.Where(i => i.SaleID == sale.SaleID).ToList();
            }
        }

        return sales;
    }

    public int Insert(Sale s)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var saleId = conn.ExecuteScalar<int>(
                @"INSERT INTO Sales (InvoiceNumber, SaleDate, PatientID, PatientName, ConsultationFee, GrandTotal, PaymentMethod, IsPosted, ReceptionistId, ReceptionistName, IsActive)
                  VALUES (@InvoiceNumber, @SaleDate, @PatientID, @PatientName, @ConsultationFee, @GrandTotal, @PaymentMethod, @IsPosted, @ReceptionistId, @ReceptionistName, 1);
                  SELECT SCOPE_IDENTITY();", s, tx);

            foreach (var item in s.Items)
            {
                item.SaleID = saleId;
                conn.Execute(
                    @"INSERT INTO SaleItems (SaleID, ProductID, Quantity, UnitTypeSold, StockQuantity, UnitPrice, Discount, Tax, LineTotal)
                      VALUES (@SaleID, @ProductID, @Quantity, @UnitTypeSold, @StockQuantity, @UnitPrice, @Discount, @Tax, @LineTotal)",
                    item, tx);

                // If posted on insert, decrement product stock
                if (s.IsPosted)
                {
                    var updated = conn.Execute(
                        "UPDATE Products SET Stock = Stock - @StockQuantity WHERE ProductID = @ProductID AND IsActive = 1 AND Stock >= @StockQuantity",
                        new { item.StockQuantity, item.ProductID }, tx);
                    if (updated != 1) throw new InvalidOperationException($"Insufficient stock for {item.ProductName ?? $"product #{item.ProductID}"}.");
                }
            }

            tx.Commit();
            return saleId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Update(Sale s)
    {
        using var conn = _session.CreateConnection();
        // Check if already posted in DB
        var current = conn.QuerySingleOrDefault<Sale>("SELECT * FROM Sales WHERE SaleID = @SaleID", new { s.SaleID });
        if (current == null) return;
        if (current.IsPosted)
        {
            throw new InvalidOperationException("This sale has already been posted and cannot be edited.");
        }

        using var tx = conn.BeginTransaction();
        try
        {
            conn.Execute(
                @"UPDATE Sales SET
                    InvoiceNumber = @InvoiceNumber, SaleDate = @SaleDate, PatientID = @PatientID,
                    PatientName = @PatientName, ConsultationFee = @ConsultationFee, GrandTotal = @GrandTotal,
                    PaymentMethod = @PaymentMethod, IsPosted = @IsPosted, ReceptionistId = @ReceptionistId, ReceptionistName = @ReceptionistName
                  WHERE SaleID = @SaleID", s, tx);

            // Delete old items
            conn.Execute("DELETE FROM SaleItems WHERE SaleID = @SaleID", new { s.SaleID }, tx);

            // Insert new items
            foreach (var item in s.Items)
            {
                item.SaleID = s.SaleID;
                conn.Execute(
                    @"INSERT INTO SaleItems (SaleID, ProductID, Quantity, UnitTypeSold, StockQuantity, UnitPrice, Discount, Tax, LineTotal)
                      VALUES (@SaleID, @ProductID, @Quantity, @UnitTypeSold, @StockQuantity, @UnitPrice, @Discount, @Tax, @LineTotal)",
                    item, tx);

                // If now posting, decrement product stock
                if (s.IsPosted)
                {
                    var updated = conn.Execute(
                        "UPDATE Products SET Stock = Stock - @StockQuantity WHERE ProductID = @ProductID AND IsActive = 1 AND Stock >= @StockQuantity",
                        new { item.StockQuantity, item.ProductID }, tx);
                    if (updated != 1) throw new InvalidOperationException($"Insufficient stock for {item.ProductName ?? $"product #{item.ProductID}"}.");
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public bool Delete(int id)
    {
        try
        {
            using var conn = _session.CreateConnection();
            var current = conn.QuerySingleOrDefault<Sale>("SELECT * FROM Sales WHERE SaleID = @id", new { id });
            if (current == null) return false;
            if (current.IsPosted) return false;

            return conn.Execute("UPDATE Sales SET IsActive = 0 WHERE SaleID = @id", new { id }) == 1;
        }
        catch
        {
            return false;
        }
    }

    public int SoftDeleteAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Execute("UPDATE Sales SET IsActive = 0 WHERE IsActive = 1");
    }
}
