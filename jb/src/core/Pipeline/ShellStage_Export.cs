/// <summary>
/// Shell delegate for the Exported stage.
/// Packages all output images with manifest.json into the requested output format.
/// Real implementation lives in <c>Exporter.cs</c>.
/// </summary>
internal static class ShellStage_Export
{
    /// <summary>
    /// Runs the Exported stage for a job context.
    /// Delegates zip/JSON packaging and manifest construction to <see cref="Exporter"/>.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        Exporter.Run(context, configuration);
        context.MarkStageCompleted(PipelineStageNames.Exported);
    }
}
