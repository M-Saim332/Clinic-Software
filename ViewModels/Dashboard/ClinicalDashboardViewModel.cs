using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
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
    public double ProgressPercentage => AppointmentsCount > 0 ? Math.Min(100, AppointmentsCount * 5) : 0; // Mock percentage based on count
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

            // Setup Patient Trends Line Chart (Mocking Monthly Data for visualization)
            // Real app would group by days in the current month
            var trendValues = new double[] { 12, 19, 15, 25, 22, 30, 28, 35, 42, 38, 45, 50, 48, 55, 60 };
            PatientTrendSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = trendValues,
                    Fill = new SolidColorPaint(new SKColor(37, 99, 235, 40)), // Blue with alpha
                    Stroke = new SolidColorPaint(new SKColor(37, 99, 235)) { StrokeThickness = 3 },
                    GeometrySize = 8,
                    GeometryStroke = new SolidColorPaint(new SKColor(37, 99, 235)) { StrokeThickness = 2 },
                    GeometryFill = new SolidColorPaint(SKColors.White)
                }
            };
            
            XAxes = new Axis[] { new Axis { Labels = new[] { "1", "3", "5", "7", "9", "11", "13", "15", "17", "19", "21", "23", "25", "27", "29" } } };
            YAxes = new Axis[] { new Axis { MinLimit = 0 } };

            // Setup Visit Type Donut Chart
            // In a real app we'd differentiate walk-ins by a field. We mock based on reason or appointment time.
            WalkInCount = appointmentList.Count(a => a.AppointmentDate.Date == today && a.ReasonOfVisit != null && a.ReasonOfVisit.ToLower().Contains("walk"));
            if (WalkInCount == 0) WalkInCount = TodaysAppointments / 3; // Mock realistic ratio if no real data
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
            var drGroups = appointmentList.Where(a => a.AppointmentDate.Date == today)
                                          .GroupBy(a => a.DoctorName ?? "Dr. Unknown")
                                          .Select(g => new DoctorWorkloadItem { DoctorName = g.Key, AppointmentsCount = g.Count() })
                                          .OrderByDescending(d => d.AppointmentsCount)
                                          .Take(5);
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
}
