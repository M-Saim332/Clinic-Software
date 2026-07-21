using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class AppointmentRepository
{
    private readonly DatabaseSession _session;

    public AppointmentRepository(DatabaseSession session) => _session = session;

    public IEnumerable<Appointment> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Appointment>(
            @"SELECT a.*, p.Name AS PatientName, p.Phone AS PatientPhone, u.Username AS DoctorName
              FROM Appointments a
              LEFT JOIN Patients p ON a.PatientID = p.PatientID
              JOIN Users u ON a.DoctorID = u.UserID
              ORDER BY a.AppointmentDate DESC, a.AppointmentTime DESC");
    }

    public int GetTodayCount()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Appointments WHERE CAST(AppointmentDate AS DATE) = CAST(GETDATE() AS DATE)");
    }

    public int GetTodayDistinctPatientCount()
    {
        using var conn = _session.CreateConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(DISTINCT PatientID) FROM Appointments WHERE CAST(AppointmentDate AS DATE) = CAST(GETDATE() AS DATE) AND PatientID IS NOT NULL");
    }


    public void CleanupOldAppointments()
    {
        using var conn = _session.CreateConnection();
        // Delete appointments where the appointment date is more than 3 days in the past
        conn.Execute("DELETE FROM Appointments WHERE AppointmentDate < DATEADD(day, -3, CAST(GETDATE() AS DATE))");
    }

    public Appointment? GetById(int id)
    {
        using var conn = _session.CreateConnection();
        return conn.QuerySingleOrDefault<Appointment>(
            @"SELECT a.*, p.Name AS PatientName, p.Phone AS PatientPhone, u.Username AS DoctorName
              FROM Appointments a
              LEFT JOIN Patients p ON a.PatientID = p.PatientID
              JOIN Users u ON a.DoctorID = u.UserID
              WHERE a.AppointmentID = @id", new { id });
    }

    public IEnumerable<Appointment> GetByDate(DateTime date)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Appointment>(
            @"SELECT a.*, p.Name AS PatientName, p.Phone AS PatientPhone, u.Username AS DoctorName
              FROM Appointments a
              LEFT JOIN Patients p ON a.PatientID = p.PatientID
              JOIN Users u ON a.DoctorID = u.UserID
              WHERE a.AppointmentDate = @date
              ORDER BY a.AppointmentTime ASC", new { date = date.Date });
    }

    public bool CheckConflict(int doctorId, DateTime date, TimeSpan time, int? excludeAppointmentId = null)
    {
        using var conn = _session.CreateConnection();
        string sql = @"SELECT COUNT(*) FROM Appointments 
                       WHERE DoctorID = @doctorId 
                         AND AppointmentDate = @date 
                         AND AppointmentTime = @time";
        if (excludeAppointmentId.HasValue)
        {
            sql += " AND AppointmentID <> @excludeAppointmentId";
        }
        var count = conn.ExecuteScalar<int>(sql, new { doctorId, date = date.Date, time, excludeAppointmentId });
        return count > 0;
    }

    public int Insert(Appointment a)
    {
        if (CheckConflict(a.DoctorID, a.AppointmentDate, a.AppointmentTime))
        {
            throw new InvalidOperationException("Doctor is already booked for this date and time.");
        }

        using var conn = _session.CreateConnection();

        // Auto-generate AppointmentNo as a sequential Token number
        if (string.IsNullOrEmpty(a.AppointmentNo))
        {
            int nextSeq = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM Appointments WHERE AppointmentDate = @date", 
                new { date = a.AppointmentDate.Date }) + 1;
            a.AppointmentNo = $"Token-{nextSeq}";
        }

        return conn.ExecuteScalar<int>(
            @"INSERT INTO Appointments 
                (AppointmentNo, PatientID, PatientName, Phone, Gender, Age, DoctorID, AppointmentDate, AppointmentTime, Reason, Status, Remarks, CancellationReason, CreatedAt)
              VALUES 
                (@AppointmentNo, @PatientID, @PatientName, @Phone, @Gender, @Age, @DoctorID, @AppointmentDate, @AppointmentTime, @Reason, @Status, @Remarks, @CancellationReason, @CreatedAt);
              SELECT SCOPE_IDENTITY();", a);
    }

    public void Update(Appointment a)
    {
        if (CheckConflict(a.DoctorID, a.AppointmentDate, a.AppointmentTime, a.AppointmentID))
        {
            throw new InvalidOperationException("Doctor is already booked for this date and time.");
        }

        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Appointments SET
                PatientID = @PatientID, PatientName = @PatientName, Phone = @Phone, 
                Gender = @Gender, Age = @Age,
                DoctorID = @DoctorID, AppointmentDate = @AppointmentDate,
                AppointmentTime = @AppointmentTime, Reason = @Reason, Status = @Status,
                Remarks = @Remarks, CancellationReason = @CancellationReason
              WHERE AppointmentID = @AppointmentID", a);
    }

    public void UpdateStatus(int appointmentId, string status, string? cancellationReason)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(
            @"UPDATE Appointments SET Status = @status, CancellationReason = @cancellationReason 
              WHERE AppointmentID = @appointmentId", 
            new { appointmentId, status, cancellationReason });
    }

    public bool Delete(int id)
    {
        try
        {
            using var conn = _session.CreateConnection();
            conn.Execute("DELETE FROM Appointments WHERE AppointmentID = @id", new { id });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
