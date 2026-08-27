using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using ClinicSystem.UI.Services;
using ClinicSystem.UI.Messages;
using CommunityToolkit.Mvvm.Messaging;

namespace ClinicSystem.UI.ViewModels.Prescriptions;

public partial class PrescriptionViewModel : ViewModelBase, ISearchable
{
    private readonly PrescriptionRepository _prescRepo;
    private readonly PatientRepository _patientRepo;
    private readonly ProductRepository _productRepo;

    public PrescriptionViewModel(
        PrescriptionRepository prescRepo,
        PatientRepository patientRepo,
        ProductRepository productRepo)
    {
        _prescRepo = prescRepo;
        _patientRepo = patientRepo;
        _productRepo = productRepo;
    }

    // ── Appointment context (set externally when opened from an appointment) ─
    /// <summary>
    /// Appointment linked to this consultation. Set before the view is shown.
    /// When set, the prescription will be linked to this appointment, and
    /// duplicate handoff detection will use this ID.
    /// </summary>
    public int? CurrentAppointmentID { get; set; }

    // Display the linked appointment badge when context is set
    public bool HasAppointmentContext => CurrentAppointmentID.HasValue;
    public string AppointmentContextLabel => CurrentAppointmentID.HasValue
        ? $"APT-{CurrentAppointmentID}"
        : string.Empty;

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

    [ObservableProperty] private string _searchTerm = string.Empty;
    public string SearchPlaceholder => "Search Prescriptions...";

    partial void OnSearchTermChanged(string value)
    {
        // No prescription list to filter in this view yet.
    }

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private Patient? _selectedFilteredPatient;

    // ── Prescription fields ───────────────────────────────────────────────
    [ObservableProperty] private DateTimeOffset _visitDate = DateTimeOffset.Now;
    /// <summary>Lab tests ordered for this visit (optional).</summary>
    [ObservableProperty] private string _labTests = string.Empty;

    // ── Doctor Inventory modal ─────────────────────────────────────────────
    [ObservableProperty] private bool _showInventoryModal;
    [ObservableProperty] private string _inventorySearch = string.Empty;
    [ObservableProperty] private ObservableCollection<Product> _filteredInventory = new();

    // ── Prescription items ────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
    [ObservableProperty] private ObservableCollection<PrescriptionItemRow> _items = new();
    [ObservableProperty] private Product? _selectedProductToAdd;
    [ObservableProperty] private string _quantityToAdd = "1";
    [ObservableProperty] private string _dosageToAdd = string.Empty;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _showPrescriptionPreview;

    public string PrescriptionNumberDisplay => CurrentAppointmentID.HasValue
        ? $"APT-{CurrentAppointmentID}"
        : "New Prescription";
    public string PreviewPatientName => SelectedPatient?.Name ?? "No patient selected";
    public string PreviewPatientMeta => SelectedPatient == null
        ? string.Empty
        : $"Age: {SelectedPatient.Age}   Gender: {SelectedPatient.Gender ?? "-"}   Phone: {SelectedPatient.Phone ?? SelectedPatient.Contact ?? "-"}";
    public string PreviewDoctorName => CurrentUser?.FullName ?? CurrentUser?.DisplayName ?? "Doctor";

    // ── Commands ──────────────────────────────────────────────────────────
    [RelayCommand] private void PickPatient() { ShowPatientList = true; FilterPatients(); }
    [RelayCommand] private void ClosePatientList() => ShowPatientList = false;

    // Inventory modal commands
    [RelayCommand] private void OpenInventoryModal() { InventorySearch = string.Empty; FilterInventory(); ShowInventoryModal = true; }
    [RelayCommand] private void CloseInventoryModal() => ShowInventoryModal = false;

    [RelayCommand]
    private void PreviewPrescription()
    {
        if (SelectedPatient == null) { StatusIsError = true; StatusMessage = "Select a patient."; return; }
        if (!Items.Any()) { StatusIsError = true; StatusMessage = "Add at least one medicine."; return; }
        StatusIsError = false;
        ShowPrescriptionPreview = true;
    }

