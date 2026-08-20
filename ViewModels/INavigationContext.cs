namespace ClinicSystem.UI.ViewModels;

public interface INavigationContext
{
    int? PreselectedEntityId { get; set; }
    Action<int>? ReturnToCaller { get; set; }
}
