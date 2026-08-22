using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class PrescriptionRepository
{
    private readonly DatabaseSession _session;
    private readonly ProductRepository _productRepo;

    public PrescriptionRepository(DatabaseSession session, ProductRepository productRepo)
    {
        _session = session;
        _productRepo = productRepo;
    }

    // ── Core SELECT fragment (reused everywhere) ─────────────────────────────
    private const string SelectCols = @"
        p.PrescriptionID, p.PatientID, p.DoctorID, p.AppointmentID, p.PharmacistID,
        p.VisitDate, p.Diagnosis, p.Notes, p.LabTests,
        p.CreatedAt, p.WorkflowStatus, p.SentToPharmacyAt, p.PrintedAt, p.DispensedAt,
        pat.Name AS PatientName, pat.Age AS PatientAge, pat.Gender AS PatientGender,
        pat.Phone AS PatientPhone, u.FullName AS DoctorName";

    public IEnumerable<Prescription> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Prescription>(
            $@"SELECT {SelectCols}
               FROM Prescriptions p
               JOIN Patients pat ON p.PatientID = pat.PatientID
               JOIN Users u ON p.DoctorID = u.UserID
               ORDER BY p.VisitDate DESC");
    }

    public IEnumerable<Prescription> GetByPatient(int patientId)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Prescription>(
            $@"SELECT {SelectCols}
               FROM Prescriptions p
               JOIN Patients pat ON p.PatientID = pat.PatientID
               JOIN Users u ON p.DoctorID = u.UserID
               WHERE p.PatientID = @patientId
               ORDER BY p.VisitDate DESC",
            new { patientId });
    }

    public Prescription? GetByIdWithItems(int prescriptionId)
    {
        using var conn = _session.CreateConnection();
        var prescription = conn.QuerySingleOrDefault<Prescription>(
            $@"SELECT {SelectCols}
               FROM Prescriptions p
               JOIN Patients pat ON p.PatientID = pat.PatientID
               JOIN Users u ON p.DoctorID = u.UserID
               WHERE p.PrescriptionID = @prescriptionId",
            new { prescriptionId });

        if (prescription == null) return null;

        prescription.Items = conn.Query<PrescriptionItem>(
            @"SELECT pi.*, m.Name AS ProductName
              FROM PrescriptionItems pi
              JOIN Products m ON pi.ProductID = m.ProductID
              WHERE pi.PrescriptionID = @prescriptionId",
            new { prescriptionId }).ToList();

        return prescription;
    }

    /// <summary>
    /// Returns the active (non-dispensed) prescription for a specific appointment, if one exists.
    /// Used to prevent duplicate handoffs when "Send to Pharmacist" is clicked multiple times.
    /// </summary>
    public Prescription? GetActivePrescriptionForAppointment(int appointmentId)
    {
        using var conn = _session.CreateConnection();
        return conn.QueryFirstOrDefault<Prescription>(
            $@"SELECT {SelectCols}
               FROM Prescriptions p
               JOIN Patients pat ON p.PatientID = pat.PatientID
               JOIN Users u ON p.DoctorID = u.UserID
               WHERE p.AppointmentID = @appointmentId
                 AND p.WorkflowStatus IN ('SentToPharmacy', 'Printed')
               ORDER BY p.CreatedAt DESC",
            new { appointmentId });
    }

    /// <summary>
    /// Returns the active (non-dispensed) prescription for a patient today.
    /// Fallback duplicate detection when no AppointmentID context is available.
    /// </summary>
    public Prescription? GetActivePrescriptionForPatientToday(int patientId)
    {
        using var conn = _session.CreateConnection();
        return conn.QueryFirstOrDefault<Prescription>(
            $@"SELECT {SelectCols}
               FROM Prescriptions p
               JOIN Patients pat ON p.PatientID = pat.PatientID
               JOIN Users u ON p.DoctorID = u.UserID
               WHERE p.PatientID = @patientId
                 AND p.WorkflowStatus IN ('SentToPharmacy', 'Printed')
                 AND CAST(p.VisitDate AS DATE) = CAST(GETDATE() AS DATE)
               ORDER BY p.CreatedAt DESC",
            new { patientId });
    }

    public IEnumerable<Prescription> GetPharmacyHandoffs(bool includeDispensed = false)
    {
        using var conn = _session.CreateConnection();
        var rows = conn.Query<Prescription>(
            $@"SELECT {SelectCols}
               FROM Prescriptions p
               JOIN Patients pat ON p.PatientID = pat.PatientID
               JOIN Users u ON p.DoctorID = u.UserID
               WHERE p.WorkflowStatus IN ('SentToPharmacy', 'Printed')
                  OR (@includeDispensed = 1 AND p.WorkflowStatus = 'Dispensed' AND p.DispensedAt >= DATEADD(day, -1, GETDATE()))
               ORDER BY CASE WHEN p.WorkflowStatus = 'SentToPharmacy' THEN 0 WHEN p.WorkflowStatus = 'Printed' THEN 1 ELSE 2 END,
                        p.SentToPharmacyAt DESC",
            new { includeDispensed });

        foreach (var row in rows)
            row.Items = conn.Query<PrescriptionItem>(
                @"SELECT pi.*, m.Name AS ProductName FROM PrescriptionItems pi
                  JOIN Products m ON pi.ProductID = m.ProductID
                  WHERE pi.PrescriptionID = @id", new { id = row.PrescriptionID }).ToList();
        return rows;
    }

    public void MarkPrinted(int prescriptionId)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(@"UPDATE Prescriptions
                       SET WorkflowStatus = 'Printed', PrintedAt = COALESCE(PrintedAt, GETDATE())
                       WHERE PrescriptionID = @prescriptionId AND WorkflowStatus = 'SentToPharmacy'",
            new { prescriptionId });
    }

    public void MarkDispensed(int prescriptionId, int pharmacistId)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(@"UPDATE Prescriptions
                       SET WorkflowStatus = 'Dispensed', DispensedAt = GETDATE(), PharmacistID = @pharmacistId
                       WHERE PrescriptionID = @prescriptionId AND WorkflowStatus IN ('SentToPharmacy', 'Printed')",
            new { prescriptionId, pharmacistId });
    }

    public int Insert(Prescription prescription, string workflowStatus = "Draft")
    {
        using var conn = _session.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var prescId = conn.ExecuteScalar<int>(
                @"INSERT INTO Prescriptions
                    (PatientID, DoctorID, AppointmentID, VisitDate, Diagnosis, Notes, LabTests, WorkflowStatus, SentToPharmacyAt)
                  VALUES
                    (@PatientID, @DoctorID, @AppointmentID, @VisitDate, @Diagnosis, @Notes, @LabTests, @WorkflowStatus,
                     CASE WHEN @WorkflowStatus = 'SentToPharmacy' THEN GETDATE() ELSE NULL END);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    prescription.PatientID,
                    prescription.DoctorID,
                    prescription.AppointmentID,
                    prescription.VisitDate,
                    prescription.Diagnosis,
                    prescription.Notes,
                    prescription.LabTests,
                    WorkflowStatus = workflowStatus
                }, tx);

            foreach (var item in prescription.Items)
            {
                item.PrescriptionID = prescId;
                conn.Execute(
                    @"INSERT INTO PrescriptionItems (PrescriptionID, ProductID, Quantity, Dosage)
                      VALUES (@PrescriptionID, @ProductID, @Quantity, @Dosage)",
                    item, tx);
            }

            tx.Commit();
            return prescId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Delete(int prescriptionId)
    {
        using var conn = _session.CreateConnection();
        // Items are cascade-deleted
        conn.Execute("DELETE FROM Prescriptions WHERE PrescriptionID = @prescriptionId",
            new { prescriptionId });
    }
}
