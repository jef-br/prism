using Prism.Workbench.Wpf.Services;
using System.Collections.ObjectModel;

namespace Prism.Workbench.Wpf.ViewModels;

/// <summary>
/// Provides the visible state for the main workbench window.
/// </summary>
/// <param name="workbenchService">Service that describes the current PRISM core connection boundary.</param>
public sealed class MainWindowViewModel(IPrismWorkbenchService workbenchService)
{
    /// <summary>
    /// Gets the title shown by the main WPF window.
    /// </summary>
    public string WindowTitle { get; } = "PRISM Workbench";

    /// <summary>
    /// Gets the current core adapter status for display.
    /// </summary>
    public string CoreStatus { get; } = workbenchService.GetCoreStatus();

    /// <summary>
    /// Gets the definitive PRISM route order shown before a job starts.
    /// </summary>
    public ObservableCollection<RouteStageViewModel> RouteStages { get; } =
    [
        new("Imported", "Waiting"),
        new("Classified", "Waiting"),
        new("Matched", "Waiting"),
        new("Ordered", "Waiting"),
        new("Renamed", "Waiting"),
        new("Generated", "Waiting"),
        new("Transformed", "Waiting"),
        new("Exported", "Waiting")
    ];
}
