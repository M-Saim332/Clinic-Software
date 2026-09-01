using ClinicSystem.Data.Repositories;

namespace ClinicSystem.UI.Services;

/// <summary>One read-through source for names shown on generated documents.</summary>
public static class ClinicBrandingService
{
    private static SettingsRepository? _settings;

    public static void Initialize(SettingsRepository settings) => _settings = settings;

    public static string ClinicName
    {
        get
        {
            var pharmacyName = _settings?.GetValue("PharmacyName", "")?.Trim();
            if (!string.IsNullOrWhiteSpace(pharmacyName)) return pharmacyName;

            var clinicName = _settings?.GetValue("ClinicName", "")?.Trim();
            return !string.IsNullOrWhiteSpace(clinicName) ? clinicName : "DR ASIF CLINIC";
        }
    }
}
