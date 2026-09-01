using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using ClinicSystem.UI.Services;

namespace ClinicSystem.UI.Views.Components;

public partial class ExportToolbarControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ExportToolbarControl, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<string> ReportNameProperty =
        AvaloniaProperty.Register<ExportToolbarControl, string>(nameof(ReportName), "Report");
    public static readonly StyledProperty<string> ColumnsProperty =
        AvaloniaProperty.Register<ExportToolbarControl, string>(nameof(Columns), string.Empty);
    public static readonly StyledProperty<string> FilterSummaryProperty =
        AvaloniaProperty.Register<ExportToolbarControl, string>(nameof(FilterSummary), string.Empty);

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string ReportName { get => GetValue(ReportNameProperty); set => SetValue(ReportNameProperty, value); }
    public string Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public string FilterSummary { get => GetValue(FilterSummaryProperty); set => SetValue(FilterSummaryProperty, value); }

    public ExportToolbarControl() => InitializeComponent();

    private async void OnExportCsvClick(object? sender, RoutedEventArgs e)
    {
        var rows = GetRows();
        var columns = GetColumns();
        if (rows.Count == 0 || columns.Count == 0) return;

        var file = await PickFileAsync("csv");
        if (file == null) return;

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", columns.Select(c => Csv(c.Header))));
        foreach (var row in rows)
            csv.AppendLine(string.Join(",", columns.Select(c => Csv(ReadPath(row, c.Path)))));
        csv.AppendLine();
        csv.AppendLine($"{Csv("Total Rows")},{Csv(rows.Count.ToString(CultureInfo.CurrentCulture))}");

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteAsync(csv.ToString());
    }

    private async void OnExportPdfClick(object? sender, RoutedEventArgs e)
    {
        var rows = GetRows();
        var columns = GetColumns();
        if (rows.Count == 0 || columns.Count == 0) return;

        var file = await PickFileAsync("pdf");
        if (file == null) return;

        await using var stream = await file.OpenWriteAsync();
        var title = ReportName;
        var clinicName = ClinicBrandingService.ClinicName;
        var filters = string.IsNullOrWhiteSpace(FilterSummary) ? "Current on-screen rows" : FilterSummary;
        var exportedAt = DateTime.Now.ToString("dd MMM yyyy, hh:mm tt", CultureInfo.CurrentCulture);

        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken3));
            page.Header().Column(header =>
            {
                header.Item().Background(Colors.Blue.Darken2).Padding(10).Column(col =>
                {
                    col.Item().Text(clinicName).FontSize(16).Bold().FontColor(Colors.White);
                    col.Item().Text(title).FontSize(11).FontColor(Colors.White);
                });
                header.Item().PaddingTop(6).Text($"Filters: {filters}    Exported: {exportedAt}").FontSize(8);
            });
            page.Content().PaddingVertical(10).Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    foreach (var column in columns) def.RelativeColumn(column.Weight);
                });
                table.Header(header =>
                {
                    foreach (var column in columns)
                        header.Cell().Element(HeaderCell).Text(column.Header);
                });
                foreach (var row in rows)
                {
                    foreach (var column in columns)
                        table.Cell().Element(BodyCell).Text(ReadPath(row, column.Path));
                }
                table.Cell().ColumnSpan((uint)columns.Count).Element(SummaryCell)
                    .Text($"Total Rows: {rows.Count}");
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf(stream);
    }

    private async Task<IStorageFile?> PickFileAsync(string ext)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;
        var storage = desktop.MainWindow?.StorageProvider;
        if (storage == null) return null;
        var safe = string.Join("_", ReportName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Replace(" ", "_");
        var type = ext == "pdf"
            ? new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } }
            : new FilePickerFileType("CSV file") { Patterns = new[] { "*.csv" } };
        return await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Export {ReportName}",
            SuggestedFileName = $"{safe}_{DateTime.Now:yyyy-MM-dd}.{ext}",
            DefaultExtension = ext,
            FileTypeChoices = new[] { type }
        });
    }

    private List<object> GetRows() => ItemsSource?.Cast<object>().ToList() ?? new List<object>();

    private List<ExportColumn> GetColumns()
    {
        return Columns.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part =>
            {
                var pieces = part.Split(':', StringSplitOptions.TrimEntries);
                if (pieces.Length < 2) return null;
                var weight = pieces.Length > 2 && float.TryParse(pieces[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 1f;
                return new ExportColumn(pieces[0], pieces[1], weight);
            })
            .Where(c => c != null)
            .Cast<ExportColumn>()
            .ToList();
    }

    private static string ReadPath(object row, string path)
    {
        object? current = row;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current == null) return string.Empty;
            current = current.GetType().GetProperty(segment, BindingFlags.Public | BindingFlags.Instance)?.GetValue(current);
        }
        return Format(current);
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
        decimal money => money.ToString("0.##", CultureInfo.CurrentCulture),
        double number => number.ToString("0.##", CultureInfo.CurrentCulture),
        float number => number.ToString("0.##", CultureInfo.CurrentCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r')
            ? $"\"{escaped}\""
            : escaped;
    }

    private static IContainer HeaderCell(IContainer container) => container
        .Background(Colors.Grey.Darken3).Border(0.5f).BorderColor(Colors.Grey.Darken2)
        .PaddingVertical(5).PaddingHorizontal(4)
        .DefaultTextStyle(style => style.FontColor(Colors.White).Bold().FontSize(7));

    private static IContainer BodyCell(IContainer container) => container
        .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
        .PaddingVertical(4).PaddingHorizontal(4);

    private static IContainer SummaryCell(IContainer container) => container
        .Border(0.5f).BorderColor(Colors.Grey.Darken2)
        .Background(Colors.Grey.Lighten4)
        .PaddingVertical(6).PaddingHorizontal(4)
        .DefaultTextStyle(style => style.Bold().FontSize(8));

    private sealed record ExportColumn(string Header, string Path, float Weight);
}
