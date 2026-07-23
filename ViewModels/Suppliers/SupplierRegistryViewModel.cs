using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Suppliers;

public partial class SupplierRegistryViewModel : ViewModelBase, ISearchable
{
    private readonly SupplierRepository _repo;
    private ObservableCollection<Supplier> _allSuppliers = new();

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

    [ObservableProperty]
    private string _searchTerm = string.Empty;
    public string SearchPlaceholder => "Search Suppliers...";

    partial void OnSearchTermChanged(string value) => FilterSuppliers();

    private void FilterSuppliers()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            Suppliers = new ObservableCollection<Supplier>(_allSuppliers);
        }
        else
        {
            var term = SearchTerm.ToLower().Replace(" ", "").Replace("-", "");
            Suppliers = new ObservableCollection<Supplier>(
                _allSuppliers.Where(s => s.Name.ToLower().Contains(term)
                                   || s.SupplierID.ToString().Contains(term)
                                   || (s.Phone?.ToLower().Replace(" ", "").Replace("-", "").Contains(term) ?? false)
                                   || (s.CNIC?.ToLower().Replace(" ", "").Replace("-", "").Contains(term) ?? false)
                                   || (s.Email?.ToLower().Contains(term) ?? false)));
        }
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
    [ObservableProperty]
    private string _cNIC = string.Empty;

    public bool MutationEnabled => Mode == FormMode.View;
    public bool SaveCancelEnabled => Mode != FormMode.View;
    public bool IsReadOnly => Mode == FormMode.Details || Mode == FormMode.View;
    public bool ShowSaveButton => Mode == FormMode.Add || Mode == FormMode.Edit;
    public bool ShowEditButton => Mode == FormMode.Details;

    [RelayCommand]
    private void New()
    {
        ClearFields();
        Mode = FormMode.Add;
        NotifyButtonStates();
        StatusMessage = "Enter new supplier details.";
    }

    [RelayCommand]
    private async Task DeleteSpecificAsync(Supplier s)
    {
        if (s == null) return;
        var ok = await Task.Run(() => _repo.Delete(s.SupplierID));
        StatusMessage = ok ? "Supplier deleted." : "Cannot delete – referenced by purchases.";
        if (ok)
        {
            LogActivity("Supplier Deleted", $"Supplier '{s.Name}' deleted", "Suppliers");
            await InitializeAsync();
        }
    }

    [RelayCommand]
    private void ViewSpecific(Supplier s)
    {
        if (s == null) return;
        SelectedSupplier = s;
        FillFields(s);
        Mode = FormMode.Details;
        NotifyButtonStates();
    }

    [RelayCommand]
    private void EditSpecific(Supplier s)
    {
        if (s == null) return;
        SelectedSupplier = s;
        FillFields(s);
        Mode = FormMode.Edit;
        NotifyButtonStates();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Name required."; return; }
        var s = new Supplier { Name = Name, Address = Address, Phone = Phone, Email = Email, CNIC = CNIC };
        if (Mode == FormMode.Add)
        {
            await Task.Run(() => _repo.Insert(s));
            StatusMessage = "Supplier created.";
            LogActivity("Supplier Added", $"New supplier '{s.Name}' added", "Suppliers");
        }
        else
        {
            s.SupplierID = SelectedSupplier!.SupplierID;
            await Task.Run(() => _repo.Update(s));
            StatusMessage = "Supplier updated.";
            LogActivity("Supplier Updated", $"Supplier '{s.Name}' updated", "Suppliers");
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
                _allSuppliers = new ObservableCollection<Supplier>(list);
                FilterSuppliers();
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
        Name = string.Empty; Address = string.Empty; Phone = string.Empty; Email = string.Empty; CNIC = string.Empty;
    }

    private void FillFields(Supplier s)
    {
        Name = s.Name; Address = s.Address ?? string.Empty; Phone = s.Phone ?? string.Empty; Email = s.Email ?? string.Empty; CNIC = s.CNIC ?? string.Empty;
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(MutationEnabled));
        OnPropertyChanged(nameof(SaveCancelEnabled));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(ShowSaveButton));
        OnPropertyChanged(nameof(ShowEditButton));
    }
}
