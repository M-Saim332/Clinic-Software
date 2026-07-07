using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Dashboard;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly PatientRepository  _patientRepo;
    private readonly MedicineRepository _medicineRepo;
    private readonly CompanyRepository  _companyRepo;
    private readonly SupplierRepository _supplierRepo;
    private readonly ProductRepository  _productRepo;
    private readonly AppointmentRepository _appointmentRepo;

    public DashboardViewModel(
        PatientRepository  patientRepo,
        MedicineRepository medicineRepo,
        CompanyRepository  companyRepo,
        SupplierRepository supplierRepo,
        ProductRepository  productRepo,
        AppointmentRepository appointmentRepo)
    {
        _patientRepo     = patientRepo;
        _medicineRepo    = medicineRepo;
        _companyRepo     = companyRepo;
        _supplierRepo    = supplierRepo;
        _productRepo     = productRepo;
        _appointmentRepo = appointmentRepo;
    }

    // ── Summary card counts ────────────────────────────────────────────────
    [ObservableProperty] private int    _totalPatients;
    [ObservableProperty] private int    _totalCompanies;
    [ObservableProperty] private int    _totalProducts;
    [ObservableProperty] private int    _totalSuppliers;
    [ObservableProperty] private int    _totalMedicines;
    [ObservableProperty] private int    _totalAppointmentsToday;

    // ── Today's summary ────────────────────────────────────────────────────
    [ObservableProperty] private string _todayDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");

    // ── Lists ──────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Medicine> _lowStockMedicines  = new();
    [ObservableProperty] private ObservableCollection<Medicine> _expiringSoonMedicines = new();
    [ObservableProperty] private ObservableCollection<Appointment> _todayAppointments = new();

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
            var products     = await Task.Run(() => _productRepo.GetAll());
            var appointments = await Task.Run(() => _appointmentRepo.GetAll());

            var medicineList    = medicines.ToList();
            var appointmentList = appointments.ToList();
            var today           = DateTime.Today;

            var lowStock    = medicineList.Where(m => m.IsLowStock && !m.IsExpired)
                                          .OrderBy(m => m.Stock).Take(8).ToList();
            var expiringSoon = medicineList
                                .Where(m => m.ExpiryDate.HasValue && !m.IsExpired
                                         && m.ExpiryDate.Value <= today.AddDays(30))
                                .OrderBy(m => m.ExpiryDate).Take(8).ToList();
            var todayAppts  = appointmentList
                                .Where(a => a.AppointmentDate.Date == today)
                                .OrderBy(a => a.AppointmentTime).Take(10).ToList();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TotalPatients          = patients.Count();
                TotalCompanies         = companies.Count();
                TotalProducts          = products.Count();
                TotalSuppliers         = suppliers.Count();
                TotalMedicines         = medicineList.Count;
                TotalAppointmentsToday = todayAppts.Count;

                LowStockMedicines     = new ObservableCollection<Medicine>(lowStock);
                ExpiringSoonMedicines = new ObservableCollection<Medicine>(expiringSoon);
                TodayAppointments     = new ObservableCollection<Appointment>(todayAppts);

                RecentActivities = new ObservableCollection<string>
                {
                    $"📅 {todayAppts.Count} appointment(s) scheduled for today",
                    $"⚠️  {lowStock.Count} medicine(s) below minimum stock level",
                    $"🕐 {expiringSoon.Count} medicine(s) expiring within 30 days",
                    $"👥 {patients.Count()} total patients registered",
                    $"💊 {medicineList.Count} medicines in the system"
                };
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                RecentActivities = new ObservableCollection<string>
                    { $"❌ Dashboard load error: {ex.Message}" });
        }
    }
}
