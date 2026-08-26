using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ClinicSystem.UI.ViewModels.Returns;

public partial class ReturnHistoryViewModel : ViewModelBase, ISearchable
{
    private readonly ReturnRepository _returnRepo;

    public ReturnHistoryViewModel(ReturnRepository returnRepo)
    {
        _returnRepo = returnRepo;
    }

    [ObservableProperty] private ObservableCollection<ProductReturn> _returns = new();
    private ObservableCollection<ProductReturn> _allReturns = new();
    [ObservableProperty] private ProductReturn? _selectedReturn;
    
    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string SearchPlaceholder => "Search returns...";

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            var returns = await Task.Run(_returnRepo.GetAll);
            _allReturns = new ObservableCollection<ProductReturn>(returns);
            FilterReturns();
            StatusMessage = $"{Returns.Count} return(s) loaded.";
        }
        catch (Exception ex) { StatusMessage = $"Failed to load returns: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateReturnInvoiceAsync()
    {
        if (SelectedReturn == null) { StatusMessage = "Select a return first."; return; }
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        var file = await desktop.MainWindow!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Return Invoice",
            SuggestedFileName = $"{SelectedReturn.ReturnNo}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } } }
        });
        if (file == null) return;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            var r = SelectedReturn;
            Document.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Header().Text("MEDICINE RETURN INVOICE").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Return No: {r.ReturnNo}");
                    col.Item().Text($"Date: {r.CreatedAt:dd MMM yyyy hh:mm tt}");
                    col.Item().Text($"Type: {r.ReturnType}");
                    foreach (var item in r.Items)
                        col.Item().Text($"{item.ProductName}: {item.Quantity} pieces — Rs. {item.RefundAmount:N2}");
                    col.Item().Text($"Total pieces: {r.Quantity}");
                    col.Item().Text($"Patient / Supplier: {r.PatientName ?? r.SupplierName ?? "N/A"}");
                    col.Item().Text($"Reason: {r.Reason}");
                    col.Item().Text($"Refund / Credit: Rs. {r.RefundAmount:N2}").FontSize(16).Bold();
                    col.Item().Text($"Processed by: {r.CreatedByName ?? (CurrentUser?.DisplayName ?? "Unknown")}");
                });
            })).GeneratePdf(stream);
            StatusMessage = "Return invoice exported.";
        }
        catch (Exception ex) { StatusMessage = $"Invoice export failed: {ex.Message}"; }
    }

    partial void OnSearchTermChanged(string value) => FilterReturns();

    private void FilterReturns()
    {
        var term = SearchTerm?.Trim() ?? string.Empty;
        Returns = new ObservableCollection<ProductReturn>(string.IsNullOrEmpty(term) ? _allReturns
            : _allReturns.Where(r =>
                r.ReturnNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.ProductName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.PatientName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.SupplierName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)));
    }
}
