using ClinicSystem.Core.Models;
using ClinicSystem.Data.Repositories;

namespace ClinicSystem.Data.Services;

public static class ActivityService
{
    private static ActivityLogRepository? _repo;

    public static void Initialize(ActivityLogRepository repo)
    {
        _repo = repo;
    }

    public static event Action<ActivityLog>? OnActivityLogged;

    public static void Log(string module, string title, string description, int userId, string userName = "")
    {
        if (_repo == null) return;
        
        var log = new ActivityLog
        {
            Title = title,
            Description = description,
            Module = module,
            UserId = userId,
            UserName = string.IsNullOrWhiteSpace(userName) ? "System" : userName
        };
        
        _repo.Insert(log);
        
        // Broadcast via event so UI layer can update Dashboard without a circular dependency
        OnActivityLogged?.Invoke(log);
    }
}
