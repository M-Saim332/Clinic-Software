namespace ClinicSystem.UI.Messages;

/// <summary>
/// Sent when the receptionist marks a refund as completed.
/// Triggers banner refresh and history reload.
/// </summary>
public class RefundCompletedMessage
{
    public int RefundID { get; init; }
}
