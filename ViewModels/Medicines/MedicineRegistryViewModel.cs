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
    }

    [ObservableProperty] private FormMode _mode = FormMode.View;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showList;
    [ObservableProperty] private bool _showAddStockPanel;
    [ObservableProperty] private string _searchTerm = string.Empty;

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;
    public IReadOnlyList<string> StockTypeOptions { get; } = new[] { "Bought", "Sample" };

    [ObservableProperty] private ObservableCollection<Medicine> _medicines = new();
    [ObservableProperty] private ObservableCollection<Medicine> _filteredMedicines = new();
    [ObservableProperty] private Medicine? _selectedMedicine;

    // Fields
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _formula = string.Empty;
    [ObservableProperty] private string _stock = "0";
    [ObservableProperty] private string _minStock = "10";
    [ObservableProperty] private DateTimeOffset? _expiryDate;
    [ObservableProperty] private string _buyingPrice = "0.00";
    [ObservableProperty] private string _sellingPrice = "0.00";
    [ObservableProperty] private string _manufacturer = string.Empty;
    [ObservableProperty] private string _company = string.Empty;
    [ObservableProperty] private string _supplierName = string.Empty;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private DateTimeOffset? _buyingDate = DateTimeOffset.Now;
    [ObservableProperty] private string _unitsBought = "0";
    [ObservableProperty] private string _stockType = "Bought";

    [ObservableProperty] private string _addStockQuantity = "1";
    [ObservableProperty] private string _addStockBuyingPrice = "0.00";
    [ObservableProperty] private string _addStockSellingPrice = "0.00";
    [ObservableProperty] private string _addStockSupplierName = string.Empty;
    [ObservableProperty] private DateTimeOffset? _addStockBuyingDate = DateTimeOffset.Now;
    [ObservableProperty] private string _addStockType = "Bought";

    public string ProfitPerUnitText => FormatMoney(CurrentSellingPrice - CurrentBuyingPrice);
    public string InventoryCostText => FormatMoney(CurrentUnitsBought * CurrentBuyingPrice);
    public string InventoryValueText => FormatMoney(CurrentStock * CurrentSellingPrice);
    public string EstimatedRevenueText => FormatMoney(Math.Max(CurrentUnitsBought - CurrentStock, 0) * CurrentSellingPrice);
    public string EstimatedProfitText => FormatMoney(Math.Max(CurrentUnitsBought - CurrentStock, 0) * (CurrentSellingPrice - CurrentBuyingPrice));

    private int CurrentStock => int.TryParse(Stock, out var value) ? value : 0;
    private int CurrentUnitsBought => int.TryParse(UnitsBought, out var value) ? value : 0;
    private decimal CurrentBuyingPrice => decimal.TryParse(BuyingPrice, out var value) ? value : 0;
    private decimal CurrentSellingPrice => decimal.TryParse(SellingPrice, out var value) ? value : 0;

    [RelayCommand]
    private void New() { ClearFields(); ShowAddStockPanel = false; Mode = FormMode.Add; NotifyButtonStates(); StatusMessage = "Enter new medicine purchase details."; }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedMedicine == null) { StatusMessage = "Select a medicine first."; return; }
        FillFields(SelectedMedicine); ShowAddStockPanel = false; Mode = FormMode.Edit; NotifyButtonStates();
    }

    [RelayCommand]
    private void OpenAddStock()
    {
        if (SelectedMedicine == null) { StatusMessage = "Select a medicine first."; return; }
        FillFields(SelectedMedicine);
        AddStockQuantity = "1";
        AddStockBuyingPrice = SelectedMedicine.BuyingPrice.ToString("F2");
        AddStockSellingPrice = SelectedMedicine.SellingPrice.ToString("F2");
        AddStockSupplierName = SelectedMedicine.SupplierName ?? string.Empty;
        AddStockBuyingDate = DateTimeOffset.Now;
        AddStockType = SelectedMedicine.StockType;
        ShowAddStockPanel = true;
        StatusMessage = "Enter stock purchase details.";
    }

    [RelayCommand]
    private async Task SaveAddedStockAsync()
    {
        if (SelectedMedicine == null) { StatusMessage = "Select a medicine first."; return; }
        if (!int.TryParse(AddStockQuantity, out var qty) || qty <= 0) { StatusMessage = "Enter a valid stock quantity."; return; }
        if (!decimal.TryParse(AddStockBuyingPrice, out var buy) || buy < 0) { StatusMessage = "Enter a valid buying price."; return; }
        if (!decimal.TryParse(AddStockSellingPrice, out var sell) || sell < 0) { StatusMessage = "Enter a valid selling price."; return; }

        await Task.Run(() => _repo.AddStock(
            SelectedMedicine.MedicineID,
            qty,
            buy,
            sell,
            string.IsNullOrWhiteSpace(AddStockSupplierName) ? null : AddStockSupplierName.Trim(),
            AddStockBuyingDate?.Date,
            AddStockType));

        var selectedId = SelectedMedicine.MedicineID;
        StatusMessage = "Stock added.";
        ShowAddStockPanel = false;
        await InitializeAsync();
        SelectedMedicine = Medicines.FirstOrDefault(m => m.MedicineID == selectedId);
        if (SelectedMedicine != null) FillFields(SelectedMedicine);
    }

    [RelayCommand]
    private void CancelAddStock()
    {
        ShowAddStockPanel = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedMedicine == null) { StatusMessage = "Select a medicine first."; return; }
        var ok = await Task.Run(() => _repo.Delete(SelectedMedicine.MedicineID));
        if (ok) { StatusMessage = "Medicine deleted."; _ = InitializeAsync(); SelectedMedicine = null; }
        else StatusMessage = "Cannot delete — medicine used in prescriptions.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Name is required."; return; }
        if (!decimal.TryParse(BuyingPrice, out var buy) || buy < 0) { StatusMessage = "Enter a valid buying price."; return; }
        if (!decimal.TryParse(SellingPrice, out var sell) || sell < 0) { StatusMessage = "Enter a valid selling price."; return; }
        if (!int.TryParse(Stock, out var stock) || stock < 0) { StatusMessage = "Enter valid stock units."; return; }
        if (!int.TryParse(UnitsBought, out var bought) || bought < stock) { UnitsBought = Stock; }
        var m = BuildMedicine();
        await Task.Run(() =>
        {
            if (Mode == FormMode.Add) _repo.Insert(m);
            else { m.MedicineID = SelectedMedicine!.MedicineID; _repo.Update(m); }
        });
        StatusMessage = Mode == FormMode.Add ? "Medicine added." : "Medicine updated.";
        Mode = FormMode.View; NotifyButtonStates(); _ = InitializeAsync();
    }

    [RelayCommand]
    private void Cancel() { Mode = FormMode.View; ShowAddStockPanel = false; NotifyButtonStates(); if (SelectedMedicine != null) FillFields(SelectedMedicine); StatusMessage = string.Empty; }

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

    public async Task InitializeAsync()
    {
        var list = await Task.Run(() => _repo.GetAll());
        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
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
                    (m.Formula?.ToLower().Contains(t) ?? false) ||
                    (m.Manufacturer?.ToLower().Contains(t) ?? false) ||
                    (m.Company?.ToLower().Contains(t) ?? false) ||
                    (m.SupplierName?.ToLower().Contains(t) ?? false)));
        }
    }

    private void ClearFields()
    {
        Name = string.Empty; Formula = string.Empty; Stock = "0"; MinStock = "10";
        ExpiryDate = null; BuyingPrice = "0.00"; SellingPrice = "0.00";
        Manufacturer = string.Empty; Company = string.Empty; SupplierName = string.Empty; Category = string.Empty;
        BuyingDate = DateTimeOffset.Now; UnitsBought = "0"; StockType = "Bought";
        NotifyCalculatedTotals();
    }

    private void FillFields(Medicine m)
    {
        Name = m.Name; Formula = m.Formula ?? string.Empty; Stock = m.Stock.ToString(); MinStock = m.MinStock.ToString();
        ExpiryDate = m.ExpiryDate.HasValue ? new DateTimeOffset(m.ExpiryDate.Value, TimeSpan.Zero) : null;
        BuyingPrice = m.BuyingPrice.ToString("F2"); SellingPrice = m.SellingPrice.ToString("F2");
        Manufacturer = m.Manufacturer ?? string.Empty; Company = m.Company ?? string.Empty;
        SupplierName = m.SupplierName ?? string.Empty; Category = m.Category ?? string.Empty;
        BuyingDate = m.BuyingDate.HasValue ? new DateTimeOffset(m.BuyingDate.Value, TimeSpan.Zero) : null;
        UnitsBought = Math.Max(m.UnitsBought, m.Stock).ToString();
        StockType = string.IsNullOrWhiteSpace(m.StockType) ? "Bought" : m.StockType;
        NotifyCalculatedTotals();
    }

    private Medicine BuildMedicine() => new()
    {
        Name = Name,
        Formula = string.IsNullOrWhiteSpace(Formula) ? null : Formula.Trim(),
        Stock = int.TryParse(Stock, out var s) ? s : 0,
        MinStock = int.TryParse(MinStock, out var ms) ? ms : 10,
        ExpiryDate = ExpiryDate?.Date,
        BuyingPrice = decimal.TryParse(BuyingPrice, out var bp) ? bp : 0,
        SellingPrice = decimal.TryParse(SellingPrice, out var sp) ? sp : 0,
        Manufacturer = string.IsNullOrWhiteSpace(Manufacturer) ? null : Manufacturer.Trim(),
        Company = string.IsNullOrWhiteSpace(Company) ? null : Company.Trim(),
        SupplierName = string.IsNullOrWhiteSpace(SupplierName) ? null : SupplierName.Trim(),
        Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
        BuyingDate = BuyingDate?.Date,
        UnitsBought = int.TryParse(UnitsBought, out var ub) ? ub : 0,
        StockType = string.IsNullOrWhiteSpace(StockType) ? "Bought" : StockType
    };

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }

    partial void OnStockChanged(string value) => NotifyCalculatedTotals();
    partial void OnUnitsBoughtChanged(string value) => NotifyCalculatedTotals();
    partial void OnBuyingPriceChanged(string value) => NotifyCalculatedTotals();
    partial void OnSellingPriceChanged(string value) => NotifyCalculatedTotals();

    private void NotifyCalculatedTotals()
    {
        OnPropertyChanged(nameof(ProfitPerUnitText));
        OnPropertyChanged(nameof(InventoryCostText));
        OnPropertyChanged(nameof(InventoryValueText));
        OnPropertyChanged(nameof(EstimatedRevenueText));
        OnPropertyChanged(nameof(EstimatedProfitText));
    }

    private static string FormatMoney(decimal value) => $"PKR {value:N2}";
}
