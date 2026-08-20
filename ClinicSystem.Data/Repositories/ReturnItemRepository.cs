using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class ReturnItemRepository
{
    private readonly DatabaseSession _session;
    public ReturnItemRepository(DatabaseSession session) => _session = session;

    public IEnumerable<ReturnItem> GetByReturnId(int returnId)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<ReturnItem>(@"SELECT ri.*,p.Name ProductName,p.Type ProductType FROM ReturnItems ri
            JOIN Products p ON ri.ProductId=p.ProductID WHERE ri.ReturnId=@returnId", new { returnId });
    }

    public int Insert(ReturnItem item)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(@"INSERT INTO ReturnItems (ReturnId,ProductId,Quantity,Reason,RefundAmount)
            VALUES (@ReturnId,@ProductId,@Quantity,@Reason,@RefundAmount); SELECT CONVERT(INT,SCOPE_IDENTITY());", item);
    }

    public void Update(ReturnItem item)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(@"UPDATE ReturnItems SET ProductId=@ProductId,Quantity=@Quantity,Reason=@Reason,RefundAmount=@RefundAmount
            WHERE ReturnItemID=@ReturnItemID", item);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.Execute(@"DELETE ri FROM ReturnItems ri JOIN Returns r ON ri.ReturnId=r.ReturnId
            WHERE ri.ReturnItemID=@id AND r.IsPosted=0", new { id }) == 1;
    }
}
