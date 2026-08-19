using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Returns;

public partial class ReturnsViewModel : ViewModelBase, ISearchable
{
    private readonly ReturnRepository _returnRepo;
    private readonly ProductRepository _productRepo;
    private readonly PatientRepository _patientRepo;
    private readonly SupplierRepository _supplierRepo;
    private readonly SaleRepository _saleRepo;

    public ReturnsViewModel(ReturnRepository returnRepo, ProductRepository productRepo,
        PatientRepository patientRepo, SupplierRepository supplierRepo, SaleRepository saleRepo)
    {
        _returnRepo = returnRepo;
        _productRepo = productRepo;
        _patientRepo = patientRepo;
        _supplierRepo = supplierRepo;
        _saleRepo = saleRepo;
    }

    [ObservableProperty] private ObservableCollection<ProductReturn> _returns = new();
    private ObservableCollection<ProductReturn> _allReturns = new();
    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
    [ObservableProperty] private ObservableCollection<Sale> _sales = new();
    [ObservableProperty] private ProductReturn? _selectedReturn;
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private Sale? _selectedSale;
    [ObservableProperty] private string _returnType = "Patient Return";
    [ObservableProperty] private string _unitType = "Tablet";
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string SearchPlaceholder => "Search returns...";
    public List<string> ReturnTypes { get; } = new() { "Patient Return", "Supplier Return" };
    public List<string> UnitTypes { get; } = new() { "Tablet", "Box" };
    public bool IsPatientReturn => ReturnType == "Patient Return";
    public bool IsSupplierReturn => !IsPatientReturn;
    public string ProcessedBy => CurrentUser?.DisplayName ?? "Unknown";
    public int StockQuantity => SelectedProduct == null ? 0 : UnitType == "Box" ? Quantity * Math.Max(1, SelectedProduct.TabletsPerBox) : Quantity;
    public decimal RefundAmount => SelectedProduct == null ? 0 : Quantity * (IsPatientReturn
        ? UnitType == "Box" ? SelectedProduct.SellingPrice : SelectedProduct.PricePerTablet
        : UnitType == "Box" ? SelectedProduct.PurchasePrice : Math.Round(SelectedProduct.PurchasePrice / Math.Max(1, SelectedProduct.TabletsPerBox), 2));

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            var products = await Task.Run(_productRepo.GetAll);
            var patients = await Task.Run(_patientRepo.GetAll);
            var suppliers = await Task.Run(_supplierRepo.GetAll);
            var sales = await Task.Run(_saleRepo.GetAll);
            var returns = await Task.Run(_returnRepo.GetAll);
            Products = new ObservableCollection<Product>(products.Where(p => p.IsReturnable));
            Patients = new ObservableCollection<Patient>(patients);
            Suppliers = new ObservableCollection<Supplier>(suppliers);
            Sales = new ObservableCollection<Sale>(sales.Where(s => s.IsPosted));
            _allReturns = new ObservableCollection<ProductReturn>(returns);
            FilterReturns();
            StatusMessage = $"{Returns.Count} return(s) loaded.";
        }
        catch (Exception ex) { StatusMessage = $"Failed to load returns: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ProcessReturnAsync()
    {
        if (SelectedProduct == null) { StatusMessage = "Select a medicine."; return; }
        if (Quantity <= 0) { StatusMessage = "Quantity must be greater than zero."; return; }
        if (string.IsNullOrWhiteSpace(Reason)) { StatusMessage = "Enter a return reason."; return; }
        if (IsPatientReturn && SelectedPatient == null) { StatusMessage = "Select the patient returning the medicine."; return; }
        if (IsSupplierReturn && SelectedSupplier == null) { StatusMessage = "Select the supplier receiving the return."; return; }
        if (IsSupplierReturn && StockQuantity > SelectedProduct.Stock) { StatusMessage = $"Only {SelectedProduct.Stock} tablet/unit(s) are in stock."; return; }

        var ret = new ProductReturn
        {
            ReturnNo = $"RET-{DateTime.Now:yyyyMMddHHmmssfff}", ProductId = SelectedProduct.ProductID,
            BatchNo = SelectedProduct.BatchNumber ?? string.Empty, Quantity = Quantity, UnitType = UnitType,
            StockQuantity = StockQuantity, ReturnType = ReturnType, Reason = Reason.Trim(), Notes = Notes.Trim(),
            PatientId = IsPatientReturn ? SelectedPatient?.PatientID : null,
            SupplierId = IsSupplierReturn ? SelectedSupplier?.SupplierID : null,
            SaleId = IsPatientReturn ? SelectedSale?.SaleID : null, RefundAmount = RefundAmount,
            CreatedBy = CurrentUser?.UserID, CreatedAt = DateTime.Now
        };
        try
        {
            IsBusy = true;
            await Task.Run(() => _returnRepo.Insert(ret));
            LogActivity(ReturnType, $"{ret.ReturnNo}: {Quantity} {UnitType}(s) of {SelectedProduct.Name}, Rs. {ret.RefundAmount:N2}", "Returns");
            WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
            ClearForm();
            await InitializeAsync();
            StatusMessage = $"Return {ret.ReturnNo} processed. Amount: Rs. {ret.RefundAmount:N2}.";
        }
        catch (Exception ex) { StatusMessage = $"Return failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateReturnInvoiceAsync()
    {
        if (SelectedReturn == null) { StatusMessage = "Select a return first."; return; }
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        var file = await desktop.MainWindow!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Return Invoice", SuggestedFileName = $"{SelectedReturn.ReturnNo}.pdf", DefaultExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } } }
        });
        if (file == null) return;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            var r = SelectedReturn;
            Document.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(2, Unit.Centimetre);
                page.Header().Text("MEDICINE RETURN INVOICE").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Spacing(10); col.Item().Text($"Return No: {r.ReturnNo}"); col.Item().Text($"Date: {r.CreatedAt:dd MMM yyyy hh:mm tt}");
                    col.Item().Text($"Type: {r.ReturnType}"); col.Item().Text($"Medicine: {r.ProductName}");
                    col.Item().Text($"Quantity: {r.Quantity} {r.UnitType}(s) ({r.StockQuantity} stock units)");
                    col.Item().Text($"Patient / Supplier: {r.PatientName ?? r.SupplierName ?? "N/A"}");
                    col.Item().Text($"Reason: {r.Reason}"); col.Item().Text($"Refund / Credit: Rs. {r.RefundAmount:N2}").FontSize(16).Bold();
                    col.Item().Text($"Processed by: {r.CreatedByName ?? ProcessedBy}");
                });
            })).GeneratePdf(stream);
            StatusMessage = "Return invoice exported.";
        }
        catch (Exception ex) { StatusMessage = $"Invoice export failed: {ex.Message}"; }
    }

    private void ClearForm() { SelectedProduct = null; SelectedPatient = null; SelectedSupplier = null; SelectedSale = null; Quantity = 1; Reason = string.Empty; Notes = string.Empty; }
    partial void OnReturnTypeChanged(string value) { SelectedPatient = null; SelectedSupplier = null; SelectedSale = null; OnPropertyChanged(nameof(IsPatientReturn)); OnPropertyChanged(nameof(IsSupplierReturn)); OnPropertyChanged(nameof(RefundAmount)); }
    partial void OnUnitTypeChanged(string value) { OnPropertyChanged(nameof(StockQuantity)); OnPropertyChanged(nameof(RefundAmount)); }
    partial void OnQuantityChanged(int value) { OnPropertyChanged(nameof(StockQuantity)); OnPropertyChanged(nameof(RefundAmount)); }
    partial void OnSelectedProductChanged(Product? value) { SelectedSupplier = value == null ? null : Suppliers.FirstOrDefault(s => s.SupplierID == value.SupplierID); OnPropertyChanged(nameof(StockQuantity)); OnPropertyChanged(nameof(RefundAmount)); }
    partial void OnSearchTermChanged(string value) => FilterReturns();
    private void FilterReturns()
    {
        var term = SearchTerm?.Trim() ?? string.Empty;
        Returns = new ObservableCollection<ProductReturn>(string.IsNullOrEmpty(term) ? _allReturns : _allReturns.Where(r =>
            r.ReturnNo.Contains(term, StringComparison.OrdinalIgnoreCase) || (r.ProductName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (r.PatientName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) || (r.SupplierName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)));
    }
}
