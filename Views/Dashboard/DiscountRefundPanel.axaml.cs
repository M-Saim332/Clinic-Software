using Avalonia.Controls;
using ClinicSystem.UI.ViewModels.Dashboard;

namespace ClinicSystem.UI.Views.Dashboard;

public partial class DiscountRefundPanel : UserControl
{
    public DiscountRefundPanel()
    {
        InitializeComponent();
    }
    
    protected override async void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is DiscountRefundViewModel vm)
        {
            await vm.LoadPatientsAsync();
        }
    }
}
