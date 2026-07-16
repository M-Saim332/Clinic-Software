namespace ClinicSystem.UI.Messages;

/// <summary>
/// Sent by the doctor when a new discount refund is approved.
/// Triggers the receptionist dashboard to refresh its pending-refund banner.
/// </summary>
public class RefundIssuedMessage
{
    public string PatientName { get; init; } = string.Empty;
    public decimal RefundAmount { get; init; }
}
