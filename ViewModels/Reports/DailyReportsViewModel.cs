using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClinicSystem.Data.Repositories;
using ClinicSystem.Core.Models;
using ClinicSystem.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ClinicSystem.UI.ViewModels.Reports;

public sealed record DailyReportLine(string Label, string Value);
public sealed record DailyReportTable(string[] Headers, IReadOnlyList<string[]> Rows, IReadOnlyList<DailyReportLine> Totals);

public partial class DailyReportsViewModel : ViewModelBase
{
    private readonly SaleRepository _sales;
    private readonly PurchaseRepository _purchases;
    private readonly ReturnRepository _returns;
    private readonly AppointmentRepository _appointments;

    [ObservableProperty] private DateTimeOffset _reportDate = new(DateTime.Today);
    [ObservableProperty] private decimal _salesTotal;
    [ObservableProperty] private decimal _purchaseTotal;
    [ObservableProperty] private decimal _patientReturns;
    [ObservableProperty] private decimal _supplierCredits;
    [ObservableProperty] private decimal _netProfitLoss;
    [ObservableProperty] private int _salesCount;
    [ObservableProperty] private int _purchaseCount;
    [ObservableProperty] private int _patientCount;
    [ObservableProperty] private int _returnCount;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Ready to generate today's reports.";

    private List<Sale> _daySales = new();
    private List<Purchase> _dayPurchases = new();
    private List<ProductReturn> _dayReturns = new();
    private List<Appointment> _dayAppointments = new();
    private List<DailyInvoiceProfitRow> _invoiceProfits = new();

    public string ReportDateText => ReportDate.LocalDateTime.ToString("dddd, dd MMMM yyyy");

    public DailyReportsViewModel(SaleRepository sales, PurchaseRepository purchases,
        ReturnRepository returns, AppointmentRepository appointments)
    {
        _sales = sales;
        _purchases = purchases;
        _returns = returns;
        _appointments = appointments;
    }

    partial void OnReportDateChanged(DateTimeOffset value)
    {
        OnPropertyChanged(nameof(ReportDateText));
        _ = LoadAsync();
    }

