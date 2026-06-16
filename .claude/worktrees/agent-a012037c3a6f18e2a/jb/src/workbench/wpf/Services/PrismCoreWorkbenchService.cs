namespace Prism.Workbench.Wpf.Services;

/// <summary>
/// Reports the current PRISM core adapter boundary for the WPF workbench.
/// </summary>
public sealed class PrismCoreWorkbenchService : IPrismWorkbenchService
{
    /// <summary>
    /// Gets the current core adapter status for display.
    /// </summary>
    /// <returns>A user-visible core adapter status.</returns>
    public string GetCoreStatus()
    {
        return "Core project reference is configured. Prism.Process is not wired into this WPF workbench yet.";
    }
}
