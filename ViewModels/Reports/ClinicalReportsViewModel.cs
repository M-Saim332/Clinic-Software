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

namespace ClinicSystem.UI.ViewModels.Reports;

public class DoctorPerformanceRow
{
    public string DoctorName { get; set; } = string.Empty;
    public int TotalHandled { get; set; }
    public int Completed { get; set; }
    public double AveragePerDay { get; set; }
    public double Percentage { get; set; }
}

public class VisitReasonRow
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }
}

public partial class ClinicalReportsViewModel : ViewModelBase
{
    private readonly PatientRepository _patientRepo;
    private readonly AppointmentRepository _appointmentRepo;

    // Filters
    [ObservableProperty] private int _selectedDateFilterIndex = 2; // Default "This Month"
    
    // Patient Analytics
    [ObservableProperty] private int _totalVisits;
    [ObservableProperty] private int _newPatients;
    [ObservableProperty] private int _returningPatients;
    [ObservableProperty] private double _averagePatientsPerDay;
    [ObservableProperty] private string _patientGrowthRate = "+0%";
    [ObservableProperty] private ISeries[] _patientVisitsSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();

    // Visit Type Analysis
    [ObservableProperty] private ISeries[] _visitTypeSeries = Array.Empty<ISeries>();
    [ObservableProperty] private int _walkInVisits;
    [ObservableProperty] private int _appointmentVisits;
    [ObservableProperty] private string _walkInPercentage = "0%";
    [ObservableProperty] private string _appointmentPercentage = "0%";

    // Appointment Analytics
    [ObservableProperty] private int _totalAppointments;
    [ObservableProperty] private int _completedAppointments;
    [ObservableProperty] private int _cancelledAppointments;
    [ObservableProperty] private int _noShows;
    [ObservableProperty] private string _completionRate = "0%";
    [ObservableProperty] private ISeries[] _appointmentTrendsSeries = Array.Empty<ISeries>();
    
    // Doctor Performance & Workload
    [ObservableProperty] private ObservableCollection<DoctorPerformanceRow> _doctorPerformances = new();
    
    // Visit Reasons
    [ObservableProperty] private ObservableCollection<VisitReasonRow> _visitReasons = new();
    [ObservableProperty] private ISeries[] _visitReasonSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _reasonYAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _reasonXAxes = Array.Empty<Axis>();

    public ClinicalReportsViewModel(PatientRepository patientRepo, AppointmentRepository appointmentRepo)
    {
        _patientRepo = patientRepo;
        _appointmentRepo = appointmentRepo;
    }

    public async Task InitializeAsync()
    {
        await LoadReportDataAsync();
    }

    partial void OnSelectedDateFilterIndexChanged(int value)
    {
        _ = LoadReportDataAsync();
    }