    public Task InitializeAsync()
    {
        ReportDate = new DateTimeOffset(DateTime.Today);
        return LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var from = ReportDate.LocalDateTime.Date;
            var to = from.AddDays(1);
            var salesTask = Task.Run(() => _sales.GetByRange(from, to).Where(x => x.IsPosted).ToList());
            var purchasesTask = Task.Run(() => _purchases.GetAll().Where(x => x.IsPosted && x.PurchaseDate >= from && x.PurchaseDate < to).ToList());
            var returnsTask = Task.Run(() => _returns.GetByRange(from, to).Where(x => x.IsPosted).ToList());
            var appointmentsTask = Task.Run(() => _appointments.GetAll().Where(x => x.AppointmentDate.Date == from).ToList());
            var profitsTask = Task.Run(() => _sales.GetInvoiceProfitByRange(from, to).ToList());
            await Task.WhenAll(salesTask, purchasesTask, returnsTask, appointmentsTask, profitsTask);

            _daySales = await salesTask;
            _dayPurchases = await purchasesTask;
            _dayReturns = await returnsTask;
            _dayAppointments = await appointmentsTask;
            _invoiceProfits = await profitsTask;
            SalesCount = _daySales.Count;
            SalesTotal = _daySales.Sum(x => x.GrandTotal);
            PurchaseCount = _dayPurchases.Count;
            PurchaseTotal = _dayPurchases.Sum(x => x.TotalAmount);
            ReturnCount = _dayReturns.Count;
            PatientReturns = _dayReturns.Where(x => x.ReturnType == "Patient Return").Sum(x => x.RefundAmount);
            SupplierCredits = _dayReturns.Where(x => x.ReturnType == "Supplier Return").Sum(x => x.RefundAmount);
            PatientCount = _dayAppointments.Select(x => x.PatientID).Where(x => x.HasValue).Distinct().Count();
            NetProfitLoss = SalesTotal - PurchaseTotal - PatientReturns + SupplierCredits;
            StatusMessage = $"Daily figures loaded for {from:dd MMM yyyy}.";
        }
        catch (Exception ex)
        {
            SalesCount = PurchaseCount = PatientCount = ReturnCount = 0;
            SalesTotal = PurchaseTotal = PatientReturns = SupplierCredits = NetProfitLoss = 0;
            StatusMessage = $"Could not load daily reports: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private Task PrintSalesSummaryAsync() => ExportAsync("Daily Sales Summary", new DailyReportTable(
        new[] { "Invoice", "Time", "Patient", "Payment", "Processed by", "Amount" },
        _daySales.Select(x => new[] { x.InvoiceNumber, x.SaleDate.ToString("hh:mm tt"), x.PatientName ?? "Walk-in", x.PaymentMethod ?? "—", x.ReceptionistName ?? "—", Money(x.GrandTotal) }).ToList(),
        new[] { new DailyReportLine("TOTAL POSTED INVOICES", SalesCount.ToString()), new DailyReportLine("TOTAL SALES VALUE", Money(SalesTotal)) }));

    [RelayCommand] private Task PrintPurchaseSummaryAsync() => ExportAsync("Daily Purchase Summary", new DailyReportTable(
        new[] { "Invoice", "Time", "Supplier", "Recorded by", "Status", "Amount" },
        _dayPurchases.Select(x => new[] { x.InvoiceNumber, x.PurchaseDate.ToString("hh:mm tt"), x.SupplierName ?? "Unlisted", x.CreatedByName ?? "—", "Posted", Money(x.TotalAmount) }).ToList(),
        new[] { new DailyReportLine("TOTAL POSTED INVOICES", PurchaseCount.ToString()), new DailyReportLine("TOTAL PURCHASE VALUE", Money(PurchaseTotal)) }));

    [RelayCommand] private Task PrintPatientsSummaryAsync() => ExportAsync("Daily Patients Summary", new DailyReportTable(
        new[] { "Appointment", "Time", "Patient", "Phone", "Doctor", "Status" },
        _dayAppointments.Select(x => new[] { x.AppointmentNo, DateTime.Today.Add(x.AppointmentTime).ToString("hh:mm tt"), x.PatientName ?? "—", x.Phone ?? "—", x.DoctorName ?? "—", x.Status }).ToList(),
        new[] { new DailyReportLine("TOTAL APPOINTMENT RECORDS", _dayAppointments.Count.ToString()), new DailyReportLine("UNIQUE PATIENTS", PatientCount.ToString()) }));

    [RelayCommand] private Task PrintReturnsSummaryAsync() => ExportAsync("Daily Returns Summary", new DailyReportTable(
        new[] { "Return", "Time", "Type", "Medicine", "Party", "Refund" },
        _dayReturns.Select(x => new[] { x.ReturnNo, x.CreatedAt.ToString("hh:mm tt"), x.ReturnType, x.ProductSummary, x.CounterpartyName, Money(x.RefundAmount) }).ToList(),
        new[] { new DailyReportLine("TOTAL POSTED RETURNS", ReturnCount.ToString()), new DailyReportLine("PATIENT REFUNDS", Money(PatientReturns)), new DailyReportLine("SUPPLIER CREDITS", Money(SupplierCredits)) }));

    [RelayCommand] private Task PrintProfitLossSummaryAsync() => ExportAsync("Daily Profit & Loss Summary", new DailyReportTable(
        new[] { "Invoice", "Time", "Patient", "Revenue", "Cost", "Invoice profit" },
        _invoiceProfits.Select(x => new[] { x.InvoiceNumber, x.SaleDate.ToString("hh:mm tt"), x.PatientName ?? "Walk-in", Money(x.Revenue), Money(x.CostOfGoods), Money(x.Profit) }).ToList(),
        new[] { new DailyReportLine("POSTED SALES INVOICES", SalesCount.ToString()), new DailyReportLine("TOTAL SALES", Money(SalesTotal)), new DailyReportLine("TOTAL COST OF GOODS", Money(_invoiceProfits.Sum(x => x.CostOfGoods))), new DailyReportLine("GROSS INVOICE PROFIT", Money(_invoiceProfits.Sum(x => x.Profit))), new DailyReportLine("DAILY PURCHASES", Money(PurchaseTotal)), new DailyReportLine("PATIENT RETURNS", $"({Money(PatientReturns)})"), new DailyReportLine("SUPPLIER CREDITS", Money(SupplierCredits)), new DailyReportLine("NET PROFIT / LOSS", Money(NetProfitLoss)) }));

    private static string Money(decimal value) => $"Rs. {value:N2}";

    private async Task ExportAsync(string title, DailyReportTable report)
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var storage = lifetime?.MainWindow?.StorageProvider;
        if (storage is null) { StatusMessage = "Unable to open the print/save dialog."; return; }

        var date = ReportDate.LocalDateTime.Date;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Print {title}",
            SuggestedFileName = $"{title.Replace(' ', '_').Replace("&", "and")}_{date:yyyy-MM-dd}.pdf",
            FileTypeChoices = new[] { new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } } }
        });
        if (file is null) return;

        IsBusy = true;
        try
        {
            using var stream = new MemoryStream();
            var clinicName = ClinicBrandingService.ClinicName;
            Document.Create(document => document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));
                page.Header().Column(header =>
                {
                    header.Item().Text(clinicName).FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                    header.Item().Text(title).FontSize(14).SemiBold();
                    header.Item().Text($"Report date: {date:dddd, dd MMMM yyyy} | Printed: {DateTime.Now:dd MMM yyyy, hh:mm tt}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                page.Content().PaddingVertical(24).Column(content =>
                {
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { foreach (var _ in report.Headers) c.RelativeColumn(); });
                        table.Header(h => { foreach (var heading in report.Headers) h.Cell().Background(Colors.Blue.Lighten4).Padding(6).Text(heading).FontSize(9).Bold(); });
                        if (report.Rows.Count == 0)
                            table.Cell().ColumnSpan((uint)report.Headers.Length).Padding(14).AlignCenter().Text("No posted records for this date.").FontColor(Colors.Grey.Darken1);
                        foreach (var row in report.Rows)
                            foreach (var value in row)
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(value ?? "—").FontSize(9);
                    });
                    content.Item().PaddingTop(20).AlignRight().Width(330).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });
                        foreach (var total in report.Totals)
                        {
                            table.Cell().Background(Colors.Grey.Lighten4).Padding(7).Text(total.Label).FontSize(9).SemiBold();
                            table.Cell().Background(Colors.Grey.Lighten4).Padding(7).AlignRight().Text(total.Value).FontSize(9).Bold();
                        }
                    });
                });
                page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages(); });
            })).GeneratePdf(stream);
            stream.Position = 0;
            await using var destination = await file.OpenWriteAsync();
            await stream.CopyToAsync(destination);
            StatusMessage = $"{title} created successfully for {date:dd MMM yyyy}.";
        }
        catch (Exception ex) { StatusMessage = $"Report generation failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
