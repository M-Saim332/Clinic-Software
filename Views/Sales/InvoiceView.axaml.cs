using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ClinicSystem.UI.Views.Sales;

public partial class InvoiceView : UserControl
{
    public InvoiceView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
