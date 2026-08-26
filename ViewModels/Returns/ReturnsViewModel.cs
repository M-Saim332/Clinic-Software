using ClinicSystem.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    public string StatusMessage     => IsReturnHistoryTab ? ReturnHistory.StatusMessage : ProcessReturn.StatusMessage;

    [ObservableProperty] private string _searchTerm = string.Empty;

    public ReturnsViewModel(ProcessReturnViewModel processReturn, ReturnHistoryViewModel returnHistory)
    {
        ProcessReturn = processReturn;
        ReturnHistory = returnHistory;
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            ProcessReturn.InitializeAsync(),
            ReturnHistory.InitializeAsync()
        );
    }

    [RelayCommand] private void SwitchToProcessReturn() => ActiveTab = "ProcessReturn";
    [RelayCommand] private void SwitchToReturnHistory() => ActiveTab = "ReturnHistory";
}
