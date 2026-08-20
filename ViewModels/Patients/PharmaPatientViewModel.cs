using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Patients;

public partial class PharmaPatientViewModel : ViewModelBase
{
    private readonly PatientRepository _repo;
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private string _name=string.Empty;
    [ObservableProperty] private string _phone=string.Empty;
    [ObservableProperty] private string _cNIC=string.Empty;
    [ObservableProperty] private string _statusMessage=string.Empty;

    public PharmaPatientViewModel(PatientRepository repo) => _repo=repo;

    public async Task InitializeAsync() => Patients=new ObservableCollection<Patient>(await Task.Run(() => _repo.GetAll("Pharma")));

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ClinicSystem.UI.Helpers.ValidationHelper.IsValidName(Name)) { StatusMessage="Enter a valid patient name."; return; }
        if (!ClinicSystem.UI.Helpers.ValidationHelper.IsValidPhone(Phone)) { StatusMessage="Enter an 11-digit phone number."; return; }
        if (!ClinicSystem.UI.Helpers.ValidationHelper.ValidateCNIC(CNIC,false)) { StatusMessage="CNIC must contain 13 digits."; return; }
        await Task.Run(() => _repo.Insert(new Patient { Name=Name.Trim(),Phone=Phone.Trim(),CNIC=string.IsNullOrWhiteSpace(CNIC)?null:CNIC.Trim(),PatientContext="Pharma" }));
        Name=Phone=CNIC=string.Empty; StatusMessage="Pharma patient saved."; await InitializeAsync();
    }
}
