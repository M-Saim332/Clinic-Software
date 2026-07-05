using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Medicines;

public enum FormMode { View, Add, Edit }

public partial class MedicineRegistryViewModel : ViewModelBase
{
    private readonly MedicineRepository _repo;

    public MedicineRegistryViewModel(MedicineRepository repo)
    {
        _repo = repo;
        LoadMedicines();
    }

    [ObservableProperty] private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showList;
    [ObservableProperty] private string _searchTerm = string.Empty;

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;

    [ObservableProperty] private ObservableCollection<Medicine> _medicines = new();
    [ObservableProperty] private ObservableCollection<Medicine> _filteredMedicines = new();
    [ObservableProperty] private Medicine? _selectedMedicine;

    // Fields
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _stock = "0";
    [ObservableProperty] private string _minStock = "10";
    [ObservableProperty] private DateTimeOffset? _expiryDate;
    [ObservableProperty] private string _price = "0.00";
    [ObservableProperty] private string _manufacturer = string.Empty;
    [ObservableProperty] private string _category = string.Empty;

    [RelayCommand]
    private void New() { ClearFields(); Mode = FormMode.Add; NotifyButtonStates(); StatusMessage = "Enter new medicine details."; }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedMedicine == null) { StatusMessage = "Select a medicine first."; return; }
        FillFields(SelectedMedicine); Mode = FormMode.Edit; NotifyButtonStates();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedMedicine == null) { StatusMessage = "Select a medicine first."; return; }
        var ok = await Task.Run(() => _repo.Delete(SelectedMedicine.MedicineID));
        if (ok) { StatusMessage = "Medicine deleted."; LoadMedicines(); SelectedMedicine = null; }
        else StatusMessage = "Cannot delete — medicine used in prescriptions.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Name is required."; return; }
        var m = BuildMedicine();
        await Task.Run(() =>
        {
            if (Mode == FormMode.Add) _repo.Insert(m);
            else { m.MedicineID = SelectedMedicine!.MedicineID; _repo.Update(m); }
        });
        StatusMessage = Mode == FormMode.Add ? "Medicine added." : "Medicine updated.";
        Mode = FormMode.View; NotifyButtonStates(); LoadMedicines();
    }

    [RelayCommand]
    private void Cancel() { Mode = FormMode.View; NotifyButtonStates(); if (SelectedMedicine != null) FillFields(SelectedMedicine); StatusMessage = string.Empty; }

    [RelayCommand] private void Find() { ShowList = !ShowList; FilterMedicines(); }
    [RelayCommand] private void List() { ShowList = true; FilterMedicines(); }
    [RelayCommand] private void CloseList() => ShowList = false;

    [RelayCommand]
    private void SelectFromList(Medicine? m)
    {
        if (m == null) return;
        SelectedMedicine = m; FillFields(m); ShowList = false;
    }

    partial void OnSearchTermChanged(string value) => FilterMedicines();

    private void LoadMedicines()
    {
        var list = _repo.GetAll();
        Medicines = new ObservableCollection<Medicine>(list);
        FilterMedicines();
    }

    private void FilterMedicines()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
            FilteredMedicines = new ObservableCollection<Medicine>(Medicines);
        else
        {
            var t = SearchTerm.ToLower();
            FilteredMedicines = new ObservableCollection<Medicine>(
                Medicines.Where(m => m.Name.ToLower().Contains(t) || (m.Manufacturer?.ToLower().Contains(t) ?? false)));
        }
    }

    private void ClearFields()
    {
        Name = string.Empty; Stock = "0"; MinStock = "10";
        ExpiryDate = null; Price = "0.00"; Manufacturer = string.Empty; Category = string.Empty;
    }

    private void FillFields(Medicine m)
    {
        Name = m.Name; Stock = m.Stock.ToString(); MinStock = m.MinStock.ToString();
        ExpiryDate = m.ExpiryDate.HasValue ? new DateTimeOffset(m.ExpiryDate.Value, TimeSpan.Zero) : null;
        Price = m.Price.ToString("F2"); Manufacturer = m.Manufacturer ?? string.Empty;
        Category = m.Category ?? string.Empty;
    }

    private Medicine BuildMedicine() => new()
    {
        Name = Name,
        Stock = int.TryParse(Stock, out var s) ? s : 0,
        MinStock = int.TryParse(MinStock, out var ms) ? ms : 10,
        ExpiryDate = ExpiryDate?.Date,
        Price = decimal.TryParse(Price, out var p) ? p : 0,
        Manufacturer = Manufacturer, Category = Category
    };

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }
}
