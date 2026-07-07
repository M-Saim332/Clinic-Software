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
using ClinicSystem.UI.ViewModels.Companies;
using ClinicSystem.UI.ViewModels.Suppliers;
using ClinicSystem.UI.ViewModels.Products;
using ClinicSystem.UI.ViewModels.Appointments;
using ClinicSystem.UI.ViewModels.Purchases;
using ClinicSystem.UI.ViewModels.Sales;
using ClinicSystem.UI.ViewModels.Inventory;
using ClinicSystem.UI.ViewModels.Dashboard;
using ClinicSystem.UI.Views.Companies;
using ClinicSystem.UI.Views.Suppliers;
using ClinicSystem.UI.Views.Products;
using ClinicSystem.UI.Views.Appointments;
using ClinicSystem.UI.Views.Purchases;
using ClinicSystem.UI.Views.Sales;
using ClinicSystem.UI.Views.Inventory;
using ClinicSystem.UI.Views.Dashboard;

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
            DashboardViewModel         => new DashboardView         { DataContext = param },
            CompanyRegistryViewModel   => new CompanyRegistryView   { DataContext = param },
            SupplierRegistryViewModel  => new SupplierRegistryView  { DataContext = param },
            ProductRegistryViewModel   => new ProductRegistryView   { DataContext = param },
            AppointmentViewModel       => new AppointmentView       { DataContext = param },
            PurchaseViewModel          => new PurchaseView          { DataContext = param },
            SaleViewModel              => new SaleView              { DataContext = param },
            InventoryViewModel         => new InventoryView         { DataContext = param },
            _ => new TextBlock { Text = $"No view for {param?.GetType().Name}" }
        };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
