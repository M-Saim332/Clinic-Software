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

namespace ClinicSystem.UI.ViewModels.Dashboard;

public partial class DashboardViewModel : ViewModelBase,
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
    private readonly ActivityLogRepository _activityRepo;

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
        DiscountRefundViewModel discountRefundVM,
        ActivityLogRepository activityRepo)
    {
        _patientRepo     = patientRepo;
        _productRepo    = productRepo;
        _companyRepo     = companyRepo;
        _supplierRepo    = supplierRepo;
        _appointmentRepo = appointmentRepo;
        _saleRepo        = saleRepo;
        _purchaseRepo    = purchaseRepo;
        _refundRepo      = refundRepo;
        _activityRepo    = activityRepo;
        DiscountRefundVM = discountRefundVM;

        WeakReferenceMessenger.Default.Register<InventoryChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<RefundIssuedMessage>(this);
        WeakReferenceMessenger.Default.Register<RefundCompletedMessage>(this);
        WeakReferenceMessenger.Default.Register<ActivityLogMessage>(this);

        // Poll every 30 seconds so the receptionist sees refunds even if
        // the doctor is logged in on the same machine in a different session.
        _refundPollTimer = new System.Timers.Timer(30_000);
        _refundPollTimer.Elapsed += (_, _) => _ = LoadPendingRefundsAsync();
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

    // ── Summary card counts ────────────────────────────────────────────────
    [ObservableProperty] private int _totalProducts;
    [ObservableProperty] private int _totalAppointmentsToday;

    // ── Today's summary panel ─────────────────────────────────────────────
    [ObservableProperty] private string _todayDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
    [ObservableProperty] private string _todayRevenue   = "Rs. 0.00";
    [ObservableProperty] private string _todayProfit    = "Rs. 0.00";
    [ObservableProperty] private int    _todayPatients;

    // ── Financials summary under charts ────────────────────────────────────
    [ObservableProperty] private string _summaryTotalRevenue  = "Rs. 0.00";
    [ObservableProperty] private string _summaryTotalProfit   = "Rs. 0.00";
    [ObservableProperty] private string _totalStockValue      = "Rs. 0.00";

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

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            var patients     = await Task.Run(() => _patientRepo.GetAll());
            var products    = await Task.Run(() => _productRepo.GetAll());
            var companies    = await Task.Run(() => _companyRepo.GetAll());
            var suppliers    = await Task.Run(() => _supplierRepo.GetAll());
            var appointments = await Task.Run(() => _appointmentRepo.GetAll());
            var sales        = await Task.Run(() => _saleRepo.GetAll());
            var purchases    = await Task.Run(() => _purchaseRepo.GetAll());

            // Load pending refund notifications
            await LoadPendingRefundsAsync();

            var productList    = products.ToList();
            var appointmentList = appointments.ToList();
            var today           = DateTime.Today;

            var lowStock = productList
                .Where(m => m.IsLowStock && !m.IsExpired)
                .OrderBy(m => m.Stock).Take(6).ToList();

            var expiringSoon = productList
                .Where(m => m.ExpiryDate.HasValue && !m.IsExpired
                         && m.ExpiryDate.Value <= today.AddDays(30))
                .OrderBy(m => m.ExpiryDate).Take(6).ToList();

            var todayAppts = appointmentList
                .Where(a => a.AppointmentDate.Date == today)
                .OrderBy(a => a.AppointmentTime).Take(10).ToList();

            // ── Financial calculations ─────────────────────────────────────
            var salesList    = sales.Where(s => s.IsPosted).ToList();
            var purchaseList = purchases.ToList();

            // Today's financials
            var todaySalesAmt    = salesList.Where(s => s.SaleDate.Date == today).Sum(s => (double)s.GrandTotal);
            var todayPurchaseAmt = purchaseList.Where(p => p.PurchaseDate.Date == today).Sum(p => (double)p.TotalAmount);
            var todayProfitAmt   = todaySalesAmt - todayPurchaseAmt;

            // 30-day chart + totals
            var thirtyDaysAgo = today.AddDays(-29);
            var dateLabels  = new List<string>();
            var revenueData = new List<double>();
            var profitData  = new List<double>();

            double totalRevenue = 0, totalExpenses = 0;

            for (int i = 0; i < 30; i++)
            {
                var d = thirtyDaysAgo.AddDays(i);
                // Show every 3rd label to avoid clutter
                dateLabels.Add(i % 3 == 0 ? d.ToString("MMM dd") : "");

                var daySales     = salesList.Where(s => s.SaleDate.Date == d).Sum(s => (double)s.GrandTotal);
                var dayPurchases = purchaseList.Where(p => p.PurchaseDate.Date == d).Sum(p => (double)p.TotalAmount);

                revenueData.Add(daySales);
                profitData.Add(Math.Max(0, daySales - dayPurchases));

                totalRevenue   += daySales;
                totalExpenses  += dayPurchases;
            }

            double totalProfit = Math.Max(0, totalRevenue - totalExpenses);
            double stockValue = productList.Sum(m => (double)(m.PurchasePrice * m.Stock));

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
            // Use Math.Max(1.0, ...) so slices render even when data is zero
            var donutSeries = new ISeries[]
            {
                new PieSeries<double>
                {
                    Values         = new double[] { Math.Max(1.0, totalRevenue) },
                    Name           = "Product Sales",
                    Fill           = new SolidColorPaint(new SKColor(0x10, 0xB9, 0x81)),
                    InnerRadius    = 80
                },
                new PieSeries<double>
                {
                    Values         = new double[] { Math.Max(1.0, totalRevenue * 0.15) },
                    Name           = "Consultation",
                    Fill           = new SolidColorPaint(new SKColor(0x37, 0x99, 0xF8)),
                    InnerRadius    = 80
                },
                new PieSeries<double>
                {
                    Values         = new double[] { Math.Max(1.0, totalExpenses) },
                    Name           = "Expenses",
                    Fill           = new SolidColorPaint(new SKColor(0xA7, 0x8B, 0xFA)),
                    InnerRadius    = 80
                },
                new PieSeries<double>
                {
                    Values         = new double[] { Math.Max(1.0, totalProfit * 0.05) },
                    Name           = "Other Income",
                    Fill           = new SolidColorPaint(new SKColor(0xFB, 0xBF, 0x24)),
                    InnerRadius    = 80
                }
            };

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TotalProducts         = productList.Count;
                TotalAppointmentsToday = todayAppts.Count;
                TodayPatients          = appointmentList.Where(a => a.AppointmentDate.Date == today).Select(a => a.PatientID).Distinct().Count();

                TodayRevenue = $"Rs. {todaySalesAmt:N2}";
                TodayProfit  = $"Rs. {Math.Max(0, todayProfitAmt):N2}";

                SummaryTotalRevenue = $"Rs. {totalRevenue:N2}";
                SummaryTotalProfit  = $"Rs. {totalProfit:N2}";
                TotalStockValue     = $"Rs. {stockValue:N2}";

                LowStockProducts     = new ObservableCollection<Product>(lowStock);
                ExpiringSoonProducts = new ObservableCollection<Product>(expiringSoon);
                TodayAppointments     = new ObservableCollection<Appointment>(todayAppts);

                ProfitSeries  = lineSeries;
                XAxes         = xAxes;
                RevenueSeries = donutSeries;
            });

            // ── Load real recent activities (outside UIThread.Post because it needs await) ──
            var activities = await Task.Run(() => _activityRepo.GetLatest(15));
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RecentActivities = new ObservableCollection<ActivityLog>(activities);
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                RecentActivities = new ObservableCollection<ActivityLog>
                    { new ActivityLog { Title = "Dashboard load error", Description = ex.Message, Module = "Dashboard" } });
        }
    }

    public void Receive(InventoryChangedMessage message) => _ = InitializeAsync();

    public void Receive(RefundIssuedMessage message)   => _ = LoadPendingRefundsAsync();
    public void Receive(RefundCompletedMessage message) => _ = LoadPendingRefundsAsync();

    public void Receive(ActivityLogMessage message)
    {
        // Fire-and-forget DB insertion and refresh
        Task.Run(async () =>
        {
            _activityRepo.Insert(message.Log);
            var activities = await Task.Run(() => _activityRepo.GetLatest(15));
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RecentActivities = new ObservableCollection<ActivityLog>(activities);
            });
        });
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
}
