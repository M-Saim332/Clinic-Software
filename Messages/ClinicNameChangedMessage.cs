namespace ClinicSystem.UI.Messages;

public class ClinicNameChangedMessage
{
    public string ClinicName { get; }
    public string PharmacyName { get; }
    
    public ClinicNameChangedMessage(string clinicName, string pharmacyName)
    {
        ClinicName = clinicName;
        PharmacyName = pharmacyName;
    }
}
