using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Prescriptions;

public partial class PrescriptionViewModel : ViewModelBase
{
    private readonly PrescriptionRepository _prescRepo;
    private readonly PatientRepository _patientRepo;
    private readonly MedicineRepository _medicineRepo;

    public PrescriptionViewModel(
        PrescriptionRepository prescRepo,
        PatientRepository patientRepo,
        MedicineRepository medicineRepo)
    {
        _prescRepo = prescRepo;
        _patientRepo = patientRepo;
        _medicineRepo = medicineRepo;
    }

    // ── Patient selection ─────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPatient))]
    [NotifyPropertyChangedFor(nameof(PatientNameColor))]
    private Patient? _selectedPatient;

    public bool HasSelectedPatient => SelectedPatient != null;
    public string PatientNameColor => SelectedPatient == null ? "#666" : "White";
    [ObservableProperty] private bool _showPatientList;
    [ObservableProperty] private string _patientSearch = string.Empty;
    [ObservableProperty] private ObservableCollection<Patient> _filteredPatients = new();
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Prescription fields ───────────────────────────────────────────────
    [ObservableProperty] private DateTimeOffset _visitDate = DateTimeOffset.Now;
    [ObservableProperty] private string _diagnosis = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    // ── Prescription items ────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Medicine> _availableMedicines = new();
    [ObservableProperty] private ObservableCollection<PrescriptionItemRow> _items = new();
    [ObservableProperty] private Medicine? _selectedMedicineToAdd;
    [ObservableProperty] private string _quantityToAdd = "1";
    [ObservableProperty] private string _dosageToAdd = string.Empty;

    [ObservableProperty] private bool _isBusy;

    // ── Commands ──────────────────────────────────────────────────────────
    [RelayCommand] private void PickPatient() { ShowPatientList = true; FilterPatients(); }
    [RelayCommand] private void ClosePatientList() => ShowPatientList = false;

    [RelayCommand]
    private void SelectPatient(Patient? p)
    {
        if (p == null) return;
        SelectedPatient = p; ShowPatientList = false;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (SelectedMedicineToAdd == null) { StatusMessage = "Select a medicine."; return; }
        if (!int.TryParse(QuantityToAdd, out var qty) || qty <= 0) { StatusMessage = "Enter a valid quantity."; return; }
        if (qty > SelectedMedicineToAdd.Stock) { StatusMessage = $"Insufficient stock (available: {SelectedMedicineToAdd.Stock})."; return; }

        Items.Add(new PrescriptionItemRow
        {
            MedicineID = SelectedMedicineToAdd.MedicineID,
            MedicineName = SelectedMedicineToAdd.Name,
            Quantity = qty,
            Dosage = DosageToAdd,
            AvailableStock = SelectedMedicineToAdd.Stock
        });

        SelectedMedicineToAdd = null; QuantityToAdd = "1"; DosageToAdd = string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void RemoveItem(PrescriptionItemRow? row)
    {
        if (row != null) Items.Remove(row);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedPatient == null) { StatusMessage = "Select a patient."; return; }
        if (!Items.Any()) { StatusMessage = "Add at least one medicine."; return; }

        IsBusy = true;
        try
        {
            var prescription = new Prescription
            {
                PatientID = SelectedPatient.PatientID,
                DoctorID = CurrentUser!.UserID,
                VisitDate = VisitDate.DateTime,
                Diagnosis = Diagnosis,
                Notes = Notes,
                Items = Items.Select(i => new PrescriptionItem
                {
                    MedicineID = i.MedicineID,
                    Quantity = i.Quantity,
                    Dosage = i.Dosage
                }).ToList()
            };

            await Task.Run(() => _prescRepo.Insert(prescription));
            StatusMessage = "Prescription saved successfully.";
            Reset();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedPatient = null; VisitDate = DateTimeOffset.Now;
        Diagnosis = string.Empty; Notes = string.Empty; Items.Clear();
        StatusMessage = string.Empty;
        _ = InitializeAsync();
    }

    partial void OnPatientSearchChanged(string value) => FilterPatients();

    public async Task InitializeAsync()
    {
        try
        {
            var patients = await Task.Run(() => _patientRepo.GetAll());
            var medicines = await Task.Run(() => _medicineRepo.GetPrescribable());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Patients = new ObservableCollection<Patient>(patients);
                AvailableMedicines = new ObservableCollection<Medicine>(medicines);
                FilterPatients();
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load data: {ex.Message}");
        }
    }

    private void FilterPatients()
    {
        if (string.IsNullOrWhiteSpace(PatientSearch))
            FilteredPatients = new ObservableCollection<Patient>(Patients);
        else
        {
            var t = PatientSearch.ToLower();
            FilteredPatients = new ObservableCollection<Patient>(
                Patients.Where(p => p.Name.ToLower().Contains(t) || (p.Contact?.Contains(t) ?? false)));
        }
    }
}

/// <summary>Row in the prescription items grid.</summary>
public partial class PrescriptionItemRow : ObservableObject
{
    public int MedicineID { get; set; }
    [ObservableProperty] private string _medicineName = string.Empty;
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private string _dosage = string.Empty;
    public int AvailableStock { get; set; }
}
