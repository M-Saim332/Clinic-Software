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
    private readonly CompanyRepository _companyRepo;

    public MedicineRegistryViewModel(MedicineRepository repo, CompanyRepository companyRepo)
    {
        _repo = repo;
        _companyRepo = companyRepo;
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

    // Companies for selection ComboBox
    [ObservableProperty] private ObservableCollection<Company> _companies = new();
    [ObservableProperty] private Company? _selectedCompany;

    // Fields
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _genericName = string.Empty;
    [ObservableProperty] private string _batchNumber = string.Empty;
    [ObservableProperty] private DateTimeOffset? _expiryDate;
    [ObservableProperty] private string _purchasePrice = "0.00";
    [ObservableProperty] private string _sellingPrice = "0.00";
    [ObservableProperty] private string _stock = "0";
    [ObservableProperty] private string _minimumStockLevel = "10";

    public string ProfitPerUnitText => FormatMoney(CurrentSellingPrice - CurrentPurchasePrice);
    public string InventoryValueText => FormatMoney(CurrentStock * CurrentSellingPrice);

    private int CurrentStock => int.TryParse(Stock, out var value) ? value : 0;
    private decimal CurrentPurchasePrice => decimal.TryParse(PurchasePrice, out var value) ? value : 0;
    private decimal CurrentSellingPrice => decimal.TryParse(SellingPrice, out var value) ? value : 0;

    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Enter new medicine details.";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedMedicine == null) { StatusMessage = "Select a medicine first."; return; }
        FillFields(SelectedMedicine);
        Mode = FormMode.Edit;
        NotifyButtonStates();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedMedicine == null) { StatusMessage = "Select a medicine first."; return; }
        var ok = await Task.Run(() => _repo.Delete(SelectedMedicine.MedicineID));
        if (ok)
        {
            StatusMessage = "Medicine deleted.";
            _ = InitializeAsync();
            SelectedMedicine = null;
        }
        else
        {
            StatusMessage = "Cannot delete — medicine is referenced in sales.";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Name is required."; return; }
        if (!decimal.TryParse(PurchasePrice, out var purchase) || purchase < 0) { StatusMessage = "Enter a valid purchase price."; return; }
        if (!decimal.TryParse(SellingPrice, out var sell) || sell < 0) { StatusMessage = "Enter a valid selling price."; return; }
        if (!int.TryParse(Stock, out var stock) || stock < 0) { StatusMessage = "Enter valid stock."; return; }
        if (!int.TryParse(MinimumStockLevel, out var minStock) || minStock < 0) { StatusMessage = "Enter valid minimum stock."; return; }

        var m = BuildMedicine();
        await Task.Run(() =>
        {
            if (Mode == FormMode.Add)
            {
                _repo.Insert(m);
            }
            else
            {
                m.MedicineID = SelectedMedicine!.MedicineID;
                _repo.Update(m);
            }
        });

        StatusMessage = Mode == FormMode.Add ? "Medicine added." : "Medicine updated.";
        Mode = FormMode.View;
        NotifyButtonStates();
        _ = InitializeAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        Mode = FormMode.View;
        NotifyButtonStates();
        if (SelectedMedicine != null) FillFields(SelectedMedicine);
        StatusMessage = string.Empty;
    }

    [RelayCommand] private void Find() { ShowList = !ShowList; FilterMedicines(); }
    [RelayCommand] private void List() { ShowList = true; FilterMedicines(); }
    [RelayCommand] private void CloseList() => ShowList = false;

    [RelayCommand]
    private void SelectFromList(Medicine? m)
    {
        if (m == null) return;
        SelectedMedicine = m;
        FillFields(m);
        ShowList = false;
    }

    partial void OnSearchTermChanged(string value) => FilterMedicines();

    public async Task InitializeAsync()
    {
        var compList = await Task.Run(() => _companyRepo.GetAll());
        var list = await Task.Run(() => _repo.GetAll());

        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            Companies = new ObservableCollection<Company>(compList);
            Medicines = new ObservableCollection<Medicine>(list);
            FilterMedicines();
        });
    }

    private void FilterMedicines()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
            FilteredMedicines = new ObservableCollection<Medicine>(Medicines);
        else
        {
            var t = SearchTerm.ToLower();
            FilteredMedicines = new ObservableCollection<Medicine>(
                Medicines.Where(m =>
                    m.Name.ToLower().Contains(t) ||
                    (m.GenericName?.ToLower().Contains(t) ?? false) ||
                    (m.CompanyName?.ToLower().Contains(t) ?? false) ||
                    (m.BatchNumber?.ToLower().Contains(t) ?? false)));
        }
    }

    private void ClearFields()
    {
        Name = string.Empty;
        GenericName = string.Empty;
        BatchNumber = string.Empty;
        ExpiryDate = null;
        PurchasePrice = "0.00";
        SellingPrice = "0.00";
        Stock = "0";
        MinimumStockLevel = "10";
        SelectedCompany = null;
        NotifyCalculatedTotals();
    }

    private void FillFields(Medicine m)
    {
        Name = m.Name;
        GenericName = m.GenericName ?? string.Empty;
        BatchNumber = m.BatchNumber ?? string.Empty;
        ExpiryDate = m.ExpiryDate.HasValue ? new DateTimeOffset(m.ExpiryDate.Value, TimeSpan.Zero) : null;
        PurchasePrice = m.PurchasePrice.ToString("F2");
        SellingPrice = m.SellingPrice.ToString("F2");
        Stock = m.Stock.ToString();
        MinimumStockLevel = m.MinimumStockLevel.ToString();
        SelectedCompany = Companies.FirstOrDefault(c => c.CompanyID == m.CompanyID);
        NotifyCalculatedTotals();
    }

    private Medicine BuildMedicine() => new()
    {
        Name = Name,
        GenericName = string.IsNullOrWhiteSpace(GenericName) ? null : GenericName.Trim(),
        BatchNumber = string.IsNullOrWhiteSpace(BatchNumber) ? null : BatchNumber.Trim(),
        ExpiryDate = ExpiryDate?.Date,
        PurchasePrice = decimal.TryParse(PurchasePrice, out var bp) ? bp : 0,
        SellingPrice = decimal.TryParse(SellingPrice, out var sp) ? sp : 0,
        Stock = int.TryParse(Stock, out var s) ? s : 0,
        MinimumStockLevel = int.TryParse(MinimumStockLevel, out var ms) ? ms : 10,
        CompanyID = SelectedCompany?.CompanyID
    };

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }

    partial void OnStockChanged(string value) => NotifyCalculatedTotals();
    partial void OnPurchasePriceChanged(string value) => NotifyCalculatedTotals();
    partial void OnSellingPriceChanged(string value) => NotifyCalculatedTotals();

    private void NotifyCalculatedTotals()
    {
        OnPropertyChanged(nameof(ProfitPerUnitText));
        OnPropertyChanged(nameof(InventoryValueText));
    }

    private static string FormatMoney(decimal value) => $"Rs. {value:N2}";
}
