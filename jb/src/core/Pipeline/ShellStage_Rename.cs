namespace Prism.Core;

/// <summary>
/// Shell delegate for the Renamed stage.
/// Validates det-slot uniqueness within each matched family and counts renamed images.
/// Real implementation lives in <c>ImageRenamer.cs</c>.
/// </summary>
internal static class ShellStage_Rename
{
    /// <summary>
    /// Runs the Renamed stage for a job context.
    /// Delegates collision detection and rename counting to <see cref="ImageRenamer"/>.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        ImageRenamer.Run(context);
        context.MarkStageCompleted(PipelineStageNames.Renamed);
    }
}
