using Avalonia;
using System;
using System.IO;
namespace ClinicSystem.UI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteStartupFailure(ex);
            Environment.ExitCode = 1;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    internal static void WriteStartupFailure(Exception exception)
    {
        var details = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Application startup failed{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
        Console.Error.WriteLine(details);
        System.Diagnostics.Debug.WriteLine(details);

        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "app_startup_error.log"), details);
        }
        catch
        {
            // Console.Error is still available when the application directory is not writable.
        }
    }
}
