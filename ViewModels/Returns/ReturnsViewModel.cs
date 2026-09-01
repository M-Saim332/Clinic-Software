using ClinicSystem.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ClinicSystem.UI.Messages;
using System.Threading.Tasks;

namespace ClinicSystem.UI.ViewModels.Returns;

/// <summary>
/// Thin wrapper that owns the two tab sub-ViewModels and tracks which tab is active.
/// This is what the sidebar's single "Returns" button navigates to.
/// </summary>
public partial class ReturnsViewModel : ViewModelBase, ISearchable
{
    public ProcessReturnViewModel  ProcessReturn  { get; }
    public ReturnHistoryViewModel  ReturnHistory  { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProcessReturnTab))]
    [NotifyPropertyChangedFor(nameof(IsReturnHistoryTab))]
    private string _activeTab = "ProcessReturn";

    public bool IsProcessReturnTab  => ActiveTab == "ProcessReturn";
    public bool IsReturnHistoryTab  => ActiveTab == "ReturnHistory";

    public string SearchPlaceholder => "Search returns…";
    private string _initializationError = string.Empty;
    public string StatusMessage => !string.IsNullOrWhiteSpace(_initializationError)
        ? _initializationError
        : IsReturnHistoryTab ? ReturnHistory.StatusMessage : ProcessReturn.StatusMessage;

    [ObservableProperty] private string _searchTerm = string.Empty;

    public ReturnsViewModel(ProcessReturnViewModel processReturn, ReturnHistoryViewModel returnHistory)
    {
        ProcessReturn = processReturn;
        ReturnHistory = returnHistory;

        WeakReferenceMessenger.Default.Register<ReturnsViewModel, RefundIssuedMessage>(this, (r, m) =>
        {
            _ = r.ReturnHistory.InitializeAsync();
        });
        WeakReferenceMessenger.Default.Register<ReturnsViewModel, RefundCompletedMessage>(this, (r, m) =>
        {
            _ = r.ReturnHistory.InitializeAsync();
        });
    }

    public Task InitializeAsync() => LoadInitialDataAsync();

    /// <summary>
    /// Loads both tabs without letting a database or migration failure destabilize
    /// the Avalonia navigation visual tree. Each child ViewModel also reports its
    /// own detailed load error in its status area.
    /// </summary>
    public async Task LoadInitialDataAsync()
    {
        try
        {
            _initializationError = string.Empty;
            await Task.WhenAll(
                ProcessReturn.InitializeAsync(),
                ReturnHistory.InitializeAsync()
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RETURNS INIT ERROR] {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            _initializationError = $"Returns data could not be loaded: {ex.Message}";
        }
        finally
        {
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    partial void OnActiveTabChanged(string value)
    {
        if (value == "ReturnHistory")
        {
            _ = ReturnHistory.InitializeAsync();
        }
    }

    [RelayCommand] private void SwitchToProcessReturn() => ActiveTab = "ProcessReturn";
    [RelayCommand] private void SwitchToReturnHistory() => ActiveTab = "ReturnHistory";
}
