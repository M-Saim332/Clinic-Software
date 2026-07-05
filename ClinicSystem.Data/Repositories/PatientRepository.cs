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
              WHERE Name LIKE @term OR Contact LIKE @term
              ORDER BY Name",
            new { term = $"%{term}%" });
    }

    public int Insert(Patient p)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Patients (Name, DateOfBirth, Age, Gender, Contact, Address, MedicalHistory)
              VALUES (@Name, @DateOfBirth, @Age, @Gender, @Contact, @Address, @MedicalHistory);
              SELECT SCOPE_IDENTITY();", p);
    }

    public void Update(Patient p)
    {
        p.UpdatedAt = DateTime.Now;
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Patients SET
                Name = @Name, DateOfBirth = @DateOfBirth, Age = @Age, Gender = @Gender,
                Contact = @Contact, Address = @Address, MedicalHistory = @MedicalHistory,
                UpdatedAt = @UpdatedAt
              WHERE PatientID = @PatientID", p);
    }

    public bool Delete(int id)
    {
        using var conn = _session.CreateConnection();
        // Check for existing prescriptions first
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Prescriptions WHERE PatientID = @id", new { id });
        if (count > 0) return false;

        conn.Execute("DELETE FROM Patients WHERE PatientID = @id", new { id });
        return true;
    }
}
