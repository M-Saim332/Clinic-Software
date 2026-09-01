using Avalonia.Controls.ApplicationLifetimes;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;
using ClinicSystem.Core.Enums;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;

namespace ClinicSystem.UI.ViewModels.Returns;

public partial class ProcessReturnViewModel : ViewModelBase
{
    private readonly ReturnRepository _returnRepo;
    private readonly ProductRepository _productRepo;
    private readonly PatientRepository _patientRepo;
    private readonly SupplierRepository _supplierRepo;
    private readonly SaleRepository _saleRepo;

    public ProcessReturnViewModel(ReturnRepository returnRepo, ProductRepository productRepo,
        PatientRepository patientRepo, SupplierRepository supplierRepo, SaleRepository saleRepo)
    {
        _returnRepo = returnRepo;
        _productRepo = productRepo;
        _patientRepo = patientRepo;
        _supplierRepo = supplierRepo;
        _saleRepo = saleRepo;

        ReturnRows.Add(new ReturnRow(this));
    }

    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
    [ObservableProperty] private ObservableCollection<Sale> _sales = new();
    [ObservableProperty] private ObservableCollection<ReturnRow> _returnRows = new();
    [ObservableProperty] private ObservableCollection<ReturnItem> _returnItems = new();

    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private Sale? _selectedSale;

