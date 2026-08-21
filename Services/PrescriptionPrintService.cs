using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClinicSystem.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClinicSystem.UI.Services;

public static class PrescriptionPrintService
{
    public static async Task<bool> ExportAsync(Prescription prescription)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
            return false;

        var safeName = string.Join("-", (prescription.PatientName ?? "patient").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Print Patient Prescription",
            SuggestedFileName = $"{safeName}-prescription-{prescription.VisitDate:yyyyMMdd}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } } }
        });
        if (file == null) return false;

        await using var stream = await file.OpenWriteAsync();
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(42);
            page.DefaultTextStyle(style => style.FontSize(10).FontColor(Colors.Grey.Darken3));
            page.Header().Column(header =>
            {
                header.Item().Text("PATIENT PRESCRIPTION").FontSize(22).Bold().FontColor(Colors.Blue.Darken2);
                header.Item().PaddingTop(4).Text($"Prescription #{(prescription.PrescriptionID > 0 ? prescription.PrescriptionID : "New")}")
                    .FontSize(9).FontColor(Colors.Grey.Medium);
            });
            page.Content().PaddingVertical(20).Column(column =>
            {
                column.Spacing(14);
                column.Item().Background(Colors.Grey.Lighten4).Padding(14).Row(row =>
                {
                    row.RelativeItem().Column(info =>
                    {
                        info.Item().Text("PATIENT").FontSize(9).Bold().FontColor(Colors.Grey.Medium);
                        info.Item().Text(prescription.PatientName ?? "—").FontSize(15).Bold();
                        info.Item().Text($"Age: {prescription.PatientAge?.ToString() ?? "—"}   Gender: {prescription.PatientGender ?? "—"}");
                        info.Item().Text($"Phone: {prescription.PatientPhone ?? "—"}");
                    });
                    row.RelativeItem().AlignRight().Column(info =>
                    {
                        info.Item().AlignRight().Text($"Date: {prescription.VisitDate:dd MMM yyyy}");
                        info.Item().AlignRight().Text($"Doctor: {prescription.DoctorName ?? "—"}");
                    });
                });

                if (!string.IsNullOrWhiteSpace(prescription.Diagnosis))
                    column.Item().Text(text => { text.Span("Diagnosis: ").Bold(); text.Span(prescription.Diagnosis); });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(34);
                        columns.RelativeColumn(3);
                        columns.ConstantColumn(55);
                        columns.RelativeColumn(3);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("#");
                        header.Cell().Element(HeaderCell).Text("MEDICINE");
                        header.Cell().Element(HeaderCell).Text("QTY");
                        header.Cell().Element(HeaderCell).Text("DOSAGE / DIRECTIONS");
                    });
                    var index = 1;
                    foreach (var item in prescription.Items)
                    {
                        table.Cell().Element(BodyCell).Text(index++.ToString());
                        table.Cell().Element(BodyCell).Text(item.ProductName ?? "Medicine");
                        table.Cell().Element(BodyCell).Text(item.Quantity.ToString());
                        table.Cell().Element(BodyCell).Text(item.Dosage ?? "—");
                    }
                });

                if (!string.IsNullOrWhiteSpace(prescription.Notes))
                    column.Item().Text(text => { text.Span("Notes: ").Bold(); text.Span(prescription.Notes); });

                column.Item().PaddingTop(30).AlignRight().Width(180).BorderTop(1).BorderColor(Colors.Grey.Medium)
                    .PaddingTop(6).AlignCenter().Text("Doctor's signature").FontSize(9);
            });
            page.Footer().AlignCenter().Text("Please follow the prescribed dosage and consult your doctor if symptoms persist.")
                .FontSize(8).FontColor(Colors.Grey.Medium);
        })).GeneratePdf(stream);
        return true;
    }

    private static IContainer HeaderCell(IContainer container) => container
        .Background(Colors.Blue.Darken2).PaddingVertical(8).PaddingHorizontal(6)
        .DefaultTextStyle(style => style.FontColor(Colors.White).Bold().FontSize(9));

    private static IContainer BodyCell(IContainer container) => container
        .BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(9).PaddingHorizontal(6);
}
