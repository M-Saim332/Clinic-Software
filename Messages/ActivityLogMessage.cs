using ClinicSystem.Core.Models;

namespace ClinicSystem.UI.Messages;

public class ActivityLogMessage
{
    public ActivityLog Log { get; }
    public ActivityLogMessage(ActivityLog log) => Log = log;
}
