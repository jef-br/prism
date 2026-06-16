namespace Prism.Workbench.Wpf.Services;

/// <summary>
/// Defines the WPF boundary for connecting the workbench to PRISM core.
/// </summary>
public interface IPrismWorkbenchService
{
    /// <summary>
    /// Gets the current core adapter status for display.
    /// </summary>
    /// <returns>A user-visible core adapter status.</returns>
    string GetCoreStatus();
}
