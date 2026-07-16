using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using ClinicSystem.UI.Messages;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Dashboard;

/// <summary>
/// ViewModel for the Doctor's "Apply Discount" panel.
/// Lives inside the DashboardViewModel — doctor fills in the form
/// and fires a refund notification to the receptionist.
/// </summary>
public partial class DiscountRefundViewModel : ViewModelBase
{
    private readonly DiscountRefundRepository _repo;
    private readonly PatientRepository        _patientRepo;

    public DiscountRefundViewModel(
        DiscountRefundRepository repo,
        PatientRepository patientRepo)
    {
        _repo        = repo;
        _patientRepo = patientRepo;
    }

    // ── Form Fields ──────────────────────────────────────────────────────────
    [ObservableProperty] private string  _patientName    = string.Empty;
    [ObservableProperty] private string  _tokenNumber    = string.Empty;
    [ObservableProperty] private decimal _originalFee;
    [ObservableProperty] private decimal _discountedFee;
    [ObservableProperty] private string  _notes          = string.Empty;
    [ObservableProperty] private string  _statusMessage  = string.Empty;
    [ObservableProperty] private bool    _isSuccess;

    // Patient autocomplete source
    [ObservableProperty] private ObservableCollection<Patient> _patients = new();
    [ObservableProperty] private Patient? _selectedPatient;

    /// <summary>Live-computed refund amount shown as user types.</summary>
    public decimal ComputedRefund => Math.Max(0, OriginalFee - DiscountedFee);

    partial void OnOriginalFeeChanged(decimal value)   => OnPropertyChanged(nameof(ComputedRefund));
    partial void OnDiscountedFeeChanged(decimal value) => OnPropertyChanged(nameof(ComputedRefund));

    /// <summary>When a patient is picked from the dropdown, fill name and fee.</summary>
    partial void OnSelectedPatientChanged(Patient? value)
    {
        if (value == null) return;
        PatientName  = value.Name;
        OriginalFee  = value.ConsultationFee;
    }

    // ── Initialization ───────────────────────────────────────────────────────
    public async Task LoadPatientsAsync()
    {
        var list = await Task.Run(() => _patientRepo.GetAll());
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            Patients = new ObservableCollection<Patient>(
                list.OrderBy(p => p.Name)));
    }

    // ── Commands ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ApplyDiscountAsync()
    {
        // Validation
        if (string.IsNullOrWhiteSpace(PatientName))
        { StatusMessage = "Patient name is required."; IsSuccess = false; return; }

        if (OriginalFee <= 0)
        { StatusMessage = "Original fee must be greater than 0."; IsSuccess = false; return; }

        if (DiscountedFee < 0 || DiscountedFee >= OriginalFee)
        { StatusMessage = "Discounted fee must be less than the original fee."; IsSuccess = false; return; }

        var refund = new DiscountRefund
        {
            PatientName      = PatientName.Trim(),
            TokenNumber      = string.IsNullOrWhiteSpace(TokenNumber) ? null : TokenNumber.Trim(),
            OriginalFee      = OriginalFee,
            DiscountedFee    = DiscountedFee,
            Notes            = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            ApprovedByUserID = CurrentUser?.UserID,
            ApprovedByName   = CurrentUser?.FullName.Length > 0
                                   ? CurrentUser.FullName
                                   : CurrentUser?.Username ?? "Doctor",
            ApprovedAt       = DateTime.Now,
            IsCompleted      = false
        };

        try
        {
            await Task.Run(() => _repo.Insert(refund));

            // Broadcast to receptionist dashboard
            WeakReferenceMessenger.Default.Send(new RefundIssuedMessage
            {
                PatientName  = refund.PatientName,
                RefundAmount = refund.OriginalFee - refund.DiscountedFee
            });

            StatusMessage = $"✓ Refund of Rs. {ComputedRefund:N2} queued for {PatientName}. Receptionist has been notified.";
            IsSuccess     = true;

            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsSuccess     = false;
        }
    }

    [RelayCommand]
    private void Clear() => ClearForm();

    private void ClearForm()
    {
        SelectedPatient = null;
        PatientName     = string.Empty;
        TokenNumber     = string.Empty;
        OriginalFee     = 0;
        DiscountedFee   = 0;
        Notes           = string.Empty;
    }
}
