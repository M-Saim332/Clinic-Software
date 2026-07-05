using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ClinicSystem.UI.ViewModels;
using ClinicSystem.UI.ViewModels.Patients;
using ClinicSystem.UI.ViewModels.Medicines;
using ClinicSystem.UI.ViewModels.Prescriptions;
using ClinicSystem.UI.ViewModels.Users;
using ClinicSystem.UI.ViewModels.Reports;
using ClinicSystem.UI.Views.Patients;
using ClinicSystem.UI.Views.Medicines;
using ClinicSystem.UI.Views.Prescriptions;
using ClinicSystem.UI.Views.Users;
using ClinicSystem.UI.Views.Reports;

namespace ClinicSystem.UI;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        return param switch
        {
            PatientRegistryViewModel   => new PatientRegistryView   { DataContext = param },
            MedicineRegistryViewModel  => new MedicineRegistryView  { DataContext = param },
            PrescriptionViewModel      => new PrescriptionView      { DataContext = param },
            VisitHistoryViewModel      => new VisitHistoryView      { DataContext = param },
            UserRegistryViewModel      => new UserRegistryView      { DataContext = param },
            ReportsViewModel           => new ReportsView           { DataContext = param },
            _ => new TextBlock { Text = $"No view for {param?.GetType().Name}" }
        };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
