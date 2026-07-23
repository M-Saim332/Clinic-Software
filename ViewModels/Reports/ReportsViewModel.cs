using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.IO;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;

namespace ClinicSystem.UI.ViewModels.Reports;

public partial class ReportsViewModel : ViewModelBase, ISearchable
{
    private readonly PatientRepository _patientRepo;
    private readonly ProductRepository _productRepo;
    private readonly PrescriptionRepository _prescriptionRepo;
    private readonly SaleRepository _saleRepo;
    private readonly PurchaseRepository _purchaseRepo;

    public ReportsViewModel(
        PatientRepository patientRepo, 
        ProductRepository productRepo, 
        PrescriptionRepository prescriptionRepo,
        SaleRepository saleRepo,
        PurchaseRepository purchaseRepo)
    {
        _patientRepo = patientRepo;
        _productRepo = productRepo;
        _prescriptionRepo = prescriptionRepo;
        _saleRepo = saleRepo;
        _purchaseRepo = purchaseRepo;
    }

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private ObservableCollection<Patient> _patientList = new();
    [ObservableProperty] private ObservableCollection<Product> _productStockList = new();
    [ObservableProperty] private ObservableCollection<Product> _expiredProducts = new();
    [ObservableProperty] private ObservableCollection<Product> _lowStockProducts = new();
    [ObservableProperty] private ObservableCollection<Prescription> _allVisits = new();
    
    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private decimal _totalExpenses;
    [ObservableProperty] private decimal _netProfit;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private string _searchTerm = string.Empty;
    public string SearchPlaceholder => "Search Reports...";

    partial void OnSearchTermChanged(string value) 
    {
        // Add filtering logic here if needed for report tables
    }