    [RelayCommand] private void ClosePrescriptionPreview() => ShowPrescriptionPreview = false;

    [RelayCommand]
    private void AddFromInventory(Product? product)
    {
        if (product == null) return;
        if (Items.Any(i => i.ProductID == product.ProductID)) { StatusMessage = "Already added."; return; }
        Items.Add(new PrescriptionItemRow
        {
            ProductID = product.ProductID,
            ProductName = product.Name,
            Quantity = 1,
            Dosage = string.Empty,
            AvailableStock = product.Stock
        });
        StatusMessage = $"{product.Name} added.";
    }

    [RelayCommand]
    private void SelectPatient(Patient? p)
    {
        if (p == null) return;
        SelectedPatient = p; ShowPatientList = false;
        NotifyPreviewMetadata();
    }

    [RelayCommand]
    private void AddItem()
    {
        if (SelectedProductToAdd == null) { StatusMessage = "Select a product."; return; }
        if (!int.TryParse(QuantityToAdd, out var qty) || qty <= 0) { StatusMessage = "Enter a valid quantity."; return; }
        if (qty > SelectedProductToAdd.Stock) { StatusMessage = $"Insufficient stock (available: {SelectedProductToAdd.Stock})."; return; }

        Items.Add(new PrescriptionItemRow
        {
            ProductID = SelectedProductToAdd.ProductID,
            ProductName = SelectedProductToAdd.Name,
            Quantity = qty,
            Dosage = DosageToAdd,
            AvailableStock = SelectedProductToAdd.Stock
        });

        SelectedProductToAdd = null; QuantityToAdd = "1"; DosageToAdd = string.Empty;
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
        if (!Items.Any()) { StatusMessage = "Add at least one product."; return; }

        IsBusy = true;
        try
        {
            var prescription = BuildPrescription();
            await Task.Run(() => _prescRepo.Insert(prescription));
            StatusIsError = false;
            StatusMessage = "Prescription saved successfully.";
            Reset();
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SendToPharmacistAsync()
    {
        if (SelectedPatient == null) { StatusIsError = true; StatusMessage = "Select a patient."; return; }
        if (!Items.Any()) { StatusIsError = true; StatusMessage = "Add at least one medicine."; return; }

        IsBusy = true;
        try
        {
            // ── Duplicate handoff prevention ──────────────────────────────
            // First try appointment-level deduplication (precise), then fall back to patient+today.
            Prescription? existingHandoff = null;
            if (CurrentAppointmentID.HasValue)
            {
                existingHandoff = await Task.Run(() =>
                    _prescRepo.GetActivePrescriptionForAppointment(CurrentAppointmentID.Value));
            }
            if (existingHandoff == null)
            {
                existingHandoff = await Task.Run(() =>
                    _prescRepo.GetActivePrescriptionForPatientToday(SelectedPatient.PatientID));
            }

            if (existingHandoff != null)
            {
                StatusIsError = false;
                StatusMessage = existingHandoff.WorkflowStatus == "Printed"
                    ? $"{SelectedPatient.Name} is already with the pharmacist (checked & printed). No duplicate created."
                    : $"{SelectedPatient.Name} has already been sent to the pharmacist. The pharmacist will attend shortly.";
                return;
            }

            // ── Insert new handoff ────────────────────────────────────────
            var prescription = BuildPrescription();
            await Task.Run(() => _prescRepo.Insert(prescription, "SentToPharmacy"));
            StatusIsError = false;
            StatusMessage = $"{SelectedPatient.Name} was sent to the pharmacist. Reception has also been notified.";
            LogActivity("Sent to Pharmacist", $"Prescription sent for {SelectedPatient.Name}", "Prescriptions");
            WeakReferenceMessenger.Default.Send(new PrescriptionHandoffChangedMessage());
            Items.Clear();
        }
        catch (Exception ex) { StatusIsError = true; StatusMessage = $"Unable to send: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PrintPatientPrescriptionAsync()
    {
        if (SelectedPatient == null) { StatusIsError = true; StatusMessage = "Select a patient."; return; }
        if (!Items.Any()) { StatusIsError = true; StatusMessage = "Add at least one medicine."; return; }
        var prescription = BuildPrescription();
        if (await PrescriptionPrintService.ExportAsync(prescription))
        {
            StatusIsError = false;
            StatusMessage = "Prescription PDF is ready to print.";
        }
    }

    [RelayCommand]
    private void EmailPrescription()
    {
        StatusIsError = false;
        StatusMessage = "Email prescription action is available from the document preview.";
    }

    private Prescription BuildPrescription() => new()
    {
        PatientID = SelectedPatient!.PatientID,
        PatientName = SelectedPatient.Name,
        PatientAge = SelectedPatient.Age,
        PatientGender = SelectedPatient.Gender,
        PatientPhone = SelectedPatient.Phone ?? SelectedPatient.Contact,
        DoctorID = CurrentUser!.UserID,
        DoctorName = CurrentUser.FullName,
        AppointmentID = CurrentAppointmentID,
        VisitDate = VisitDate.DateTime,
        LabTests = string.IsNullOrWhiteSpace(LabTests) ? null : LabTests,
        Items = Items.Select(i => new PrescriptionItem
        {
            ProductID = i.ProductID, ProductName = i.ProductName, Quantity = i.Quantity, Dosage = i.Dosage
        }).ToList()
    };

    [RelayCommand]
    private void Reset()
    {
        SelectedPatient = null; VisitDate = DateTimeOffset.Now;
        LabTests = string.Empty; Items.Clear();
        StatusMessage = string.Empty;
        CurrentAppointmentID = null;
        OnPropertyChanged(nameof(HasAppointmentContext));
        OnPropertyChanged(nameof(AppointmentContextLabel));
        NotifyPreviewMetadata();
        _ = InitializeAsync();
    }

    private void NotifyPreviewMetadata()
    {
        OnPropertyChanged(nameof(PrescriptionNumberDisplay));
        OnPropertyChanged(nameof(PreviewPatientName));
        OnPropertyChanged(nameof(PreviewPatientMeta));
        OnPropertyChanged(nameof(PreviewDoctorName));
    }

    partial void OnPatientSearchChanged(string value) => FilterPatients();
    partial void OnSelectedPatientChanged(Patient? value) => NotifyPreviewMetadata();

    public async Task InitializeAsync()
    {
        try
        {
            var patients = await Task.Run(() => _patientRepo.GetAll());
            var products = await Task.Run(() => _productRepo.GetPrescribable());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Patients = new ObservableCollection<Patient>(patients);
                AvailableProducts = new ObservableCollection<Product>(products);
                FilterPatients();
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load data: {ex.Message}");
        }
    }

    partial void OnInventorySearchChanged(string value) => FilterInventory();

    private void FilterInventory()
    {
        if (string.IsNullOrWhiteSpace(InventorySearch))
            FilteredInventory = new ObservableCollection<Product>(AvailableProducts);
        else
        {
            var t = InventorySearch.ToLower();
            FilteredInventory = new ObservableCollection<Product>(
                AvailableProducts.Where(p => p.Name.ToLower().Contains(t)));
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
    public int ProductID { get; set; }
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private string _dosage = string.Empty;
    public int AvailableStock { get; set; }
    /// <summary>Per-unit selling price captured from the product catalog at prescription time.</summary>
    public decimal UnitPrice { get; set; }
    public string BatchExpiryDisplay => "N/A";
    public string RateDisplay => UnitPrice > 0 ? $"Rs. {UnitPrice:N2} / piece" : "Pharma billing";
    public string TaxDiscountDisplay => "N/A";
    public string TotalDisplay => UnitPrice > 0 ? $"Rs. {UnitPrice * Quantity:N2}" : "Pending sale";
}