    private async Task LoadReportDataAsync()
    {
        var allAppointments = await Task.Run(_appointmentRepo.GetAll);
        var allPatients = await Task.Run(() => _patientRepo.GetAll("Clinical"));

        var appointmentsList = allAppointments.ToList();
        var patientsList = allPatients.ToList();

        // Apply Date Filter based on SelectedDateFilterIndex (0: Today, 1: This Week, 2: This Month, 3: This Year, 4: All Time)
        var today = DateTime.Today;
        DateTime startDate = DateTime.MinValue;
        DateTime endDate = today.AddDays(1).AddTicks(-1);

        switch (SelectedDateFilterIndex)
        {
            case 0: startDate = today; break;
            case 1: startDate = today.AddDays(-(int)today.DayOfWeek); break;
            case 2: startDate = new DateTime(today.Year, today.Month, 1); break;
            case 3: startDate = new DateTime(today.Year, 1, 1); break;
            case 4: startDate = DateTime.MinValue; break;
        }

        var effectiveStartDate = startDate == DateTime.MinValue
            ? appointmentsList.Select(a => (DateTime?)a.AppointmentDate.Date)
                .Concat(patientsList.Select(p => p.LastVisitDate?.Date))
                .Where(d => d.HasValue)
                .Min() ?? today
            : startDate;

        var filteredAppointments = appointmentsList.Where(a => a.AppointmentDate.Date >= effectiveStartDate.Date && a.AppointmentDate.Date <= endDate.Date).ToList();
        var totalDays = (endDate.Date - effectiveStartDate.Date).Days + 1;
        if (totalDays <= 0) totalDays = 1;

        // 1. Patient Analytics
        TotalVisits = filteredAppointments.Count(a => a.Status == "Completed");
        NewPatients = patientsList.Count(p => p.LastVisitDate?.Date >= effectiveStartDate.Date && p.LastVisitDate?.Date <= endDate.Date);
        ReturningPatients = TotalVisits > NewPatients ? TotalVisits - NewPatients : 0;
        AveragePatientsPerDay = Math.Round((double)TotalVisits / totalDays, 1);
        var previousStartDate = effectiveStartDate.AddDays(-totalDays);
        var previousEndDate = effectiveStartDate.AddDays(-1);
        var previousVisits = appointmentsList.Count(a =>
            a.Status == "Completed" &&
            a.AppointmentDate.Date >= previousStartDate.Date &&
            a.AppointmentDate.Date <= previousEndDate.Date);
        PatientGrowthRate = previousVisits == 0
            ? (TotalVisits > 0 ? "+100%" : "0%")
            : $"{(TotalVisits >= previousVisits ? "+" : "")}{Math.Round(((double)TotalVisits - previousVisits) / previousVisits * 100)}%";

        var trendDays = BuildTrendDays(effectiveStartDate.Date, endDate.Date);
        var visitsByDay = filteredAppointments
            .Where(a => a.Status == "Completed")
            .GroupBy(a => a.AppointmentDate.Date)
            .ToDictionary(g => g.Key, g => (double)g.Count());
        var trendValues = trendDays.Select(d => visitsByDay.TryGetValue(d, out var count) ? count : 0).ToArray();
        PatientVisitsSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = trendValues,
                Fill = new SolidColorPaint(new SKColor(37, 99, 235, 40)),
                Stroke = new SolidColorPaint(new SKColor(37, 99, 235)) { StrokeThickness = 3 },
                GeometrySize = 0,
                LineSmoothness = 0.5
            }
        };
        XAxes = new Axis[] { new Axis { Labels = trendDays.Select(d => d.ToString("dd MMM")).ToArray(), IsVisible = false } };
        YAxes = new Axis[] { new Axis { MinLimit = 0, IsVisible = false } };

        // 2. Visit Type Analysis
        WalkInVisits = filteredAppointments.Count(a => a.ReasonOfVisit != null && a.ReasonOfVisit.ToLower().Contains("walk"));
        AppointmentVisits = filteredAppointments.Count - WalkInVisits;
        
        int totalAppts = filteredAppointments.Count;
        if (totalAppts > 0)
        {
            WalkInPercentage = $"{Math.Round((double)WalkInVisits / totalAppts * 100)}%";
            AppointmentPercentage = $"{Math.Round((double)AppointmentVisits / totalAppts * 100)}%";
        }

        VisitTypeSeries = new ISeries[]
        {
            new PieSeries<int> { Values = new[] { WalkInVisits }, Name = "Walk-in", Fill = new SolidColorPaint(new SKColor(245, 158, 11)), InnerRadius = 70 },
            new PieSeries<int> { Values = new[] { AppointmentVisits }, Name = "Appointment", Fill = new SolidColorPaint(new SKColor(37, 99, 235)), InnerRadius = 70 }
        };

        // 3. Appointment Analytics
        TotalAppointments = filteredAppointments.Count;
        CompletedAppointments = filteredAppointments.Count(a => a.Status == "Completed");
        CancelledAppointments = filteredAppointments.Count(a => a.Status == "Cancelled");
        NoShows = filteredAppointments.Count(a => a.Status == "No-Show");
        CompletionRate = TotalAppointments > 0 ? $"{Math.Round((double)CompletedAppointments / TotalAppointments * 100)}%" : "0%";

        AppointmentTrendsSeries = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Values = new[] { CompletedAppointments, CancelledAppointments, NoShows },
                Fill = new SolidColorPaint(new SKColor(37, 99, 235))
            }
        };

        // 4. Doctor Performance
        var drGroups = filteredAppointments.GroupBy(a => a.DoctorName ?? "Dr. Unknown").ToList();
        var drList = new List<DoctorPerformanceRow>();
        foreach (var group in drGroups)
        {
            int handled = group.Count();
            int completed = group.Count(a => a.Status == "Completed");
            drList.Add(new DoctorPerformanceRow
            {
                DoctorName = group.Key,
                TotalHandled = handled,
                Completed = completed,
                AveragePerDay = Math.Round((double)handled / totalDays, 1),
                Percentage = totalAppts > 0 ? Math.Round((double)handled / totalAppts * 100, 1) : 0
            });
        }
        DoctorPerformances = new ObservableCollection<DoctorPerformanceRow>(drList.OrderByDescending(d => d.TotalHandled));

        // 5. Visit Reasons
        var reasonGroups = filteredAppointments.Where(a => !string.IsNullOrEmpty(a.ReasonOfVisit))
                                               .GroupBy(a => a.ReasonOfVisit)
                                               .Select(g => new { Reason = g.Key!, Count = g.Count() })
                                               .OrderByDescending(g => g.Count)
                                               .Take(5)
                                               .ToList();
        
        VisitReasons = new ObservableCollection<VisitReasonRow>(reasonGroups.Select(g => new VisitReasonRow { Reason = g.Reason, Count = g.Count }));

        VisitReasonSeries = new ISeries[]
        {
            new RowSeries<int>
            {
                Values = reasonGroups.Select(g => g.Count).ToArray(),
                Fill = new SolidColorPaint(new SKColor(139, 92, 246)), // Purple
                MaxBarWidth = 24
            }
        };
        ReasonYAxes = new Axis[] { new Axis { Labels = reasonGroups.Select(g => g.Reason).ToArray(), LabelsPaint = new SolidColorPaint(new SKColor(15, 23, 42)) } };
        ReasonXAxes = new Axis[] { new Axis { MinLimit = 0 } };
    }

    private static DateTime[] BuildTrendDays(DateTime startDate, DateTime endDate)
    {
        var dayCount = Math.Max(1, (endDate - startDate).Days + 1);
        var maxPoints = Math.Min(dayCount, 30);
        var chartStart = endDate.AddDays(-(maxPoints - 1));
        return Enumerable.Range(0, maxPoints).Select(i => chartStart.AddDays(i)).ToArray();
    }
}
