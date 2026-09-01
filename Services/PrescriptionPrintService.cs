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

        var clinicName = ClinicBrandingService.ClinicName;
        var exportedAt = DateTime.Now.ToString("dd MMM yyyy, hh:mm tt");
        await using var stream = await file.OpenWriteAsync();
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(42);
            page.DefaultTextStyle(style => style.FontSize(10).FontColor(Colors.Grey.Darken3));

            // ── Header ───────────────────────────────────────────────────
            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Column(clinic =>
                    {
                        clinic.Item().Text(clinicName)
                            .FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                        clinic.Item().PaddingTop(2)
                            .Text($"Patient Prescription | Exported: {exportedAt}")
                            .FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                    row.AutoItem().AlignRight().Column(apptCol =>
                    {
                        if (prescription.AppointmentID.HasValue)
                        {
                            apptCol.Item().AlignRight()
                                .Text($"Appointment: APT-{prescription.AppointmentID}")
                                .FontSize(9).FontColor(Colors.Blue.Darken1).Bold();
                        }
                        apptCol.Item().AlignRight()
                            .Text($"Date: {prescription.VisitDate:dd MMM yyyy}")
                            .FontSize(10);
                    });
                });
                header.Item().PaddingTop(6).BorderBottom(1).BorderColor(Colors.Blue.Darken2);
            });

            // ── Content ───────────────────────────────────────────────────
            page.Content().PaddingVertical(16).Column(column =>
            {
                column.Spacing(14);

                // Patient + Doctor info panel
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
                        info.Item().AlignRight().Text("DOCTOR").FontSize(9).Bold().FontColor(Colors.Grey.Medium);
                        info.Item().AlignRight().Text(prescription.DoctorName ?? "—").FontSize(13).Bold();
                        info.Item().AlignRight().Text($"Time: {prescription.VisitDate:hh:mm tt}").FontSize(9);
                    });
                });

                // Medicines table
                column.Item().Text("PRESCRIBED MEDICINES").FontSize(9).Bold().FontColor(Colors.Grey.Medium);
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
                        table.Cell().Element(BodyCell).Text(
                            string.IsNullOrWhiteSpace(item.Dosage) ? "—" : item.Dosage);
                    }
                });

                // Lab Tests section (only when ordered)
                if (!string.IsNullOrWhiteSpace(prescription.LabTests))
                {
                    column.Item().Text("LAB TESTS ORDERED").FontSize(9).Bold().FontColor(Colors.Grey.Medium);
                    column.Item().Background(Colors.Yellow.Lighten4).Padding(12).Text(prescription.LabTests)
                        .FontSize(10);
                }

                // Doctor signature line
                column.Item().PaddingTop(30).AlignRight().Width(180)
                    .BorderTop(1).BorderColor(Colors.Grey.Medium)
                    .PaddingTop(6).AlignCenter().Text("Doctor's Signature").FontSize(9);
            });

            // ── Footer ────────────────────────────────────────────────────
            page.Footer().AlignCenter()
                .Text("Please follow the prescribed dosage and consult your doctor if symptoms persist.")
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
