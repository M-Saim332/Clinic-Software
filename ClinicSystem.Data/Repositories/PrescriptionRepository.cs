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

    public IEnumerable<Prescription> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Prescription>(
            @"SELECT p.*, pat.Name AS PatientName, pat.Age AS PatientAge, pat.Gender AS PatientGender,
                     pat.Phone AS PatientPhone, u.FullName AS DoctorName
              FROM Prescriptions p
              JOIN Patients pat ON p.PatientID = pat.PatientID
              JOIN Users u ON p.DoctorID = u.UserID
              ORDER BY p.VisitDate DESC");
    }

    public IEnumerable<Prescription> GetByPatient(int patientId)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Prescription>(
            @"SELECT p.*, pat.Name AS PatientName, pat.Age AS PatientAge, pat.Gender AS PatientGender,
                     pat.Phone AS PatientPhone, u.FullName AS DoctorName
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
            @"SELECT p.*, pat.Name AS PatientName, pat.Age AS PatientAge, pat.Gender AS PatientGender,
                     pat.Phone AS PatientPhone, u.FullName AS DoctorName
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

    /// <summary>Inserts the doctor's drug selection. Dispensing and stock deduction happen in posted sales.</summary>
    public IEnumerable<Prescription> GetPharmacyHandoffs(bool includeDispensed = false)
    {
        using var conn = _session.CreateConnection();
        var rows = conn.Query<Prescription>(
            @"SELECT p.*, pat.Name AS PatientName, pat.Age AS PatientAge, pat.Gender AS PatientGender,
                     pat.Phone AS PatientPhone, u.FullName AS DoctorName
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
        conn.Execute(@"UPDATE Prescriptions SET WorkflowStatus = 'Printed', PrintedAt = COALESCE(PrintedAt, GETDATE())
                       WHERE PrescriptionID = @prescriptionId AND WorkflowStatus = 'SentToPharmacy'", new { prescriptionId });
    }

    public void MarkDispensed(int prescriptionId)
    {
        using var conn = _session.CreateConnection();
        conn.Execute(@"UPDATE Prescriptions SET WorkflowStatus = 'Dispensed', DispensedAt = GETDATE()
                       WHERE PrescriptionID = @prescriptionId AND WorkflowStatus IN ('SentToPharmacy', 'Printed')", new { prescriptionId });
    }

    public int Insert(Prescription prescription, string workflowStatus = "Draft")
    {
        using var conn = _session.CreateConnection();
        // Note: conn is already open — CreateConnection() opens it.
        using var tx = conn.BeginTransaction();
        try
        {
            var prescId = conn.ExecuteScalar<int>(
                @"INSERT INTO Prescriptions (PatientID, DoctorID, VisitDate, Diagnosis, Notes, WorkflowStatus, SentToPharmacyAt)
                  VALUES (@PatientID, @DoctorID, @VisitDate, @Diagnosis, @Notes, @WorkflowStatus,
                          CASE WHEN @WorkflowStatus = 'SentToPharmacy' THEN GETDATE() ELSE NULL END);
                  SELECT SCOPE_IDENTITY();",
                new { prescription.PatientID, prescription.DoctorID, prescription.VisitDate, prescription.Diagnosis,
                      prescription.Notes, WorkflowStatus = workflowStatus }, tx);

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
