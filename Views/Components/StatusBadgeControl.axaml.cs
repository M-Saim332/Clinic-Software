using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ClinicSystem.UI.Views.Components;

public partial class StatusBadgeControl : UserControl
{
    public StatusBadgeControl()
    {
        InitializeComponent();
        UpdateStyle();
    }

    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<StatusBadgeControl, string>(nameof(Status), string.Empty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            UpdateStyle();
        }
    }

    public string Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly StyledProperty<IBrush> BadgeBackgroundProperty =
        AvaloniaProperty.Register<StatusBadgeControl, IBrush>(nameof(BadgeBackground), Brushes.Transparent);

    public IBrush BadgeBackground
    {
        get => GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    public static readonly StyledProperty<IBrush> BadgeForegroundProperty =
        AvaloniaProperty.Register<StatusBadgeControl, IBrush>(nameof(BadgeForeground), Brushes.Black);

    public IBrush BadgeForeground
    {
        get => GetValue(BadgeForegroundProperty);
        set => SetValue(BadgeForegroundProperty, value);
    }

    private void UpdateStyle()
    {
        var status = Status;
        if (string.IsNullOrWhiteSpace(status))
        {
            BadgeBackground = Brushes.Transparent;
            BadgeForeground = Brushes.Black;
            return;
        }

        var s = status.Trim().ToLowerInvariant();

        // Green/Teal
        if (s == "completed" || s == "active" || s == "success" || s == "checked-in" || s == "in stock" || s == "true" || s == "yes")
        {
            BadgeBackground = BrushFromHex("#F0FDFA");
            BadgeForeground = BrushFromHex("#0F766E");
        }
        // Red/Rose
        else if (s == "cancelled" || s == "no-show" || s == "expired" || s == "out of stock" || s == "inactive" || s == "danger" || s == "false" || s == "no")
        {
            BadgeBackground = BrushFromHex("#FFF1F2");
            BadgeForeground = BrushFromHex("#E11D48");
        }
        // Amber
        else if (s == "low stock" || s == "expiring soon" || s == "warning" || s == "pending")
        {
            BadgeBackground = BrushFromHex("#FFFBEB");
            BadgeForeground = BrushFromHex("#B45309");
        }
        // Blue (default)
        else
        {
            BadgeBackground = BrushFromHex("#EFF6FF");
            BadgeForeground = BrushFromHex("#2563EB");
        }
    }

    private IBrush BrushFromHex(string hex)
    {
        if (Color.TryParse(hex, out var color))
        {
            return new SolidColorBrush(color);
        }
        return Brushes.Gray;
    }
}
