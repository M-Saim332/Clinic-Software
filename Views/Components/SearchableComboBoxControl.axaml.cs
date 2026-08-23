using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ClinicSystem.UI.Views.Components;

public partial class SearchableComboBoxControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SearchableComboBoxControl, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<SearchableComboBoxControl, object?>(nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<string> DisplayMemberPathProperty =
        AvaloniaProperty.Register<SearchableComboBoxControl, string>(nameof(DisplayMemberPath), "Name");
    public static readonly StyledProperty<string> DetailMemberPathProperty =
        AvaloniaProperty.Register<SearchableComboBoxControl, string>(nameof(DetailMemberPath), string.Empty);
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<SearchableComboBoxControl, string>(nameof(Placeholder), "Search and select...");

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SearchableComboBoxControl, string>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private readonly ObservableCollection<SearchableOption> _options = new();
    private bool _synchronizing;

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public string DisplayMemberPath { get => GetValue(DisplayMemberPathProperty); set => SetValue(DisplayMemberPathProperty, value); }
    public string DetailMemberPath { get => GetValue(DetailMemberPathProperty); set => SetValue(DetailMemberPathProperty, value); }
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    static SearchableComboBoxControl()
    {
        TextProperty.Changed.AddClassHandler<SearchableComboBoxControl>((c, _) => {
            if (!c._synchronizing && c.FindControl<TextBox>("SearchBox") is { } box && box.Text != c.Text)
                box.Text = c.Text;
        });
        ItemsSourceProperty.Changed.AddClassHandler<SearchableComboBoxControl>((c, _) => c.RefreshOptions());
        SelectedItemProperty.Changed.AddClassHandler<SearchableComboBoxControl>((c, _) => c.SyncSelection(updateText: true));
        DisplayMemberPathProperty.Changed.AddClassHandler<SearchableComboBoxControl>((c, _) => c.RefreshOptions());
        DetailMemberPathProperty.Changed.AddClassHandler<SearchableComboBoxControl>((c, _) => c.RefreshOptions());
        PlaceholderProperty.Changed.AddClassHandler<SearchableComboBoxControl>((c, _) =>
        {
            if (c.FindControl<TextBox>("SearchBox") is { } box) box.PlaceholderText = c.Placeholder;
        });
    }

    public SearchableComboBoxControl()
    {
        InitializeComponent();
        OptionsList.ItemsSource = _options;
        AttachedToVisualTree += (_, _) => { SearchBox.PlaceholderText = Placeholder; RefreshOptions(); };
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_synchronizing) return;
        Text = SearchBox.Text;
        RefreshOptions(SearchBox.Text);
        OptionsPopup.IsOpen = true;
    }

    private void OnSearchBoxGotFocus(object? sender, RoutedEventArgs e)
    {
        RefreshOptions(SearchBox.Text);
        OptionsPopup.IsOpen = true;
    }
    
    private void OnSearchBoxPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        RefreshOptions(SearchBox.Text);
        OptionsPopup.IsOpen = true;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_synchronizing || OptionsList.SelectedItem is not SearchableOption option) return;
        _synchronizing = true;
        SelectedItem = option.Value;
        SearchBox.Text = option.Label;
        Text = option.Label;
        OptionsPopup.IsOpen = false;
        _synchronizing = false;
    }

    private void RefreshOptions(string? search = null)
    {
        if (_synchronizing) return;
        var query = search?.Trim() ?? string.Empty;
        _options.Clear();
        if (ItemsSource == null) return;
        foreach (var value in ItemsSource.Cast<object>())
        {
            var label = Read(value, DisplayMemberPath);
            var detail = Read(value, DetailMemberPath);
            if (query.Length == 0 || label.Contains(query, StringComparison.OrdinalIgnoreCase) || detail.Contains(query, StringComparison.OrdinalIgnoreCase))
                _options.Add(new SearchableOption(value, label, detail));
        }
        SyncSelection(updateText: false);
    }

    private void SyncSelection(bool updateText = true)
    {
        if (_synchronizing) return;
        _synchronizing = true;
        OptionsList.SelectedItem = _options.FirstOrDefault(o => ReferenceEquals(o.Value, SelectedItem) || Equals(o.Value, SelectedItem));
        if (updateText) 
        {
            var newText = OptionsList.SelectedItem is SearchableOption option ? option.Label : string.Empty;
            SearchBox.Text = newText;
            Text = newText;
        }
        _synchronizing = false;
    }

    private static string Read(object value, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return value.ToString() ?? string.Empty;
        return value.GetType().GetProperty(path, BindingFlags.Public | BindingFlags.Instance)?.GetValue(value)?.ToString() ?? string.Empty;
    }

    private sealed record SearchableOption(object Value, string Label, string Detail);
}
