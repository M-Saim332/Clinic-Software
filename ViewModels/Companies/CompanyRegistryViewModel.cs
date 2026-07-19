using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Companies;

public partial class CompanyRegistryViewModel : ViewModelBase
{
    private readonly CompanyRepository _repo;

    public CompanyRegistryViewModel(CompanyRepository repo)
    {
        _repo = repo;
    }

    [ObservableProperty]
    private FormMode _mode = FormMode.View;
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Company> _companies = new();
    [ObservableProperty]
    private Company? _selectedCompany;
    
    partial void OnSelectedCompanyChanged(Company? value)
    {
        StatusMessage = string.Empty;
    }

    // Fields for editing/adding
    [ObservableProperty]
    private string _name = string.Empty;
    [ObservableProperty]
    private string _address = string.Empty;
    [ObservableProperty]
    private string _phone = string.Empty;
    [ObservableProperty]
    private string _email = string.Empty;

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;

    // ── Delete confirmation state ──────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingDeleteLabel))]
    private Company? _pendingDeleteCompany;
    [ObservableProperty] private bool _showDeleteConfirm;
    public string PendingDeleteLabel => PendingDeleteCompany is { } c ? c.Name : string.Empty;

    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Enter new company details.";
    }

    // ── Row-level commands (match Patients pattern) ────────────────────
    [RelayCommand]
    private void EditSpecific(Company company)
    {
        if (company == null) return;
        SelectedCompany = company;
        FillFields(company);
        Mode = FormMode.Edit;
        NotifyButtonStates();
        StatusMessage = "Edit company details and click Save.";
    }

    [RelayCommand]
    private void RequestDeleteSpecific(Company company)
    {
        if (company == null) return;
        PendingDeleteCompany = company;
        ShowDeleteConfirm = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        var target = PendingDeleteCompany;
        ShowDeleteConfirm = false;
        PendingDeleteCompany = null;
        if (target == null) return;

        var ok = await Task.Run(() => _repo.Delete(target.CompanyID));
        StatusMessage = ok ? "Company deleted." : "Cannot delete — company is referenced by products.";
        if (ok)
        {
            LogActivity("Company Deleted", $"Company '{target.Name}' deleted", "Companies");
            if (SelectedCompany?.CompanyID == target.CompanyID) SelectedCompany = null;
            await InitializeAsync();
        }
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ShowDeleteConfirm = false;
        PendingDeleteCompany = null;
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedCompany == null) { StatusMessage = "Select a company first."; return; }
        EditSpecific(SelectedCompany);
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedCompany == null) { StatusMessage = "Select a company first."; return; }
        RequestDeleteSpecificCommand.Execute(SelectedCompany);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Name required."; return; }
        var c = new Company { Name = Name, Address = Address, Phone = Phone, Email = Email };
        if (Mode == FormMode.Add)
        {
            await Task.Run(() => _repo.Insert(c));
            StatusMessage = "Company created.";
            LogActivity("Company Added", $"New company '{c.Name}' added", "Companies");
        }
        else
        {
            c.CompanyID = SelectedCompany!.CompanyID;
            await Task.Run(() => _repo.Update(c));
            StatusMessage = "Company updated.";
            LogActivity("Company Updated", $"Company '{c.Name}' updated", "Companies");
        }
        Mode = FormMode.View;
        NotifyButtonStates();
        await InitializeAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        Mode = FormMode.View;
        NotifyButtonStates();
        StatusMessage = string.Empty;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var list = await Task.Run(() => _repo.GetAll());
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = string.Empty;
                Companies = new ObservableCollection<Company>(list);
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load companies: {ex.Message}");
        }
    }

    private void ClearFields()
    {
        Name = string.Empty; Address = string.Empty; Phone = string.Empty; Email = string.Empty;
    }

    private void FillFields(Company c)
    {
        Name = c.Name; Address = c.Address ?? string.Empty; Phone = c.Phone ?? string.Empty; Email = c.Email ?? string.Empty;
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }
}
