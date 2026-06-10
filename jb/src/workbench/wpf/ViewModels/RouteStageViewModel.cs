namespace Prism.Workbench.Wpf.ViewModels;

/// <summary>
/// Describes one visible PRISM route stage in the workbench.
/// </summary>
/// <param name="Name">Stage name from the definitive PRISM route order.</param>
/// <param name="Status">Current display status for the stage.</param>
public sealed record RouteStageViewModel(string Name, string Status);
