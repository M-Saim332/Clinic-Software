using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ClinicSystem.UI.Views.Components;

public partial class SummaryCardControl : UserControl
{
    public SummaryCardControl()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SummaryCardControl, string>(nameof(Title), string.Empty);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<SummaryCardControl, string>(nameof(Value), string.Empty);

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<IBrush> BorderColorProperty =
        AvaloniaProperty.Register<SummaryCardControl, IBrush>(nameof(BorderColor), Brushes.Blue);

    public IBrush BorderColor
    {
        get => GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }
}
