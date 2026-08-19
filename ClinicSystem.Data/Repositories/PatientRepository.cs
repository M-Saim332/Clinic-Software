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
            @"SELECT PatientID, Name, Age, Gender, Phone, CNIC, Address,
                     Diagnosis, Prescription, ConsultationFee,
                     ISNULL(Discount, 0) AS Discount,
                     NextAppointmentDate, NextAppointmentTime,
                     VisitStatus, LastVisitDate, IsActive
              FROM Patients WHERE IsActive = 1 ORDER BY Name");
    }

    public int GetCount()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Patients WHERE IsActive = 1");
    }

    public decimal GetTotalConsultationFee()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<decimal>("SELECT ISNULL(SUM(ConsultationFee - (ConsultationFee * ISNULL(Discount, 0) / 100.0)), 0) FROM Patients WHERE IsActive = 1");
    }

    public Patient? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Patient>(
            "SELECT * FROM Patients WHERE PatientID = @id AND IsActive = 1", new { id });
    }

    public IEnumerable<Patient> Search(string term)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Patient>(
            @"SELECT * FROM Patients
              WHERE IsActive = 1 AND (Name LIKE @term OR Phone LIKE @term)
              ORDER BY Name",
            new { term = $"%{term}%" });
    }

    public int Insert(Patient p)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Patients (Name, Age, Gender, Phone, Address, Diagnosis, Prescription, ConsultationFee, Discount, VisitStatus, LastVisitDate, CNIC, NextAppointmentTime)
              VALUES (@Name, @Age, @Gender, @Phone, @Address, @Diagnosis, @Prescription, @ConsultationFee, @Discount, @VisitStatus, @LastVisitDate, @CNIC, @NextAppointmentTime);
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
                VisitStatus = @VisitStatus, LastVisitDate = @LastVisitDate, CNIC = @CNIC,
                NextAppointmentTime = @NextAppointmentTime
              WHERE PatientID = @PatientID", p);
    }

    public void UpdateVisitStatus(int patientId, string? status, DateTime date)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Patients SET VisitStatus = @status, LastVisitDate = @date WHERE PatientID = @patientId",
            new { status, date = date.Date, patientId });
    }

    public void UpdateVisitStatusAndTime(int patientId, string status, DateTime date, TimeSpan? time)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Patients SET VisitStatus = @status, LastVisitDate = @date, NextAppointmentTime = @time WHERE PatientID = @patientId",
            new { status, date = date.Date, time, patientId });
    }

    public bool Delete(int id)
    {
        try
        {
            using var conn = _session.CreateConnection();
            return conn.Execute("UPDATE Patients SET IsActive = 0 WHERE PatientID = @id AND IsActive = 1", new { id }) == 1;
        }
        catch 
        {
            return false;
        }
    }

    public int SoftDeleteAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Execute("UPDATE Patients SET IsActive = 0 WHERE IsActive = 1");
    }
}
