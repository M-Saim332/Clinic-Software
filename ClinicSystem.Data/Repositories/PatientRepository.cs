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

    public int GetCount()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Patients");
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
            @"INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, LastVisitDate, CNIC)
              VALUES (@Name, @Age, @Gender, @Phone, @Address, @Diagnosis, @Prescription, @ConsultationFee, @Discount, @VisitStatus, @LastVisitDate, @CNIC);
              SELECT SCOPE_IDENTITY();", p);
    }

    public void Update(Patient p)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Patients SET
                Name = @Name, Age = @Age, Gender = @Gender,
                Phone = @Phone, Address = @Address, Diagnosis = @Diagnosis,
                Prescription = @Prescription, ConsultationFee = @ConsultationFee, Discount = @Discount,
                VisitStatus = @VisitStatus, LastVisitDate = @LastVisitDate, CNIC = @CNIC
              WHERE PatientID = @PatientID", p);
    }

    public void UpdateVisitStatus(int patientId, string status, DateTime date)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Patients SET VisitStatus = @status, LastVisitDate = @date WHERE PatientID = @patientId",
            new { status, date = date.Date, patientId });
    }

    public bool Delete(int id)
    {
        try
        {
            using var conn = _session.CreateConnection();
            using var tx = conn.BeginTransaction();
            
            // Cascade delete Appointments
            conn.Execute("DELETE FROM Appointments WHERE PatientID = @id", new { id }, tx);
            
            // Cascade delete SaleItems related to Patient's Sales
            conn.Execute(@"
                DELETE FROM SaleItems 
                WHERE SaleID IN (SELECT SaleID FROM Sales WHERE PatientID = @id)", 
                new { id }, tx);
                
            // Cascade delete Sales
            conn.Execute("DELETE FROM Sales WHERE PatientID = @id", new { id }, tx);
            
            // Delete the Patient
            conn.Execute("DELETE FROM Patients WHERE PatientID = @id", new { id }, tx);
            
            tx.Commit();
            return true;
        }
        catch 
        {
            return false;
        }
    }
}
