namespace ClinicSystem.Core.Models;

public class DiscountRefund
{
    public int RefundID { get; set; }

    // Patient info
    public string PatientName { get; set; } = string.Empty;
    public string? TokenNumber { get; set; }

    // Fee breakdown
    public decimal OriginalFee { get; set; }
    public decimal DiscountedFee { get; set; }
    public decimal RefundAmount { get; set; } // Persisted computed column from DB

    public string? Notes { get; set; }

    // Approval audit
    public int? ApprovedByUserID { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime ApprovedAt { get; set; } = DateTime.Now;

    // Completion audit
    public int? CompletedByUserID { get; set; }
    public string? CompletedByName { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool IsCompleted { get; set; }

    // ── Display helpers ──────────────────────────────────────────────────────
    public string RefundSummary =>
        $"Please return Rs. {RefundAmount:N2} to {PatientName}" +
        (string.IsNullOrWhiteSpace(TokenNumber) ? "" : $" (Token: {TokenNumber})") +
        $" — Discount approved by {ApprovedByName ?? "Doctor"}.";

    public string FeeSummary =>
        $"Original: Rs. {OriginalFee:N2}  →  After Discount: Rs. {DiscountedFee:N2}  =  Refund Rs. {RefundAmount:N2}";

    public string ApprovedAtDisplay => ApprovedAt.ToString("dd MMM yyyy  hh:mm tt");

    public string CompletedAtDisplay =>
        CompletedAt.HasValue ? CompletedAt.Value.ToString("dd MMM yyyy  hh:mm tt") : "—";
}
