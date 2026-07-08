using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ClinicSystem.UI.ViewModels.Dashboard;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly PatientRepository    _patientRepo;
    private readonly MedicineRepository   _medicineRepo;
    private readonly CompanyRepository    _companyRepo;
    private readonly SupplierRepository   _supplierRepo;
    private readonly AppointmentRepository _appointmentRepo;
    private readonly SaleRepository       _saleRepo;
    private readonly PurchaseRepository   _purchaseRepo;

    public DashboardViewModel(
        PatientRepository    patientRepo,
        MedicineRepository   medicineRepo,
        CompanyRepository    companyRepo,
        SupplierRepository   supplierRepo,
        AppointmentRepository appointmentRepo,
        SaleRepository       saleRepo,
        PurchaseRepository   purchaseRepo)
    {
        _patientRepo     = patientRepo;
        _medicineRepo    = medicineRepo;
        _companyRepo     = companyRepo;
        _supplierRepo    = supplierRepo;
        _appointmentRepo = appointmentRepo;
        _saleRepo        = saleRepo;
        _purchaseRepo    = purchaseRepo;
    }

    // ── Summary card counts ────────────────────────────────────────────────
    [ObservableProperty] private int _totalPatients;
    [ObservableProperty] private int _totalCompanies;
    [ObservableProperty] private int _totalSuppliers;
    [ObservableProperty] private int _totalMedicines;
    [ObservableProperty] private int _totalAppointmentsToday;

    // ── Today's summary panel ─────────────────────────────────────────────
    [ObservableProperty] private string _todayDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
    [ObservableProperty] private string _todayRevenue   = "Rs. 0.00";
    [ObservableProperty] private string _todayProfit    = "Rs. 0.00";
    [ObservableProperty] private string _todayLoss      = "Rs. 0.00";
    [ObservableProperty] private int    _todayPatients;

    // ── Financials summary under charts ────────────────────────────────────
    [ObservableProperty] private string _summaryTotalRevenue  = "Rs. 0.00";
    [ObservableProperty] private string _summaryTotalProfit   = "Rs. 0.00";
    [ObservableProperty] private string _summaryTotalLoss     = "Rs. 0.00";

    // ── Charts Data ────────────────────────────────────────────────────────
    [ObservableProperty] private ISeries[] _profitSeries  = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[]    _xAxes         = Array.Empty<Axis>();
    [ObservableProperty] private ISeries[] _revenueSeries = Array.Empty<ISeries>();

    // ── Lists ──────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Medicine>    _lowStockMedicines     = new();
    [ObservableProperty] private ObservableCollection<Medicine>    _expiringSoonMedicines = new();
    [ObservableProperty] private ObservableCollection<Appointment> _todayAppointments     = new();

    // ── Recent activity feed ───────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<string> _recentActivities = new();

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            var patients     = await Task.Run(() => _patientRepo.GetAll());
            var medicines    = await Task.Run(() => _medicineRepo.GetAll());
            var companies    = await Task.Run(() => _companyRepo.GetAll());
            var suppliers    = await Task.Run(() => _supplierRepo.GetAll());
            var appointments = await Task.Run(() => _appointmentRepo.GetAll());
            var sales        = await Task.Run(() => _saleRepo.GetAll());
            var purchases    = await Task.Run(() => _purchaseRepo.GetAll());

            var medicineList    = medicines.ToList();
            var appointmentList = appointments.ToList();
            var today           = DateTime.Today;

            var lowStock = medicineList
                .Where(m => m.IsLowStock && !m.IsExpired)
                .OrderBy(m => m.Stock).Take(6).ToList();

            var expiringSoon = medicineList
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
            var expenseData = new List<double>();
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
                expenseData.Add(dayPurchases);
                profitData.Add(Math.Max(0, daySales - dayPurchases));

                totalRevenue   += daySales;
                totalExpenses  += dayPurchases;
            }

            double totalProfit = Math.Max(0, totalRevenue - totalExpenses);
            double totalLoss   = totalExpenses > totalRevenue ? totalExpenses - totalRevenue : 0;

            // ── Build chart series ─────────────────────────────────────────
            var greenPaint  = new SolidColorPaint(new SKColor(0x10, 0xB9, 0x81)) { StrokeThickness = 2.5f };
            var bluePaint   = new SolidColorPaint(new SKColor(0x37, 0x99, 0xF8)) { StrokeThickness = 2.5f };
            var redPaint    = new SolidColorPaint(new SKColor(0xF8, 0x71, 0x71)) { StrokeThickness = 2.5f };

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
                },
                new LineSeries<double>
                {
                    Values            = expenseData,
                    Name              = "Loss",
                    Stroke            = redPaint,
                    GeometryFill      = new SolidColorPaint(new SKColor(0xF8, 0x71, 0x71)),
                    GeometryStroke    = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                    GeometrySize      = 8,
                    Fill              = new SolidColorPaint(new SKColor(0xF8, 0x71, 0x71, 30))
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
            var donutSeries = new ISeries[]
            {
                new PieSeries<double>
                {
                    Values         = new double[] { totalRevenue },
                    Name           = "Medicine Sales",
                    Fill           = new SolidColorPaint(new SKColor(0x10, 0xB9, 0x81)),
                    InnerRadius    = 80
                },
                new PieSeries<double>
                {
                    Values         = new double[] { Math.Max(0, totalRevenue * 0.15) },
                    Name           = "Consultation",
                    Fill           = new SolidColorPaint(new SKColor(0x37, 0x99, 0xF8)),
                    InnerRadius    = 80
                },
                new PieSeries<double>
                {
                    Values         = new double[] { Math.Max(0, totalExpenses) },
                    Name           = "Expenses",
                    Fill           = new SolidColorPaint(new SKColor(0xA7, 0x8B, 0xFA)),
                    InnerRadius    = 80
                },
                new PieSeries<double>
                {
                    Values         = new double[] { Math.Max(0, totalProfit * 0.05) },
                    Name           = "Other Income",
                    Fill           = new SolidColorPaint(new SKColor(0xFB, 0xBF, 0x24)),
                    InnerRadius    = 80
                }
            };

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TotalPatients          = patients.Count();
                TotalCompanies         = companies.Count();
                TotalSuppliers         = suppliers.Count();
                TotalMedicines         = medicineList.Count;
                TotalAppointmentsToday = todayAppts.Count;
                TodayPatients          = appointmentList.Where(a => a.AppointmentDate.Date == today).Select(a => a.PatientID).Distinct().Count();

                TodayRevenue = $"Rs. {todaySalesAmt:N2}";
                TodayProfit  = $"Rs. {Math.Max(0, todayProfitAmt):N2}";
                TodayLoss    = $"Rs. {Math.Max(0, -todayProfitAmt):N2}";

                SummaryTotalRevenue = $"Rs. {totalRevenue:N2}";
                SummaryTotalProfit  = $"Rs. {totalProfit:N2}";
                SummaryTotalLoss    = $"Rs. {totalLoss:N2}";

                LowStockMedicines     = new ObservableCollection<Medicine>(lowStock);
                ExpiringSoonMedicines = new ObservableCollection<Medicine>(expiringSoon);
                TodayAppointments     = new ObservableCollection<Appointment>(todayAppts);

                ProfitSeries  = lineSeries;
                XAxes         = xAxes;
                RevenueSeries = donutSeries;

                RecentActivities = new ObservableCollection<string>
                {
                    $"New patient registered",
                    $"Invoice created",
                    $"Payment received",
                    $"New purchase added",
                    $"{todayAppts.Count} appointment(s) today"
                };
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                RecentActivities = new ObservableCollection<string>
                    { $"Dashboard load error: {ex.Message}" });
        }
    }
}
