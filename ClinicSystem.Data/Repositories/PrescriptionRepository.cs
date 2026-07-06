using ClinicSystem.Core.Models;
using Dapper;

namespace ClinicSystem.Data.Repositories;

public class PrescriptionRepository
{
    private readonly DatabaseSession _session;
    private readonly MedicineRepository _medicineRepo;

    public PrescriptionRepository(DatabaseSession session, MedicineRepository medicineRepo)
    {
        _session = session;
        _medicineRepo = medicineRepo;
    }

    public IEnumerable<Prescription> GetAll()
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Prescription>(
            @"SELECT p.*, pat.Name AS PatientName, u.FullName AS DoctorName
              FROM Prescriptions p
              JOIN Patients pat ON p.PatientID = pat.PatientID
              JOIN Users u ON p.DoctorID = u.UserID
              ORDER BY p.VisitDate DESC");
    }

    public IEnumerable<Prescription> GetByPatient(int patientId)
    {
        using var conn = _session.CreateConnection();
        return conn.Query<Prescription>(
            @"SELECT p.*, pat.Name AS PatientName, u.FullName AS DoctorName
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
            @"SELECT p.*, pat.Name AS PatientName, u.FullName AS DoctorName
              FROM Prescriptions p
              JOIN Patients pat ON p.PatientID = pat.PatientID
              JOIN Users u ON p.DoctorID = u.UserID
              WHERE p.PrescriptionID = @prescriptionId",
            new { prescriptionId });

        if (prescription == null) return null;

        prescription.Items = conn.Query<PrescriptionItem>(
            @"SELECT pi.*, m.Name AS MedicineName
              FROM PrescriptionItems pi
              JOIN Medicines m ON pi.MedicineID = m.MedicineID
              WHERE pi.PrescriptionID = @prescriptionId",
            new { prescriptionId }).ToList();

        return prescription;
    }

    /// <summary>Inserts a prescription with its items and decrements stock atomically.</summary>
    public int Insert(Prescription prescription)
    {
        using var conn = _session.CreateConnection();
        // Note: conn is already open — CreateConnection() opens it.
        using var tx = conn.BeginTransaction();
        try
        {
            var prescId = conn.ExecuteScalar<int>(
                @"INSERT INTO Prescriptions (PatientID, DoctorID, VisitDate, Diagnosis, Notes)
                  VALUES (@PatientID, @DoctorID, @VisitDate, @Diagnosis, @Notes);
                  SELECT SCOPE_IDENTITY();",
                prescription, tx);

            foreach (var item in prescription.Items)
            {
                item.PrescriptionID = prescId;
                conn.Execute(
                    @"INSERT INTO PrescriptionItems (PrescriptionID, MedicineID, Quantity, Dosage)
                      VALUES (@PrescriptionID, @MedicineID, @Quantity, @Dosage)",
                    item, tx);

                // Decrement stock
                conn.Execute(
                    "UPDATE Medicines SET Stock = Stock - @Quantity WHERE MedicineID = @MedicineID",
                    new { item.Quantity, item.MedicineID }, tx);
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
