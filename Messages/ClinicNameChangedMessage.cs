namespace ClinicSystem.UI.Messages;

public class ClinicNameChangedMessage
{
    public string NewName { get; }
    
    public ClinicNameChangedMessage(string newName)
    {
        NewName = newName;
    }
}
