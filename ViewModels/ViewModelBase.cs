using CommunityToolkit.Mvvm.ComponentModel;
using ClinicSystem.Core.Models;

namespace ClinicSystem.UI.ViewModels;

/// <summary>Base for all ViewModels — carries the logged-in session user.</summary>
public partial class ViewModelBase : ObservableObject
{
    public static User? CurrentUser { get; set; }
    public static bool IsDoctor => CurrentUser?.IsDoctor ?? false;
}
