namespace ClinicSystem.Core.Models;

public class ActivityLog
{
    public int ActivityId  { get; set; }
    public string Title       { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Module      { get; set; } = string.Empty;
    public int    UserId      { get; set; }
    public string UserName    { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Formatted display text used in the dashboard feed.</summary>
    public string Display => $"{Title} — {Description}";
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - CreatedAt;
            if (diff.TotalMinutes < 1)  return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours}h ago";
            return CreatedAt.ToString("dd MMM");
        }
    }
}
