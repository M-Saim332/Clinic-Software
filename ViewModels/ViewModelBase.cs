using CommunityToolkit.Mvvm.ComponentModel;
using ClinicSystem.Core.Models;
using ClinicSystem.UI.Messages;
using CommunityToolkit.Mvvm.Messaging;

namespace ClinicSystem.UI.ViewModels;

/// <summary>Base for all ViewModels — carries the logged-in session user.</summary>
public partial class ViewModelBase : ObservableObject
{
    public static User? CurrentUser { get; set; }
    public static bool IsDoctor => CurrentUser?.IsDoctor ?? false;

    protected void LogActivity(string title, string description, string module)
    {
        ClinicSystem.Data.Services.ActivityService.Log(
            module: module,
            title: title,
            description: description,
            userId: CurrentUser?.UserID ?? 0,
            userName: CurrentUser?.FullName?.Length > 0 ? CurrentUser.FullName : CurrentUser?.Username ?? "System"
        );
    }
}