    [RelayCommand]
    private async Task LoadPatientListAsync()
    {
        IsBusy = true;
        PatientList = new ObservableCollection<Patient>(await Task.Run(_patientRepo.GetAll));
        StatusMessage = $"{PatientList.Count} patients.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadProductStockAsync()
    {
        IsBusy = true;
        ProductStockList = new ObservableCollection<Product>(await Task.Run(_productRepo.GetAll));
        StatusMessage = $"{ProductStockList.Count} products.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadExpiredLowStockAsync()
    {
        IsBusy = true;
        ExpiredProducts = new ObservableCollection<Product>(await Task.Run(_productRepo.GetExpired));
        LowStockProducts = new ObservableCollection<Product>(await Task.Run(_productRepo.GetLowStock));
        StatusMessage = $"{ExpiredProducts.Count} expired, {LowStockProducts.Count} low-stock.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadAllVisitsAsync()
    {
        IsBusy = true;
        AllVisits = new ObservableCollection<Prescription>(await Task.Run(_prescriptionRepo.GetAll));
        StatusMessage = $"{AllVisits.Count} visits.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadFinancialsAsync()
    {
        IsBusy = true;
        var sales = await Task.Run(_saleRepo.GetAll);
        var purchases = await Task.Run(_purchaseRepo.GetAll);

        TotalRevenue = sales.Where(s => s.IsPosted).Sum(s => s.GrandTotal);
        TotalExpenses = purchases.Sum(p => p.TotalAmount);
        NetProfit = TotalRevenue - TotalExpenses;

        StatusMessage = "Financials loaded.";
        IsBusy = false;
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        _ = value switch
        {
            0 => LoadPatientListAsync(),
            1 => LoadProductStockAsync(),
            2 => LoadExpiredLowStockAsync(),
            3 => LoadAllVisitsAsync(),
            4 => LoadFinancialsAsync(),
            _ => Task.CompletedTask
        };
    }

    // ── Per-Tab Export: Patient List ───────────────────────────────────────
    [RelayCommand]
    private async Task ExportPatientListToPdfAsync()
    {
        if (PatientList.Count == 0) { StatusMessage = "Load patient data first."; return; }
        var file = await PickSaveFileAsync("PatientList", "pdf");
        if (file == null) return;
        IsBusy = true; StatusMessage = "Exporting PDF...";
        try
        {
            using var stream = new MemoryStream();
            Document.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(2, Unit.Centimetre);
                page.Header().Text("Patient List Report").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(cols => { cols.ConstantColumn(50); cols.RelativeColumn(3); cols.ConstantColumn(60); cols.ConstantColumn(80); cols.RelativeColumn(2); cols.RelativeColumn(2); });
                    table.Header(h => { h.Cell().Text("ID").SemiBold(); h.Cell().Text("Name").SemiBold(); h.Cell().Text("Age").SemiBold(); h.Cell().Text("Gender").SemiBold(); h.Cell().Text("Phone").SemiBold(); h.Cell().Text("Address").SemiBold(); h.Cell().ColumnSpan(6).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Black); });
                    foreach (var p in PatientList)
                    {
                        table.Cell().Text($"{p.PatientID}"); table.Cell().Text(p.Name); table.Cell().Text($"{p.Age}");
                        table.Cell().Text(p.Gender ?? ""); table.Cell().Text(p.Contact ?? ""); table.Cell().Text(p.Address ?? "");
                    }
                });
                page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages(); });
            })).GeneratePdf(stream);
            stream.Position = 0;
            await using var fs = await file.OpenWriteAsync();
            await stream.CopyToAsync(fs);
            StatusMessage = $"PDF exported ({PatientList.Count} patients).";
        }
        catch (Exception ex) { StatusMessage = $"Export failed: {ex.Message}"; }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task ExportPatientListToExcelAsync()
    {
        if (PatientList.Count == 0) { StatusMessage = "Load patient data first."; return; }
        var file = await PickSaveFileAsync("PatientList", "xlsx");
        if (file == null) return;
        IsBusy = true; StatusMessage = "Exporting Excel...";
        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Patients");
            ws.Cell(1, 1).Value = "ID"; ws.Cell(1, 2).Value = "Name"; ws.Cell(1, 3).Value = "Age";
            ws.Cell(1, 4).Value = "Gender"; ws.Cell(1, 5).Value = "Phone"; ws.Cell(1, 6).Value = "Address";
            ws.Row(1).Style.Font.Bold = true;
            int row = 2;
            foreach (var p in PatientList)
            { ws.Cell(row, 1).Value = p.PatientID; ws.Cell(row, 2).Value = p.Name; ws.Cell(row, 3).Value = p.Age; ws.Cell(row, 4).Value = p.Gender ?? ""; ws.Cell(row, 5).Value = p.Contact ?? ""; ws.Cell(row, 6).Value = p.Address ?? ""; row++; }
            ws.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            wb.SaveAs(stream); stream.Position = 0;
            await using var fs = await file.OpenWriteAsync();
            await stream.CopyToAsync(fs);
            StatusMessage = $"Excel exported ({PatientList.Count} patients).";
        }
        catch (Exception ex) { StatusMessage = $"Export failed: {ex.Message}"; }
        IsBusy = false;
    }

    // ── Per-Tab Export: Product Stock ───────────────────────────────────────
    [RelayCommand]
    private async Task ExportProductStockToPdfAsync()
    {
        if (ProductStockList.Count == 0) { StatusMessage = "Load stock data first."; return; }
        var file = await PickSaveFileAsync("ProductStock", "pdf");
        if (file == null) return;
        IsBusy = true; StatusMessage = "Exporting PDF...";
        try
        {
            using var stream = new MemoryStream();
            Document.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(2, Unit.Centimetre);
                page.Header().Text("Product Stock Report").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.ConstantColumn(80); cols.ConstantColumn(80); cols.ConstantColumn(90); cols.RelativeColumn(2); });
                    table.Header(h => { h.Cell().Text("Product").SemiBold(); h.Cell().Text("Stock").SemiBold(); h.Cell().Text("Min").SemiBold(); h.Cell().Text("Expiry").SemiBold(); h.Cell().Text("Manufacturer").SemiBold(); h.Cell().ColumnSpan(5).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Black); });
                    foreach (var p in ProductStockList)
                    { table.Cell().Text(p.Name); table.Cell().Text($"{p.Stock}"); table.Cell().Text($"{p.MinStock}"); table.Cell().Text(p.ExpiryDate.HasValue ? p.ExpiryDate.Value.ToString("dd MMM yyyy") : ""); table.Cell().Text(p.Manufacturer ?? ""); }
                });
                page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages(); });
            })).GeneratePdf(stream);
            stream.Position = 0;
            await using var fs = await file.OpenWriteAsync();
            await stream.CopyToAsync(fs);
            StatusMessage = $"PDF exported ({ProductStockList.Count} products).";
        }
        catch (Exception ex) { StatusMessage = $"Export failed: {ex.Message}"; }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task ExportProductStockToExcelAsync()
    {
        if (ProductStockList.Count == 0) { StatusMessage = "Load stock data first."; return; }
        var file = await PickSaveFileAsync("ProductStock", "xlsx");
        if (file == null) return;
        IsBusy = true; StatusMessage = "Exporting Excel...";
        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("ProductStock");
            ws.Cell(1, 1).Value = "Product"; ws.Cell(1, 2).Value = "Stock"; ws.Cell(1, 3).Value = "Min Stock";
            ws.Cell(1, 4).Value = "Status"; ws.Cell(1, 5).Value = "Expiry"; ws.Cell(1, 6).Value = "Price"; ws.Cell(1, 7).Value = "Manufacturer";
            ws.Row(1).Style.Font.Bold = true;
            int row = 2;
            foreach (var p in ProductStockList)
            { ws.Cell(row, 1).Value = p.Name; ws.Cell(row, 2).Value = p.Stock; ws.Cell(row, 3).Value = p.MinStock; ws.Cell(row, 4).Value = p.StockStatus; ws.Cell(row, 5).Value = p.ExpiryDate.HasValue ? p.ExpiryDate.Value.ToString("dd MMM yyyy") : ""; ws.Cell(row, 6).Value = (double)p.SellingPrice; ws.Cell(row, 7).Value = p.Manufacturer ?? ""; row++; }
            ws.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            wb.SaveAs(stream); stream.Position = 0;
            await using var fs = await file.OpenWriteAsync();
            await stream.CopyToAsync(fs);
            StatusMessage = $"Excel exported ({ProductStockList.Count} products).";
        }
        catch (Exception ex) { StatusMessage = $"Export failed: {ex.Message}"; }
        IsBusy = false;
    }

    // ── Per-Tab Export: Financials ──────────────────────────────────────────
    [RelayCommand]
    private async Task ExportFinancialsToExcelAsync()
    {
        var file = await PickSaveFileAsync("Financials", "xlsx");
        if (file == null) return;
        IsBusy = true; StatusMessage = "Exporting Excel...";
        try
        {
            var sales = await Task.Run(_saleRepo.GetAll);
            var purchases = await Task.Run(_purchaseRepo.GetAll);
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Financials");
            ws.Cell(1, 1).Value = "Type"; ws.Cell(1, 2).Value = "Date"; ws.Cell(1, 3).Value = "Reference"; ws.Cell(1, 4).Value = "Amount";
            ws.Row(1).Style.Font.Bold = true;
            int row = 2;
            foreach (var s in sales.Where(s => s.IsPosted))
            { ws.Cell(row, 1).Value = "Revenue"; ws.Cell(row, 2).Value = s.SaleDate.ToString("yyyy-MM-dd"); ws.Cell(row, 3).Value = s.InvoiceNumber; ws.Cell(row, 4).Value = (double)s.GrandTotal; row++; }
            foreach (var p in purchases)
            { ws.Cell(row, 1).Value = "Expense"; ws.Cell(row, 2).Value = p.PurchaseDate.ToString("yyyy-MM-dd"); ws.Cell(row, 3).Value = p.InvoiceNumber; ws.Cell(row, 4).Value = (double)p.TotalAmount; row++; }
            row += 2;
            ws.Cell(row, 3).Value = "Total Revenue:"; ws.Cell(row, 4).Value = (double)TotalRevenue;
            row++; ws.Cell(row, 3).Value = "Total Expenses:"; ws.Cell(row, 4).Value = (double)TotalExpenses;
            row++; ws.Cell(row, 3).Value = "Net Profit:"; ws.Cell(row, 4).Value = (double)NetProfit;
            ws.Row(row).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            wb.SaveAs(stream); stream.Position = 0;
            await using var fs = await file.OpenWriteAsync();
            await stream.CopyToAsync(fs);
            StatusMessage = "Financials exported to Excel.";
        }
        catch (Exception ex) { StatusMessage = $"Export failed: {ex.Message}"; }
        IsBusy = false;
    }

    // ── Shared: File Picker Helper ─────────────────────────────────────────
    private static async Task<IStorageFile?> PickSaveFileAsync(string prefix, string ext)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return null;
        var storage = desktop.MainWindow?.StorageProvider;
        if (storage == null) return null;
        var fileType = ext == "pdf"
            ? new FilePickerFileType("PDF File") { Patterns = new[] { "*.pdf" } }
            : new FilePickerFileType("Excel File") { Patterns = new[] { "*.xlsx" } };
        return await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Export {prefix}",
            SuggestedFileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}",
            DefaultExtension = ext,
            FileTypeChoices = new[] { fileType }
        });
    }

    [RelayCommand]
    private async Task DownloadMonthlyReportAsync()
    {
        await ExportReportAsync(TimeSpan.FromDays(30), "MonthlyReport");
    }

    [RelayCommand]
    private async Task DownloadWeeklyReportAsync()
    {
        await ExportReportAsync(TimeSpan.FromDays(7), "WeeklyReport");
    }

    private async Task ExportReportAsync(TimeSpan timeSpan, string prefix)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storage = desktop.MainWindow?.StorageProvider;
            if (storage == null) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Report",
                SuggestedFileName = $"{prefix}_{DateTime.Now:yyyyMMdd}.pdf",
                DefaultExtension = "pdf",
                FileTypeChoices = new[] { new FilePickerFileType("PDF File") { Patterns = new[] { "*.pdf" } } }
            });

            if (file != null)
            {
                IsBusy = true;
                StatusMessage = "Exporting report...";
                try
                {
                    var since = DateTime.Now - timeSpan;
                    var sales = await Task.Run(() => _saleRepo.GetAll().Where(s => s.IsPosted && s.SaleDate >= since).ToList());
                    var purchases = await Task.Run(() => _purchaseRepo.GetAll().Where(p => p.PurchaseDate >= since).ToList());

                    var totalRevenue = sales.Sum(s => s.GrandTotal);
                    var totalExpenses = purchases.Sum(p => p.TotalAmount);
                    var netProfit = totalRevenue - totalExpenses;

                    // Generate PDF in memory first to avoid holding file lock too long
                    using var stream = new MemoryStream();
                    
                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(2, Unit.Centimetre);
                            page.PageColor(Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(11));

                            page.Header().Element(compose => 
                            {
                                compose.Row(row =>
                                {
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("Clinic Financial Report").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                                        col.Item().Text($"Period: {since:MMM dd, yyyy} to {DateTime.Now:MMM dd, yyyy}").FontSize(14).FontColor(Colors.Grey.Medium);
                                    });
                                    row.ConstantItem(100).AlignRight().Text($"Date: {DateTime.Now:d}");
                                });
                            });

                            page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                            {
                                col.Item().PaddingBottom(5).Text("Transactions").SemiBold().FontSize(16);
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2); // Type
                                        columns.RelativeColumn(2); // Date
                                        columns.RelativeColumn(3); // Reference
                                        columns.RelativeColumn(2); // Amount
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Type").SemiBold();
                                        header.Cell().Text("Date").SemiBold();
                                        header.Cell().Text("Reference").SemiBold();
                                        header.Cell().AlignRight().Text("Amount").SemiBold();
                                        header.Cell().ColumnSpan(4).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                    });

                                    foreach (var s in sales)
                                    {
                                        table.Cell().Text("Revenue").FontColor(Colors.Green.Darken2);
                                        table.Cell().Text($"{s.SaleDate:yyyy-MM-dd}");
                                        table.Cell().Text(s.InvoiceNumber);
                                        table.Cell().AlignRight().Text($"{s.GrandTotal:C2}");
                                    }

                                    foreach (var p in purchases)
                                    {
                                        table.Cell().Text("Expense").FontColor(Colors.Red.Darken2);
                                        table.Cell().Text($"{p.PurchaseDate:yyyy-MM-dd}");
                                        table.Cell().Text(p.InvoiceNumber);
                                        table.Cell().AlignRight().Text($"{p.TotalAmount:C2}");
                                    }
                                });

                                col.Item().PaddingVertical(20).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                                col.Item().Row(row =>
                                {
                                    row.RelativeItem();
                                    row.ConstantItem(250).Column(innerCol =>
                                    {
                                        innerCol.Item().Row(r => { r.RelativeItem().Text("Total Revenue:"); r.ConstantItem(100).AlignRight().Text($"{totalRevenue:C2}"); });
                                        innerCol.Item().Row(r => { r.RelativeItem().Text("Total Expenses:"); r.ConstantItem(100).AlignRight().Text($"{totalExpenses:C2}"); });
                                        innerCol.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Black);
                                        innerCol.Item().Row(r => { r.RelativeItem().Text("Net Profit:").SemiBold(); r.ConstantItem(100).AlignRight().Text($"{netProfit:C2}").SemiBold(); });
                                    });
                                });
                            });

                            page.Footer().AlignCenter().Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                        });
                    }).GeneratePdf(stream);

                    // Write to file
                    stream.Position = 0;
                    await using var fileStream = await file.OpenWriteAsync();
                    await stream.CopyToAsync(fileStream);

                    StatusMessage = "Report exported to PDF successfully!";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
                IsBusy = false;
            }
        }
    }
}
