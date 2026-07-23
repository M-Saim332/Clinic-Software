using System;

namespace ClinicSystem.UI.ViewModels;

public interface ISearchable
{
    string SearchTerm { get; set; }
    string SearchPlaceholder { get; }
}
