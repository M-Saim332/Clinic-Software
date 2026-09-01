using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class PatientRepository
{
    private readonly DatabaseSession _session;

    public PatientRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Patient> GetAll(string context = "Clinical")
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Patient>(
            @"SELECT PatientID, Name, Age, Gender, Phone, CNIC, Address, ReasonOfVisit, PatientContext,
                     NextAppointmentDate, NextAppointmentTime,
                     VisitStatus, LastVisitDate, IsActive
              FROM Patients WHERE IsActive = 1 AND (PatientContext=@context OR PatientContext='Both') ORDER BY Name", new { context });
    }

    public int GetCount()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Patients WHERE IsActive = 1");
    }

    public Patient? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Patient>(
            "SELECT * FROM Patients WHERE PatientID = @id AND IsActive = 1", new { id });
    }

    /// <summary>Case-insensitive wildcard search over the patient registry's searchable identifiers.</summary>
    public IEnumerable<Patient> Search(string searchText, string context = "Clinical")
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Patient>(
            @"SELECT PatientID, Name, Age, Gender, Phone, CNIC, Address, ReasonOfVisit, PatientContext,
                     NextAppointmentDate, NextAppointmentTime, VisitStatus, LastVisitDate, IsActive
              FROM Patients
              WHERE IsActive = 1
                AND (PatientContext=@context OR PatientContext='Both')
                AND (Name LIKE @term OR Phone LIKE @term OR CNIC LIKE @term
                     OR CONVERT(NVARCHAR(20), PatientID) LIKE @term)
              ORDER BY Name ASC",
            new { term = $"%{searchText.Trim()}%", context });
    }

    public int Insert(Patient p)
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO Patients (Name, Age, Gender, Phone, Address, VisitStatus, LastVisitDate, CNIC, NextAppointmentTime, ReasonOfVisit, PatientContext)
              VALUES (@Name, @Age, @Gender, @Phone, @Address, @VisitStatus, @LastVisitDate, @CNIC, @NextAppointmentTime, @ReasonOfVisit, @PatientContext);
              SELECT CONVERT(INT,SCOPE_IDENTITY());", p);
    }

    public void Update(Patient p)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Patients SET
                Name = @Name, Age = @Age, Gender = @Gender,
                Phone = @Phone, Address = @Address,
                VisitStatus = @VisitStatus, LastVisitDate = @LastVisitDate, CNIC = @CNIC,
                NextAppointmentTime = @NextAppointmentTime, ReasonOfVisit=@ReasonOfVisit, PatientContext=@PatientContext
              WHERE PatientID = @PatientID", p);
    }

    public Patient SyncFromAppointment(int appointmentId)
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        var appointment = conn.QuerySingle<Appointment>("SELECT * FROM Appointments WHERE AppointmentID=@appointmentId", new { appointmentId }, tx);
        var patient = conn.QueryFirstOrDefault<Patient>(@"SELECT TOP 1 * FROM Patients WHERE IsActive=1 AND
            ((@CNIC <> '' AND CNIC=@CNIC) OR (@Phone <> '' AND Phone=@Phone) OR PatientID=@PatientID)
            ORDER BY CASE WHEN PatientID=@PatientID THEN 0 ELSE 1 END", new {
                CNIC = appointment.CNIC?.Trim() ?? "", Phone = appointment.Phone?.Trim() ?? "", appointment.PatientID
            }, tx);
        if (patient == null)
        {
            patient = new Patient { Name=appointment.PatientName ?? "Unnamed", Phone=appointment.Phone, CNIC=appointment.CNIC,
                Age=appointment.Age, Gender=appointment.Gender, ReasonOfVisit=appointment.Reason, PatientContext="Clinical",
                VisitStatus="Waiting", LastVisitDate=appointment.AppointmentDate };
            patient.PatientID = conn.ExecuteScalar<int>(@"INSERT INTO Patients
                (Name,Age,Gender,Phone,CNIC,ReasonOfVisit,PatientContext,VisitStatus,LastVisitDate,IsActive)
                VALUES (@Name,@Age,@Gender,@Phone,@CNIC,@ReasonOfVisit,@PatientContext,@VisitStatus,@LastVisitDate,1);
                SELECT CONVERT(INT,SCOPE_IDENTITY());", patient, tx);
        }
        else
        {
            patient.Name=appointment.PatientName ?? patient.Name; patient.Phone=appointment.Phone ?? patient.Phone;
            patient.CNIC=appointment.CNIC ?? patient.CNIC; patient.Age=appointment.Age ?? patient.Age;
            patient.Gender=appointment.Gender ?? patient.Gender; patient.ReasonOfVisit=appointment.Reason;
            if (patient.PatientContext == "Pharma") patient.PatientContext = "Both";
            conn.Execute(@"UPDATE Patients SET Name=@Name,Phone=@Phone,CNIC=@CNIC,Age=@Age,Gender=@Gender,
                ReasonOfVisit=@ReasonOfVisit,PatientContext=@PatientContext WHERE PatientID=@PatientID", patient, tx);
        }
        conn.Execute("UPDATE Appointments SET PatientID=@PatientID WHERE AppointmentID=@appointmentId", new { patient.PatientID, appointmentId }, tx);
        tx.Commit();
        return patient;
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

    public void ShareWithPharma(int patientId)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(@"UPDATE Patients SET PatientContext=CASE WHEN PatientContext='Clinical' THEN 'Both' ELSE PatientContext END
            WHERE PatientID=@patientId AND IsActive=1", new { patientId });
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
