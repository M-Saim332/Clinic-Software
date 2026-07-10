using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Suppliers;

public partial class SupplierRegistryViewModel : ViewModelBase
{
    private readonly SupplierRepository _repo;

    public SupplierRegistryViewModel(SupplierRepository repo)
    {
        _repo = repo;
    }

    [ObservableProperty]
    private FormMode _mode = FormMode.View;
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Supplier> _suppliers = new();
    [ObservableProperty]
    private Supplier? _selectedSupplier;

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

    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Enter new supplier details.";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedSupplier == null) { StatusMessage = "Select a supplier first."; return; }
        FillFields(SelectedSupplier);
        Mode = FormMode.Edit;
        NotifyButtonStates();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedSupplier == null) { StatusMessage = "Select a supplier first."; return; }
        var ok = await Task.Run(() => _repo.Delete(SelectedSupplier.SupplierID));
        StatusMessage = ok ? "Supplier deleted." : "Cannot delete – referenced by purchases.";
        if (ok) await InitializeAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Name required."; return; }
        var s = new Supplier { Name = Name, Address = Address, Phone = Phone, Email = Email };
        if (Mode == FormMode.Add)
        {
            await Task.Run(() => _repo.Insert(s));
            StatusMessage = "Supplier created.";
        }
        else
        {
            s.SupplierID = SelectedSupplier!.SupplierID;
            await Task.Run(() => _repo.Update(s));
            StatusMessage = "Supplier updated.";
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
                Suppliers = new ObservableCollection<Supplier>(list);
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                StatusMessage = $"Failed to load suppliers: {ex.Message}");
        }
    }

    private void ClearFields()
    {
        Name = string.Empty; Address = string.Empty; Phone = string.Empty; Email = string.Empty;
    }

    private void FillFields(Supplier s)
    {
        Name = s.Name; Address = s.Address ?? string.Empty; Phone = s.Phone ?? string.Empty; Email = s.Email ?? string.Empty;
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
    }
}
