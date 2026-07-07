using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using System.Collections.ObjectModel;

namespace ClinicSystem.UI.ViewModels.Inventory;

public partial class InventoryViewModel : ViewModelBase
{
    private readonly MedicineRepository _medicineRepo;

    public InventoryViewModel(MedicineRepository medicineRepo)
    {
        _medicineRepo = medicineRepo;
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Medicine> _allStock = new();
    
    [ObservableProperty]
    private ObservableCollection<Medicine> _lowStock = new();
    
    [ObservableProperty]
    private ObservableCollection<Medicine> _outOfStock = new();
    
    [ObservableProperty]
    private ObservableCollection<Medicine> _expired = new();
    
    [ObservableProperty]
    private ObservableCollection<Medicine> _nearExpiry = new();

    // Adjustment fields
    [ObservableProperty]
    private Medicine? _selectedMedicine;
    [ObservableProperty]
    private int _adjustmentQuantity;
    [ObservableProperty]
    private string _adjustmentReason = string.Empty;

    public async Task InitializeAsync()
    {
        var medicines = await Task.Run(() => _medicineRepo.GetAll());
        var list = medicines.ToList();
        var today = DateTime.Today;

        Avalonia.Threading.Dispatcher.UIThread.Post(() => 
        {
            AllStock = new ObservableCollection<Medicine>(list.OrderBy(m => m.Name));
            
            LowStock = new ObservableCollection<Medicine>(
                list.Where(m => m.IsLowStock && m.Stock > 0 && !m.IsExpired).OrderBy(m => m.Stock));
                
            OutOfStock = new ObservableCollection<Medicine>(
                list.Where(m => m.Stock <= 0).OrderBy(m => m.Name));
                
            Expired = new ObservableCollection<Medicine>(
                list.Where(m => m.IsExpired).OrderBy(m => m.ExpiryDate));
                
            NearExpiry = new ObservableCollection<Medicine>(
                list.Where(m => m.ExpiryDate.HasValue && !m.IsExpired && m.ExpiryDate.Value <= today.AddDays(30))
                    .OrderBy(m => m.ExpiryDate));
        });
    }

    [RelayCommand]
    private async Task AdjustStockAsync()
    {
        if (SelectedMedicine == null)
        {
            StatusMessage = "Please select a medicine.";
            return;
        }

        if (AdjustmentQuantity == 0)
        {
            StatusMessage = "Quantity cannot be zero.";
            return;
        }

        if (SelectedMedicine.Stock + AdjustmentQuantity < 0)
        {
            StatusMessage = "Cannot adjust below zero stock.";
            return;
        }

        // Just adjusting the master stock record for now
        await Task.Run(() => _medicineRepo.AddStock(SelectedMedicine.MedicineID, AdjustmentQuantity));
        
        StatusMessage = $"Stock adjusted for {SelectedMedicine.Name} by {AdjustmentQuantity}.";
        
        SelectedMedicine = null;
        AdjustmentQuantity = 0;
        AdjustmentReason = string.Empty;
        
        await InitializeAsync();
    }
}
