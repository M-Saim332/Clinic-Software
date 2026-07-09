using Avalonia;
using Avalonia.Controls;

namespace ClinicSystem.UI.Views.Components;

public partial class DataTableShellControl : UserControl

{
    public DataTableShellControl()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DataTableShellControl, string>(nameof(Title), "Records");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string> RecordCountProperty =
        AvaloniaProperty.Register<DataTableShellControl, string>(nameof(RecordCount), "0");

    public string RecordCount
    {
        get => GetValue(RecordCountProperty);
        set => SetValue(RecordCountProperty, value);
    }

    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<DataTableShellControl, object?>(nameof(HeaderActions));

    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    public static readonly StyledProperty<object?> FilterToolbarProperty =
        AvaloniaProperty.Register<DataTableShellControl, object?>(nameof(FilterToolbar));

    public object? FilterToolbar
    {
        get => GetValue(FilterToolbarProperty);
        set => SetValue(FilterToolbarProperty, value);
    }

    public static readonly StyledProperty<object?> TableContentProperty =
        AvaloniaProperty.Register<DataTableShellControl, object?>(nameof(TableContent));

    public object? TableContent
    {
        get => GetValue(TableContentProperty);
        set => SetValue(TableContentProperty, value);
    }
}
