using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class PatientRepository
{
    private readonly DatabaseSession _session;

    public PatientRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Patient> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Patient>(
            "SELECT * FROM Patients ORDER BY Name");
    }

    public Patient? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Patient>(
            "SELECT * FROM Patients WHERE PatientID = @id", new { id });
    }

    public IEnumerable<Patient> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Patient>(
            @"SELECT * FROM Patients
              WHERE Name LIKE @term OR Phone LIKE @term
              ORDER BY Name",
            new { term = $"%{term}%" });
    }

    public int Insert(Patient p)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount)
              VALUES (@Name, @Age, @Gender, @Phone, @Address, @Diagnosis, @Prescription, @ConsultationFee, @Discount);
              SELECT SCOPE_IDENTITY();", p);
    }

    public void Update(Patient p)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Patients SET
                Name = @Name, Age = @Age, Gender = @Gender,
                Phone = @Phone, Address = @Address, Diagnosis = @Diagnosis,
                Prescription = @Prescription, ConsultationFee = @ConsultationFee, Discount = @Discount
              WHERE PatientID = @PatientID", p);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        // Check for existing appointments or sales first
        var apptCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Appointments WHERE PatientID = @id", new { id });
        var salesCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Sales WHERE PatientID = @id", new { id });
        if (apptCount > 0 || salesCount > 0) return false;

        conn.Execute("DELETE FROM Patients WHERE PatientID = @id", new { id });
        return true;
    }
}
