using Avalonia.Markup.Xaml;
using Avalonia.Controls;

namespace ClinicSystem.UI.Views.Users;

public partial class ChangePasswordView : UserControl
{
    public ChangePasswordView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
