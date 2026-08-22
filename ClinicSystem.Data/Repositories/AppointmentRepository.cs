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
        // Intentionally retained as a no-op for compatibility. Appointments are clinical history
        // and must never be automatically deleted.
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

    public Patient? GetPatientByCNICOrPhone(string? cnic, string? phone)
    {
        if (string.IsNullOrWhiteSpace(cnic) && string.IsNullOrWhiteSpace(phone)) return null;
        using var conn = _session.CreateConnection();
        return conn.QueryFirstOrDefault<Patient>(
            @"SELECT TOP 1 * FROM Patients
              WHERE IsActive=1 AND ((@cnic <> '' AND CNIC=@cnic) OR (@phone <> '' AND Phone=@phone))
              ORDER BY CASE WHEN CNIC=@cnic AND @cnic <> '' THEN 0 ELSE 1 END, PatientID",
            new { cnic = cnic?.Trim() ?? "", phone = phone?.Trim() ?? "" });
    }

    public Patient? GetPatientByNamePhoneOrCNIC(string? name, string? phone, string? cnic)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(cnic)) return null;
        using var conn = _session.CreateConnection();
        return conn.QueryFirstOrDefault<Patient>(
            @"SELECT TOP 1 * FROM Patients
              WHERE IsActive=1 AND (
                  (@cnic <> '' AND CNIC=@cnic) OR 
                  (@phone <> '' AND Phone=@phone) OR 
                  (@name <> '' AND Name=@name)
              )
              ORDER BY CASE WHEN CNIC=@cnic AND @cnic <> '' THEN 0 
                            WHEN Phone=@phone AND @phone <> '' THEN 1 
                            WHEN Name=@name AND @name <> '' THEN 2 
                            ELSE 3 END, PatientID",
            new { name = name?.Trim() ?? "", phone = phone?.Trim() ?? "", cnic = cnic?.Trim() ?? "" });
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
        using var tx = conn.BeginTransaction();

        var existingPatientId = conn.QueryFirstOrDefault<int?>(
            @"SELECT TOP 1 PatientID FROM Patients
              WHERE IsActive=1 AND ((@CNIC <> '' AND CNIC=@CNIC) OR (@Phone <> '' AND Phone=@Phone))
              ORDER BY CASE WHEN CNIC=@CNIC AND @CNIC <> '' THEN 0 ELSE 1 END, PatientID",
            new { CNIC = a.CNIC?.Trim() ?? "", Phone = a.Phone?.Trim() ?? "" }, tx);
        if (existingPatientId.HasValue)
        {
            a.PatientID = existingPatientId;
            conn.Execute(@"UPDATE Patients SET Name=COALESCE(NULLIF(@PatientName,''),Name), Phone=COALESCE(NULLIF(@Phone,''),Phone),
                CNIC=COALESCE(NULLIF(@CNIC,''),CNIC), Age=COALESCE(@Age,Age), Gender=COALESCE(NULLIF(@Gender,''),Gender),
                ReasonOfVisit=@Reason, PatientContext=CASE WHEN PatientContext='Pharma' THEN 'Both' ELSE PatientContext END
                WHERE PatientID=@PatientID", a, tx);
        }
        else if (!string.IsNullOrWhiteSpace(a.PatientName))
        {
            a.PatientID = conn.ExecuteScalar<int>(@"INSERT INTO Patients
                (Name,Age,Gender,Phone,CNIC,ReasonOfVisit,PatientContext,VisitStatus,LastVisitDate,IsActive)
                VALUES (@PatientName,@Age,@Gender,@Phone,@CNIC,@Reason,'Clinical','Waiting',@AppointmentDate,1);
                SELECT CONVERT(INT,SCOPE_IDENTITY());", a, tx);
        }

        // Auto-generate AppointmentNo as a sequential Token number
        if (string.IsNullOrEmpty(a.AppointmentNo))
        {
            int nextSeq = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM Appointments WHERE AppointmentDate = @date", 
                new { date = a.AppointmentDate.Date }, tx) + 1;
            a.AppointmentNo = $"Token-{nextSeq}";
        }

        var appointmentId = conn.ExecuteScalar<int>(
            @"INSERT INTO Appointments 
                (AppointmentNo, PatientID, PatientName, Phone, CNIC, Gender, Age, DoctorID, AppointmentDate, AppointmentTime, Reason, Status, Remarks, CancellationReason, CreatedAt)
              VALUES 
                (@AppointmentNo, @PatientID, @PatientName, @Phone, @CNIC, @Gender, @Age, @DoctorID, @AppointmentDate, @AppointmentTime, @Reason, @Status, @Remarks, @CancellationReason, @CreatedAt);
              SELECT CONVERT(INT,SCOPE_IDENTITY());", a, tx);
        tx.Commit();
        return appointmentId;
    }

    public void Update(Appointment a)
    {
        if (CheckConflict(a.DoctorID, a.AppointmentDate, a.AppointmentTime, a.AppointmentID))
        {
            throw new InvalidOperationException("Doctor is already booked for this date and time.");
        }

        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        conn.Execute(
            @"UPDATE Appointments SET
                PatientID = @PatientID, PatientName = @PatientName, Phone = @Phone, CNIC = @CNIC,
                Gender = @Gender, Age = @Age,
                DoctorID = @DoctorID, AppointmentDate = @AppointmentDate,
                AppointmentTime = @AppointmentTime, Reason = @Reason, Status = @Status,
                Remarks = @Remarks, CancellationReason = @CancellationReason
              WHERE AppointmentID = @AppointmentID", a, tx);
        if (a.PatientID.HasValue)
            conn.Execute(@"UPDATE Patients SET Name=COALESCE(NULLIF(@PatientName,''),Name),Phone=COALESCE(NULLIF(@Phone,''),Phone),
                CNIC=COALESCE(NULLIF(@CNIC,''),CNIC),Age=COALESCE(@Age,Age),Gender=COALESCE(NULLIF(@Gender,''),Gender),
                ReasonOfVisit=@Reason WHERE PatientID=@PatientID", a, tx);
        tx.Commit();
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