    [ObservableProperty] private string _returnType = "Patient Return";
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDraftInvoice))]
    [NotifyPropertyChangedFor(nameof(IsCheckingInvoice))]
    [NotifyPropertyChangedFor(nameof(IsPostedInvoice))]
    private InvoiceState _invoiceState = InvoiceState.Draft;

    public bool IsDraftInvoice => InvoiceState == InvoiceState.Draft;
    public bool IsCheckingInvoice => InvoiceState == InvoiceState.Checking;
    public bool IsPostedInvoice => InvoiceState == InvoiceState.Posted;

    public List<string> ReturnTypes { get; } = new() { "Patient Return", "Supplier Return" };
    public bool IsPatientReturn => ReturnType == "Patient Return";
    public bool IsSupplierReturn => !IsPatientReturn;
    public string ProcessedBy => CurrentUser?.DisplayName ?? "Unknown";

    public string GlobalUnitLabel => IsPatientReturn ? "Pieces" : "Packs";

    public int StockQuantity => ReturnItems.Sum(i => i.Quantity);
    public decimal RefundAmount => ReturnItems.Sum(i => i.RefundAmount);

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            var products  = await Task.Run(_productRepo.GetAll) ?? Enumerable.Empty<Product>();
            var patients  = await Task.Run(() => _patientRepo.GetAll());
            var suppliers = await Task.Run(_supplierRepo.GetAll);
            var sales     = await Task.Run(_saleRepo.GetAll);

            Products  = new ObservableCollection<Product>(products.Where(p => p != null && p.IsReturnable));
            Patients  = new ObservableCollection<Patient>(patients);
            Suppliers = new ObservableCollection<Supplier>(suppliers);
            Sales     = new ObservableCollection<Sale>(sales.Where(s => s.IsPosted));
            
            StatusMessage = "Process return module loaded.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RETURNS INIT ERROR] {ex.Message}{Environment.NewLine}{ex.StackTrace}");

            // Preserve a renderable form if the database/schema is temporarily
            // unavailable.  A failed data load must never collapse the Returns UI.
            Products = new ObservableCollection<Product>();
            Patients = new ObservableCollection<Patient>();
            Suppliers = new ObservableCollection<Supplier>();
            Sales = new ObservableCollection<Sale>();
            StatusMessage = $"Returns data could not be loaded: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddRow()
    {
        if (!IsDraftInvoice) return;
        ReturnRows.Add(new ReturnRow(this));
    }

    [RelayCommand]
    private void RemoveRow(ReturnRow? row)
    {
        if (row == null || !IsDraftInvoice) return;
        if (ReturnRows.Count <= 1) { StatusMessage = "At least one row is required."; return; }
        ReturnRows.Remove(row);
    }

    [RelayCommand]
    private void AddReturnItem()
    {
        if (!IsDraftInvoice) return;
        var filledRows = ReturnRows.Where(r => r.SelectedProduct != null).ToList();
        if (filledRows.Count == 0) { StatusMessage = "Select at least one medicine."; return; }

        var errors = new List<string>();
        foreach (var row in filledRows)
        {
            var err = row.ValidationError;
            if (!string.IsNullOrEmpty(err))
                errors.Add($"{row.SelectedProduct?.Name}: {err}");
        }
        if (errors.Count > 0) { StatusMessage = string.Join(" | ", errors); return; }

        var ids = filledRows.Select(r => r.SelectedProduct!.ProductID).ToList();
        if (ids.Distinct().Count() != ids.Count) { StatusMessage = "Duplicate medicines in rows — each medicine can appear only once."; return; }

        var existingIds = ReturnItems.Select(i => i.ProductId).ToHashSet();
        var newDups = filledRows.Where(r => existingIds.Contains(r.SelectedProduct!.ProductID)).ToList();
        if (newDups.Count > 0) { StatusMessage = $"{newDups[0].SelectedProduct!.Name} is already in the invoice."; return; }

        foreach (var row in filledRows)
        {
            var product = row.SelectedProduct!;
            var piecesQty = row.PiecesQuantity;
            var unitLabel = IsPatientReturn ? "Pieces" : "Packs";

            ReturnItems.Add(new ReturnItem
            {
                ProductId       = product.ProductID,
                ProductName     = product.Name,
                ProductType     = product.Type,
                StockID         = row.SelectedBatch?.StockID,
                EnteredQuantity = row.EnteredQuantity,
                UnitType        = unitLabel,
                Quantity        = piecesQty,
                PiecesPerPack   = Math.Max(1, product.PiecesPerUnit),
                UnitPrice       = IsPatientReturn
                    ? Math.Round((row.SelectedBatch?.MRP > 0 ? row.SelectedBatch.MRP : product.SellingPrice) / Math.Max(1, product.PiecesPerUnit), 2, MidpointRounding.AwayFromZero)
                    : (row.SelectedBatch?.PurchasePrice > 0 ? row.SelectedBatch.PurchasePrice : product.Rate),
                Reason          = string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim(),
                RefundAmount    = row.RefundAmount,
            });
        }

        ReturnRows.Clear();
        ReturnRows.Add(new ReturnRow(this));
        OnPropertyChanged(nameof(StockQuantity));
        OnPropertyChanged(nameof(RefundAmount));
        StatusMessage = $"{ReturnItems.Count} item(s) ready for invoice check.";
    }

    [RelayCommand]
    private void RemoveReturnItem(ReturnItem? item)
    {
        if (item != null && IsDraftInvoice)
        {
            ReturnItems.Remove(item);
            OnPropertyChanged(nameof(StockQuantity));
            OnPropertyChanged(nameof(RefundAmount));
        }
    }

    [RelayCommand]
    private void CheckInvoice()
    {
        if (IsBusy || !IsDraftInvoice) return;
        if (ReturnRows.Any(r => r.SelectedProduct != null))
            AddReturnItem();

        if (ReturnItems.Count == 0) { StatusMessage = "Add at least one medicine."; return; }

        // This is deliberately an in-memory state transition. No Return, ReturnItem,
        // or ProductStock record is created or updated until PostReturn is invoked.
        InvoiceState = InvoiceState.Checking;
        StatusMessage = "Return invoice ready for review. Post when confirmed.";
    }

    [RelayCommand]
    private async Task PostReturnAsync()
    {
        if (IsBusy || !IsCheckingInvoice) return;
        if (ReturnItems.Count == 0) { StatusMessage = "Add at least one medicine."; return; }

        if (IsPatientReturn && SelectedPatient == null)  { StatusMessage = "Select the patient returning the medicine."; return; }
        if (IsSupplierReturn && SelectedSupplier == null) { StatusMessage = "Select the supplier receiving the return."; return; }

        if (IsSupplierReturn)
        {
            foreach (var item in ReturnItems)
            {
                var prod = Products.FirstOrDefault(p => p.ProductID == item.ProductId);
                if (prod != null && item.Quantity > prod.TotalStock)
                {
                    StatusMessage = $"{item.ProductName}: return qty ({item.Quantity} pcs) exceeds stock ({prod.TotalStock} pcs).";
                    return;
                }
            }
        }

        var unitType = IsPatientReturn ? "Pieces" : "Packs";
        var ret = new ProductReturn
        {
            ReturnNo     = $"RET-{DateTime.Now:yyyyMMddHHmmssfff}",
            ProductId    = ReturnItems[0].ProductId,
            Quantity     = ReturnItems.Sum(i => i.Quantity),
            EnteredQuantity = ReturnItems.Sum(i => i.EnteredQuantity),
            UnitType     = unitType,
            StockQuantity = ReturnItems.Sum(i => i.Quantity),
            ReturnType   = ReturnType,
            Reason       = Reason.Trim(),
            Notes        = Notes.Trim(),
            PatientId    = IsPatientReturn  ? SelectedPatient?.PatientID  : null,
            SupplierId   = IsSupplierReturn ? SelectedSupplier?.SupplierID : null,
            SaleId       = IsPatientReturn  ? SelectedSale?.SaleID         : null,
            RefundAmount = ReturnItems.Sum(i => i.RefundAmount),
            CreatedBy    = CurrentUser?.UserID,
            CreatedAt    = DateTime.Now,
            Items        = ReturnItems.ToList(),
            IsPosted     = true,
        };

        try
        {
            IsBusy = true;
            await Task.Run(() => _returnRepo.Insert(ret));
            InvoiceState = InvoiceState.Posted;
            LogActivity(ReturnType, $"{ret.ReturnNo}: {ret.Items.Count} item(s), Rs. {ret.RefundAmount:N2}", "Returns");
            WeakReferenceMessenger.Default.Send(new InventoryChangedMessage());
            WeakReferenceMessenger.Default.Send(new RefundCompletedMessage());
            // Keep the posted invoice visible for review. The operator explicitly
            // starts a clean form through NewReturn, which calls ClearForm().
            StatusMessage = $"Return {ret.ReturnNo} processed. Amount: Rs. {ret.RefundAmount:N2}.";
        }
        catch (Exception ex) { StatusMessage = $"Return failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void ClearForm()
    {
        SelectedPatient  = null;
        SelectedSupplier = null;
        SelectedSale     = null;
        Reason           = string.Empty;
        Notes            = string.Empty;
        ReturnItems.Clear();
        ReturnRows.Clear();
        ReturnRows.Add(new ReturnRow(this));
        OnPropertyChanged(nameof(StockQuantity));
        OnPropertyChanged(nameof(RefundAmount));
    }

    [RelayCommand]
    private void EditDraft()
    {
        if (IsCheckingInvoice)
        {
            InvoiceState = InvoiceState.Draft;
            StatusMessage = "Draft restored. Update the medicines or quantities, then check it again.";
        }
    }

    [RelayCommand] private void NewReturn()    { ClearForm(); InvoiceState = InvoiceState.Draft; StatusMessage = "New return draft."; }

    partial void OnReturnTypeChanged(string value)
    {
        SelectedPatient  = null;
        SelectedSupplier = null;
        SelectedSale     = null;
        OnPropertyChanged(nameof(IsPatientReturn));
        OnPropertyChanged(nameof(IsSupplierReturn));
        OnPropertyChanged(nameof(GlobalUnitLabel));
        OnPropertyChanged(nameof(RefundAmount));
        foreach (var row in ReturnRows) row.RefreshReturnType();
    }

    partial void OnReturnItemsChanged(ObservableCollection<ReturnItem> value)
    {
        OnPropertyChanged(nameof(StockQuantity));
        OnPropertyChanged(nameof(RefundAmount));
    }
}

