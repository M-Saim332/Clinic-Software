using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;
using System.Timers;
using ClinicSystem.UI.Services;

namespace ClinicSystem.UI.ViewModels.Dashboard;

public partial class DashboardViewModel : ViewModelBase, ISearchable,
    IRecipient<InventoryChangedMessage>,
    IRecipient<RefundIssuedMessage>,
    IRecipient<RefundCompletedMessage>,
    IRecipient<ActivityLogMessage>
{
    private readonly PatientRepository    _patientRepo;
    private readonly ProductRepository   _productRepo;
    private readonly CompanyRepository    _companyRepo;
    private readonly SupplierRepository   _supplierRepo;
    private readonly AppointmentRepository _appointmentRepo;
    private readonly SaleRepository       _saleRepo;
    private readonly PurchaseRepository   _purchaseRepo;
    private readonly DiscountRefundRepository _refundRepo;
    private readonly ReturnRepository     _productReturnRepo;
    private readonly ActivityLogRepository _activityRepo;
    private readonly PrescriptionRepository _prescriptionRepo;

    // 30-second auto-refresh timer for pending refunds
    private readonly System.Timers.Timer _refundPollTimer;

    // Wired up by MainWindowViewModel so the dashboard can open the shared popup
    public Action? RequestChangePassword { get; set; }

    // Navigation delegates wired by MainWindowViewModel
    public Action? RequestNavigatePatients     { get; set; }
    public Action? RequestNavigateCompanies    { get; set; }
    public Action? RequestNavigateSuppliers    { get; set; }
    public Action? RequestNavigateSales        { get; set; }
    public Action? RequestNavigateAppointments { get; set; }
    public Action? RequestNavigateProducts    { get; set; }
    public Action? RequestNavigateInventory    { get; set; }

    public bool IsAdmin => CurrentUser?.IsAdmin ?? false;

    public DashboardViewModel(
        PatientRepository    patientRepo,
        ProductRepository   productRepo,
        CompanyRepository    companyRepo,
        SupplierRepository   supplierRepo,
        AppointmentRepository appointmentRepo,
        SaleRepository       saleRepo,
        PurchaseRepository   purchaseRepo,
        DiscountRefundRepository refundRepo,
        ReturnRepository productReturnRepo,
        DiscountRefundViewModel discountRefundVM,
        ActivityLogRepository activityRepo,
        PrescriptionRepository prescriptionRepo)
    {
        _patientRepo     = patientRepo;
        _productRepo    = productRepo;
        _companyRepo     = companyRepo;
        _supplierRepo    = supplierRepo;
        _appointmentRepo = appointmentRepo;
        _saleRepo        = saleRepo;
        _purchaseRepo    = purchaseRepo;
        _refundRepo      = refundRepo;
        _productReturnRepo = productReturnRepo;
        _activityRepo    = activityRepo;
        _prescriptionRepo = prescriptionRepo;
        DiscountRefundVM = discountRefundVM;

        WeakReferenceMessenger.Default.RegisterAll(this);

        // Poll every 15 seconds so the dashboard stays in sync
        // across multiple computers (e.g. Receptionist vs Doctor sessions)
        // and picks up external database changes.
        _refundPollTimer = new System.Timers.Timer(15_000);
        _refundPollTimer.Elapsed += (_, _) => _ = InitializeAsync();
        _refundPollTimer.AutoReset = true;
        _refundPollTimer.Start();
    }

    [RelayCommand]
    private void OpenChangePassword() => RequestChangePassword?.Invoke();

    [RelayCommand] private void NavigateToPatients()     => RequestNavigatePatients?.Invoke();
    [RelayCommand] private void NavigateToCompanies()    => RequestNavigateCompanies?.Invoke();
    [RelayCommand] private void NavigateToSuppliers()    => RequestNavigateSuppliers?.Invoke();
    [RelayCommand] private void NavigateToSales()        => RequestNavigateSales?.Invoke();
    [RelayCommand] private void NavigateToAppointments() => RequestNavigateAppointments?.Invoke();
    [RelayCommand] private void NavigateToProducts()    => RequestNavigateProducts?.Invoke();
    [RelayCommand] private void NavigateToInventory()    => RequestNavigateInventory?.Invoke();

    public DiscountRefundViewModel DiscountRefundVM { get; }

    // ── Pending refund notifications (receptionist sees these) ────────────────
    [ObservableProperty] private ObservableCollection<DiscountRefund> _pendingRefunds = new();
    [ObservableProperty] private bool _hasPendingRefunds;
    [ObservableProperty] private ObservableCollection<DiscountRefund> _refundHistory = new();

    // ── Doctor handoff notifications ─────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Prescription> _prescriptionNotifications = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDispenseSelected))]
    private Prescription? _selectedHandoff;
    [ObservableProperty] private bool _showHandoffDetails;
    [ObservableProperty] private string _prescriptionStatusMessage = string.Empty;
    [ObservableProperty] private int _newPrescriptionCount;
    public bool CanSeePrescriptionNotifications => CurrentUser?.IsPharmacist == true ||
        CurrentUser?.UserRole == ClinicSystem.Core.Enums.UserRole.Receptionist;
    public bool CanManagePrescriptionHandoffs => CurrentUser?.IsPharmacist == true;
    public bool HasPrescriptionNotifications => PrescriptionNotifications.Count > 0;
    public bool CanDispenseSelected => CanManagePrescriptionHandoffs && SelectedHandoff?.WorkflowStatus == "Printed";

    // ── Summary card counts ────────────────────────────────────────────────
    [ObservableProperty] private int _totalProducts;
    [ObservableProperty] private int _totalAppointmentsToday;

    // ── Today's summary panel ─────────────────────────────────────────────
    [ObservableProperty] private string _todayDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
    [ObservableProperty] private string _todayRevenue   = "Rs. 0.00";
    [ObservableProperty] private string _todayProfit    = "Rs. 0.00";
    [ObservableProperty] private int    _todayPatients;
    [ObservableProperty] private int    _totalPatients;

    // ── Financials summary under charts ────────────────────────────────────
    [ObservableProperty] private string _summaryTotalRevenue      = "Rs. 0.00";
    [ObservableProperty] private string _summaryTotalProfit       = "Rs. 0.00";
    [ObservableProperty] private string _totalStockValue          = "Rs. 0.00";

    // ── Charts Data ────────────────────────────────────────────────────────
    [ObservableProperty] private ISeries[] _profitSeries  = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[]    _xAxes         = Array.Empty<Axis>();
    [ObservableProperty] private ISeries[] _revenueSeries = Array.Empty<ISeries>();

    // ── Lists ──────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Product>    _lowStockProducts     = new();
    [ObservableProperty] private ObservableCollection<Product>    _expiringSoonProducts = new();
    [ObservableProperty] private ObservableCollection<Appointment> _todayAppointments     = new();

    // ── Recent activity feed ───────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ActivityLog> _recentActivities = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRecentActivities))]
    private bool _hasRecentActivities;

    public bool HasNoRecentActivities => !HasRecentActivities;

    [ObservableProperty] private string _searchTerm = string.Empty;
    public string SearchPlaceholder => "Search Dashboard...";

    partial void OnSearchTermChanged(string value) => FilterActivities();

    private void FilterActivities()
    {
        // Add minimal filtering for Dashboard activities if needed, else ignore
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            // Load pending refund notifications
            await LoadPendingRefundsAsync();
            await LoadPrescriptionNotificationsAsync();

            var today = DateTime.Today;

            // Fetch scalar values asynchronously directly from DB to avoid loading full tables
            int totalPatients = await Task.Run(() => _patientRepo.GetCount());
            int totalProducts = await Task.Run(() => _productRepo.GetCount());
            int todayApptsCount = await Task.Run(() => _appointmentRepo.GetTodayCount());
            int todayPatientsCount = await Task.Run(() => _appointmentRepo.GetTodayDistinctPatientCount());
            
            decimal stockValue = await Task.Run(() => _productRepo.GetTotalStockValue());
            
            var lowStock = await Task.Run(() => _productRepo.GetLowStock().Take(6).ToList());
            var expiringSoon = await Task.Run(() => _productRepo.GetExpiringSoon(30).Take(6).ToList());
            var todayAppts = await Task.Run(() => _appointmentRepo.GetByDate(today).Take(10).ToList());

            // ── Financial calculations ─────────────────────────────────────
            // Today's financials
            decimal todaySalesAmt = await Task.Run(() => _saleRepo.GetTodayRevenue());
            decimal todayCogsAmt = await Task.Run(() => _saleRepo.GetTodayCostOfGoodsSold());
            decimal todayRefundAmt = await Task.Run(() => _refundRepo.GetTodayTotalCompleted());
            decimal todayPatientReturns = await Task.Run(() => _productReturnRepo.GetTodayTotalPatientReturns());
            decimal todaySupplierReturns = await Task.Run(() => _productReturnRepo.GetTodayTotalSupplierReturns());
            
            decimal todayProfitAmt = todaySalesAmt - todayCogsAmt - todayRefundAmt - todayPatientReturns + todaySupplierReturns;

            // 30-day chart + totals (from optimized queries)
            var dailySales = await Task.Run(() => _saleRepo.GetDailyRevenueLast30Days().ToDictionary(x => x.Date, x => x));
            var dailyCogs = await Task.Run(() => _saleRepo.GetDailyCostOfGoodsSoldLast30Days().ToDictionary(x => x.Date, x => x.Cogs));
            var dailyPurchases = await Task.Run(() => _purchaseRepo.GetDailyTotalsLast30Days().ToDictionary(x => x.Date, x => x.Total));
            var dailyRefunds = await Task.Run(() => _refundRepo.GetDailyTotalsLast30Days().ToDictionary(x => x.Date, x => x.Total));
            var dailyPatientReturns = await Task.Run(() => _productReturnRepo.GetDailyPatientReturnsLast30Days().ToDictionary(x => (DateTime)x.Date, x => (decimal)x.Total));
            var dailySupplierReturns = await Task.Run(() => _productReturnRepo.GetDailySupplierReturnsLast30Days().ToDictionary(x => (DateTime)x.Date, x => (decimal)x.Total));

            var thirtyDaysAgo = today.AddDays(-29);
            var dateLabels  = new List<string>();
            var revenueData = new List<double>();
            var profitData  = new List<double>();

            double totalRevenue = 0, totalCogs = 0, totalRefunds = 0, totalSupplierCredits = 0;

            for (int i = 0; i < 30; i++)
            {
                var d = thirtyDaysAgo.AddDays(i);
                // Show every 3rd label to avoid clutter
                dateLabels.Add(i % 3 == 0 ? d.ToString("MMM dd") : "");

                double daySales = 0, dayCogs = 0, dayRefunds = 0;

                if (dailySales.TryGetValue(d, out var saleData))
                {
                    daySales = (double)saleData.Revenue;
                }

                if (dailyCogs.TryGetValue(d, out var cogsTotal))
                {
                    dayCogs = (double)cogsTotal;
                }

                if (dailyRefunds.TryGetValue(d, out var refundTotal))
                {
                    dayRefunds += (double)refundTotal;
                }

                if (dailyPatientReturns.TryGetValue(d, out var patientReturns))
                {
                    dayRefunds += (double)patientReturns;
                }

                double daySupplierCredits = 0;
                if (dailySupplierReturns.TryGetValue(d, out var supplierReturns))
                {
                    daySupplierCredits = (double)supplierReturns;
                }

                // Revenue line = posted product sales.
                revenueData.Add(daySales);
                // Profit line = revenue minus cost of goods sold and refunds
                profitData.Add(Math.Max(0, daySales - dayCogs - dayRefunds + daySupplierCredits));

                totalRevenue         += daySales;
                totalCogs            += dayCogs;
                totalRefunds         += dayRefunds;
                totalSupplierCredits += daySupplierCredits;
            }

            // True 30-day profit = revenue - cost of goods sold - refunds + supplier credits
            double totalProfit = Math.Max(0, totalRevenue - totalCogs - totalRefunds + totalSupplierCredits);

            // ── Build chart series ─────────────────────────────────────────
            var greenPaint  = new SolidColorPaint(new SKColor(0x10, 0xB9, 0x81)) { StrokeThickness = 2.5f };
            var bluePaint   = new SolidColorPaint(new SKColor(0x37, 0x99, 0xF8)) { StrokeThickness = 2.5f };

            var lineSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values            = revenueData,
                    Name              = "Revenue",
                    Stroke            = greenPaint,
                    GeometryFill      = new SolidColorPaint(new SKColor(0x10, 0xB9, 0x81)),
                    GeometryStroke    = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                    GeometrySize      = 8,
                    Fill              = new SolidColorPaint(new SKColor(0x10, 0xB9, 0x81, 30))
                },
                new LineSeries<double>
                {
                    Values            = profitData,
                    Name              = "Profit",
                    Stroke            = bluePaint,
                    GeometryFill      = new SolidColorPaint(new SKColor(0x37, 0x99, 0xF8)),
                    GeometryStroke    = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                    GeometrySize      = 8,
                    Fill              = new SolidColorPaint(new SKColor(0x37, 0x99, 0xF8, 30))
                }
            };

            var xAxes = new Axis[]
            {
                new Axis
                {
                    Labels          = dateLabels,
                    LabelsRotation  = 0,
                    TextSize        = 10,
                    Padding         = new LiveChartsCore.Drawing.Padding(4)
                }
            };

            // Donut/Pie series for revenue overview
            // Calculate real values from live data instead of hardcoded percentages
            double productSalesValue = Math.Max(0, totalRevenue);
            var donutSeries = new ISeries[]
            {
                new PieSeries<double>
                {
                    Values         = new double[] { productSalesValue },
                    Name           = "Product Sales",
                    Fill           = new SolidColorPaint(new SKColor(0x10, 0xB9, 0x81)),
                    InnerRadius    = 80
                },
                new PieSeries<double>
                {
                    Values         = new double[] { totalCogs },
                    Name           = "Purchases",
                    Fill           = new SolidColorPaint(new SKColor(0xA7, 0x8B, 0xFA)),
                    InnerRadius    = 80
                },
                new PieSeries<double>
                {
                    Values         = new double[] { totalRefunds },
                    Name           = "Refunds",
                    Fill           = new SolidColorPaint(new SKColor(0xFB, 0xBF, 0x24)),
                    InnerRadius    = 80
                }
            };

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TotalProducts         = totalProducts;
                TotalAppointmentsToday = todayApptsCount;
                TodayPatients          = todayPatientsCount;
                TotalPatients          = totalPatients;

                TodayRevenue = $"Rs. {todaySalesAmt:N2}";
                TodayProfit  = $"Rs. {Math.Max(0, todayProfitAmt):N2}";

                SummaryTotalRevenue    = $"Rs. {totalRevenue:N2}";
                SummaryTotalProfit     = $"Rs. {totalProfit:N2}";
                TotalStockValue        = $"Rs. {stockValue:N2}";

                LowStockProducts     = new ObservableCollection<Product>(lowStock);
                ExpiringSoonProducts = new ObservableCollection<Product>(expiringSoon);
                TodayAppointments     = new ObservableCollection<Appointment>(todayAppts);

                ProfitSeries  = lineSeries;
                XAxes         = xAxes;
                RevenueSeries = donutSeries;
            });

            // ── Load real recent activities (outside UIThread.Post because it needs await) ──
            var activities = await Task.Run(() => _activityRepo.GetLatest(20));
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RecentActivities = new ObservableCollection<ActivityLog>(activities);
                HasRecentActivities = RecentActivities.Count > 0;
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RecentActivities = new ObservableCollection<ActivityLog>
                    { new ActivityLog { Title = "Dashboard load error", Description = ex.Message, Module = "Dashboard" } };
                HasRecentActivities = true;
            });
        }
    }

    public void Receive(InventoryChangedMessage message) => _ = InitializeAsync();

    public void Receive(RefundIssuedMessage message)   => _ = LoadPendingRefundsAsync();
    public void Receive(RefundCompletedMessage message) => _ = LoadPendingRefundsAsync();

    public void Receive(ActivityLogMessage message)
    {
        // Refresh the entire dashboard (KPIs, Charts, and Recent Activities feed)
        // when any CRUD operation occurs to keep data live.
        _ = InitializeAsync();
    }

    // ── Refund Commands ───────────────────────────────────────────────────────
    [RelayCommand]
    private async Task MarkRefundCompletedAsync(DiscountRefund refund)
    {
        if (refund == null) return;
        try
        {
            var completedBy     = CurrentUser?.UserID ?? 0;
            var completedByName = CurrentUser?.FullName.Length > 0
                                      ? CurrentUser.FullName
                                      : CurrentUser?.Username ?? "Receptionist";

            await Task.Run(() => _refundRepo.MarkCompleted(refund.RefundID, completedBy, completedByName));

            WeakReferenceMessenger.Default.Send(new RefundCompletedMessage { RefundID = refund.RefundID });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                RecentActivities.Insert(0, new ActivityLog { Title = "Refund error", Description = ex.Message, Module = "Sales" }));
        }
    }

    public async Task LoadPendingRefundsAsync()
    {
        try
        {
            var pending = await Task.Run(() => _refundRepo.GetAllPending());
            var history = await Task.Run(() => _refundRepo.GetAllHistory(50));

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                PendingRefunds    = new ObservableCollection<DiscountRefund>(pending);
                HasPendingRefunds = PendingRefunds.Count > 0;
                RefundHistory     = new ObservableCollection<DiscountRefund>(history);
            });
        }
        catch { /* silent — dashboard still loads */ }
    }

    public async Task LoadPrescriptionNotificationsAsync()
    {
        if (!CanSeePrescriptionNotifications) return;
        try
        {
            var handoffs = await Task.Run(() => _prescriptionRepo.GetPharmacyHandoffs(includeDispensed: true).ToList());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                PrescriptionNotifications = new ObservableCollection<Prescription>(handoffs);
                NewPrescriptionCount = handoffs.Count(p => p.WorkflowStatus == "SentToPharmacy");
                OnPropertyChanged(nameof(HasPrescriptionNotifications));
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => PrescriptionStatusMessage = $"Notifications could not be loaded: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenHandoff(Prescription? prescription)
    {
        if (prescription == null) return;
        SelectedHandoff = prescription;
        ShowHandoffDetails = true;
        PrescriptionStatusMessage = string.Empty;
    }

    [RelayCommand] private void CloseHandoff() => ShowHandoffDetails = false;

    [RelayCommand]
    private async Task PrintHandoffAsync()
    {
        if (SelectedHandoff == null || !CanManagePrescriptionHandoffs) return;
        if (!await PrescriptionPrintService.ExportAsync(SelectedHandoff)) return;
        await Task.Run(() => _prescriptionRepo.MarkPrinted(SelectedHandoff.PrescriptionID));
        SelectedHandoff.WorkflowStatus = "Printed";
        OnPropertyChanged(nameof(CanDispenseSelected));
        PrescriptionStatusMessage = "Prescription checked and exported for printing.";
        await LoadPrescriptionNotificationsAsync();
    }

    [RelayCommand]
    private async Task MarkMedicinesGivenAsync()
    {
        if (SelectedHandoff == null || !CanManagePrescriptionHandoffs) return;
        if (SelectedHandoff.WorkflowStatus != "Printed")
        {
            PrescriptionStatusMessage = "Check and print the prescription before giving medicines.";
            return;
        }
        await Task.Run(() => _prescriptionRepo.MarkDispensed(SelectedHandoff.PrescriptionID));
        var patientName = SelectedHandoff.PatientName;
        ShowHandoffDetails = false;
        PrescriptionStatusMessage = $"Medicines marked as given to {patientName}.";
        LogActivity("Medicines Given", $"Prescription dispensed to {patientName}", "Prescriptions");
        await LoadPrescriptionNotificationsAsync();
    }
}
