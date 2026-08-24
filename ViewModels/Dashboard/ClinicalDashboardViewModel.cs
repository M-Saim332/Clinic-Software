using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ClinicSystem.UI.ViewModels.Dashboard;

public class DoctorWorkloadItem
{
    public string DoctorName { get; set; } = string.Empty;
    public int AppointmentsCount { get; set; }
    public double ProgressPercentage { get; set; }
}

public class RecentVisitItem
{
    public string Name { get; set; } = string.Empty;
    public string VisitType { get; set; } = string.Empty;
    public string ReasonOfVisit { get; set; } = string.Empty;
    public string Doctor { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public partial class ClinicalDashboardViewModel : ViewModelBase
{
    private readonly AppointmentRepository _appointmentRepo;
    private readonly PatientRepository _patientRepo;
    private List<Appointment> _appointmentTrendSource = new();

    // KPIs
    [ObservableProperty] private int _todaysAppointments;
    [ObservableProperty] private string _todaysAppointmentsComparison = "+0 from yesterday";
    
    [ObservableProperty] private int _patientsSeenToday;
    [ObservableProperty] private string _patientsSeenTodayComparison = "+0 from yesterday";
    
    [ObservableProperty] private int _patientsWaiting;
    [ObservableProperty] private string _patientsWaitingComparison = "Currently waiting";
    
    [ObservableProperty] private int _totalPatients;
    [ObservableProperty] private string _totalPatientsComparison = "+0 this month";

    // Charts
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonthlyTrendSelected))]
    [NotifyPropertyChangedFor(nameof(IsYearlyTrendSelected))]
    [NotifyPropertyChangedFor(nameof(IsLifetimeTrendSelected))]
    private string _selectedPatientTrendRange = "Monthly";

    public bool IsMonthlyTrendSelected => SelectedPatientTrendRange == "Monthly";
    public bool IsYearlyTrendSelected => SelectedPatientTrendRange == "Yearly";
    public bool IsLifetimeTrendSelected => SelectedPatientTrendRange == "Lifetime";

    [ObservableProperty] private ISeries[] _patientTrendSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();
    
    [ObservableProperty] private ISeries[] _visitTypeSeries = Array.Empty<ISeries>();
    [ObservableProperty] private string _walkInPercentage = "0%";
    [ObservableProperty] private string _appointmentPercentage = "0%";
    [ObservableProperty] private int _walkInCount;
    [ObservableProperty] private int _appointmentCount;

    // Tables
    [ObservableProperty] private ObservableCollection<RecentVisitItem> _recentVisits = new();
    [ObservableProperty] private ObservableCollection<DoctorWorkloadItem> _appointmentsByDoctor = new();
    
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ClinicalDashboardViewModel(AppointmentRepository appointmentRepo, PatientRepository patientRepo)
    {
        _appointmentRepo = appointmentRepo;
        _patientRepo = patientRepo;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var appointments = await Task.Run(_appointmentRepo.GetAll);
            var patients = await Task.Run(() => _patientRepo.GetAll("Clinical"));
            
            var appointmentList = appointments.ToList();
            var patientList = patients.ToList();
            _appointmentTrendSource = appointmentList;

            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            // Calculate KPIs
            TodaysAppointments = appointmentList.Count(a => a.AppointmentDate.Date == today);
            var yesterdayAppointments = appointmentList.Count(a => a.AppointmentDate.Date == yesterday);
            TodaysAppointmentsComparison = $"{(TodaysAppointments >= yesterdayAppointments ? "+" : "")}{TodaysAppointments - yesterdayAppointments} from yesterday";

            PatientsSeenToday = appointmentList.Count(a => a.AppointmentDate.Date == today && a.Status == "Completed");
            var yesterdaySeen = appointmentList.Count(a => a.AppointmentDate.Date == yesterday && a.Status == "Completed");
            PatientsSeenTodayComparison = $"{(PatientsSeenToday >= yesterdaySeen ? "+" : "")}{PatientsSeenToday - yesterdaySeen} from yesterday";

            PatientsWaiting = appointmentList.Count(a => a.AppointmentDate.Date == today && (a.Status == "Checked-In" || a.Status == "Waiting"));
            
            TotalPatients = patientList.Count;
            var patientsThisMonth = patientList.Count(p => p.LastVisitDate >= new DateTime(today.Year, today.Month, 1));
            TotalPatientsComparison = $"+{patientsThisMonth} this month";

            BuildPatientTrendChart();

            // Setup Visit Type Donut Chart
            WalkInCount = appointmentList.Count(a => a.AppointmentDate.Date == today && a.ReasonOfVisit != null && a.ReasonOfVisit.ToLower().Contains("walk"));
            AppointmentCount = TodaysAppointments - WalkInCount;
            if (AppointmentCount < 0) AppointmentCount = 0;

            int totalVisits = WalkInCount + AppointmentCount;
            if (totalVisits > 0)
            {
                WalkInPercentage = $"{Math.Round((double)WalkInCount / totalVisits * 100)}%";
                AppointmentPercentage = $"{Math.Round((double)AppointmentCount / totalVisits * 100)}%";
            }

            VisitTypeSeries = new ISeries[]
            {
                new PieSeries<int> { Values = new[] { AppointmentCount }, Name = "Appointment", Fill = new SolidColorPaint(new SKColor(37, 99, 235)), InnerRadius = 70 },
                new PieSeries<int> { Values = new[] { WalkInCount }, Name = "Walk-in", Fill = new SolidColorPaint(new SKColor(245, 158, 11)), InnerRadius = 70 }
            };

            // Setup Appointments by Doctor
            var drCounts = appointmentList.Where(a => a.AppointmentDate.Date == today)
                                          .GroupBy(a => a.DoctorName ?? "Dr. Unknown")
                                          .Select(g => new { DoctorName = g.Key, AppointmentsCount = g.Count() })
                                          .OrderByDescending(d => d.AppointmentsCount)
                                          .Take(5)
                                          .ToList();
            var maxDoctorCount = drCounts.Count == 0 ? 0 : drCounts.Max(d => d.AppointmentsCount);
            var drGroups = drCounts.Select(d => new DoctorWorkloadItem
            {
                DoctorName = d.DoctorName,
                AppointmentsCount = d.AppointmentsCount,
                ProgressPercentage = maxDoctorCount > 0 ? Math.Round((double)d.AppointmentsCount / maxDoctorCount * 100, 1) : 0
            });
            AppointmentsByDoctor = new ObservableCollection<DoctorWorkloadItem>(drGroups);

            // Setup Recent Visits
            var recentAppts = appointmentList.OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.AppointmentTime).Take(8);
            var visits = recentAppts.Select(a => new RecentVisitItem
            {
                Name = a.DisplayPatientName,
                VisitType = "Appointment",
                ReasonOfVisit = string.IsNullOrWhiteSpace(a.ReasonOfVisit) ? "General Checkup" : a.ReasonOfVisit,
                Doctor = a.DoctorName ?? "Dr. Unknown",
                Time = a.AppointmentTime.ToString(@"hh\:mm"),
                Status = a.Status
            });
            RecentVisits = new ObservableCollection<RecentVisitItem>(visits);
        }
        catch (Exception ex) 
        { 
            StatusMessage = $"Clinical dashboard could not be loaded: {ex.Message}"; 
        }
    }

    [RelayCommand]
    private void SetPatientTrendRange(string range)
    {
        if (string.IsNullOrWhiteSpace(range) || SelectedPatientTrendRange == range) return;

        SelectedPatientTrendRange = range;
        BuildPatientTrendChart();
    }

    private void BuildPatientTrendChart()
    {
        var today = DateTime.Today;
        var appointments = _appointmentTrendSource;

        var points = SelectedPatientTrendRange switch
        {
            "Yearly" => BuildMonthlyPoints(
                new DateTime(today.Year, 1, 1),
                new DateTime(today.Year, 12, 1),
                appointments),
            "Lifetime" => BuildLifetimePoints(appointments, today),
            _ => BuildDailyPoints(new DateTime(today.Year, today.Month, 1), today, appointments)
        };

        PatientTrendSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = points.Select(p => p.Value).ToArray(),
                Fill = new SolidColorPaint(new SKColor(37, 99, 235, 40)),
                Stroke = new SolidColorPaint(new SKColor(37, 99, 235)) { StrokeThickness = 3 },
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(new SKColor(37, 99, 235)) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                LineSmoothness = 0.45
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Labels = points.Select(p => p.Label).ToArray(),
                LabelsRotation = points.Count > 12 ? 35 : 0
            }
        };
        YAxes = new Axis[] { new Axis { MinLimit = 0 } };
    }

    private static List<TrendPoint> BuildDailyPoints(DateTime startDate, DateTime endDate, IReadOnlyCollection<Appointment> appointments)
    {
        var appointmentsByDay = appointments
            .Where(a => a.AppointmentDate.Date >= startDate.Date && a.AppointmentDate.Date <= endDate.Date)
            .GroupBy(a => a.AppointmentDate.Date)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        var dayCount = Math.Max(1, (endDate.Date - startDate.Date).Days + 1);
        return Enumerable.Range(0, dayCount)
            .Select(i => startDate.AddDays(i))
            .Select(d => new TrendPoint(d.ToString("dd MMM"), appointmentsByDay.TryGetValue(d.Date, out var count) ? count : 0))
            .ToList();
    }

    private static List<TrendPoint> BuildMonthlyPoints(DateTime startMonth, DateTime endMonth, IReadOnlyCollection<Appointment> appointments)
    {
        var start = new DateTime(startMonth.Year, startMonth.Month, 1);
        var end = new DateTime(endMonth.Year, endMonth.Month, 1);
        var appointmentsByMonth = appointments
            .Where(a => a.AppointmentDate.Date >= start.Date && a.AppointmentDate.Date <= end.AddMonths(1).AddDays(-1).Date)
            .GroupBy(a => new DateTime(a.AppointmentDate.Year, a.AppointmentDate.Month, 1))
            .ToDictionary(g => g.Key, g => (double)g.Count());

        var monthCount = Math.Max(1, ((end.Year - start.Year) * 12) + end.Month - start.Month + 1);
        return Enumerable.Range(0, monthCount)
            .Select(i => start.AddMonths(i))
            .Select(m => new TrendPoint(m.ToString("MMM"), appointmentsByMonth.TryGetValue(m, out var count) ? count : 0))
            .ToList();
    }

    private static List<TrendPoint> BuildLifetimePoints(IReadOnlyCollection<Appointment> appointments, DateTime today)
    {
        var firstAppointment = appointments
            .Select(a => (DateTime?)a.AppointmentDate.Date)
            .OrderBy(d => d)
            .FirstOrDefault();

        if (firstAppointment == null)
            return BuildMonthlyPoints(new DateTime(today.Year, 1, 1), new DateTime(today.Year, today.Month, 1), appointments);

        var firstMonth = new DateTime(firstAppointment.Value.Year, firstAppointment.Value.Month, 1);
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        var totalMonths = ((currentMonth.Year - firstMonth.Year) * 12) + currentMonth.Month - firstMonth.Month + 1;

        if (totalMonths <= 24)
        {
            var monthly = BuildMonthlyPoints(firstMonth, currentMonth, appointments);
            return monthly.Select((p, i) =>
            {
                var month = firstMonth.AddMonths(i);
                return p with { Label = month.ToString("MMM yyyy") };
            }).ToList();
        }

        var appointmentsByYear = appointments
            .GroupBy(a => a.AppointmentDate.Year)
            .ToDictionary(g => g.Key, g => (double)g.Count());
        return Enumerable.Range(firstMonth.Year, today.Year - firstMonth.Year + 1)
            .Select(year => new TrendPoint(year.ToString(), appointmentsByYear.TryGetValue(year, out var count) ? count : 0))
            .ToList();
    }

    private readonly record struct TrendPoint(string Label, double Value);
}
