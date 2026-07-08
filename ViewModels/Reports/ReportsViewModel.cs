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

namespace ClinicSystem.UI.ViewModels.Reports;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly PatientRepository _patientRepo;
    private readonly MedicineRepository _medicineRepo;
    private readonly PrescriptionRepository _prescriptionRepo;
    private readonly SaleRepository _saleRepo;
    private readonly PurchaseRepository _purchaseRepo;

    public ReportsViewModel(
        PatientRepository patientRepo, 
        MedicineRepository medicineRepo, 
        PrescriptionRepository prescriptionRepo,
        SaleRepository saleRepo,
        PurchaseRepository purchaseRepo)
    {
        _patientRepo = patientRepo;
        _medicineRepo = medicineRepo;
        _prescriptionRepo = prescriptionRepo;
        _saleRepo = saleRepo;
        _purchaseRepo = purchaseRepo;
    }

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private ObservableCollection<Patient> _patientList = new();
    [ObservableProperty] private ObservableCollection<Medicine> _medicineStockList = new();
    [ObservableProperty] private ObservableCollection<Medicine> _expiredMedicines = new();
    [ObservableProperty] private ObservableCollection<Medicine> _lowStockMedicines = new();
    [ObservableProperty] private ObservableCollection<Prescription> _allVisits = new();
    
    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private decimal _totalExpenses;
    [ObservableProperty] private decimal _netProfit;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [RelayCommand]
    private async Task LoadPatientListAsync()
    {
        IsBusy = true;
        PatientList = new ObservableCollection<Patient>(await Task.Run(_patientRepo.GetAll));
        StatusMessage = $"{PatientList.Count} patients.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadMedicineStockAsync()
    {
        IsBusy = true;
        MedicineStockList = new ObservableCollection<Medicine>(await Task.Run(_medicineRepo.GetAll));
        StatusMessage = $"{MedicineStockList.Count} medicines.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadExpiredLowStockAsync()
    {
        IsBusy = true;
        ExpiredMedicines = new ObservableCollection<Medicine>(await Task.Run(_medicineRepo.GetExpired));
        LowStockMedicines = new ObservableCollection<Medicine>(await Task.Run(_medicineRepo.GetLowStock));
        StatusMessage = $"{ExpiredMedicines.Count} expired, {LowStockMedicines.Count} low-stock.";
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
            1 => LoadMedicineStockAsync(),
            2 => LoadExpiredLowStockAsync(),
            3 => LoadAllVisitsAsync(),
            4 => LoadFinancialsAsync(),
            _ => Task.CompletedTask
        };
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
