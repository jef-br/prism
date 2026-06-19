/// <summary>
/// Shell delegate for the Generated stage.
/// For families below minimum image count, copies the hero image and creates generated variants.
/// Real implementation lives in the generation module.
/// </summary>
internal static class ShellStage_Generate
{
    /// <summary>
    /// Runs the Generated stage for a job context.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        ImageGenerator.Run(context);
        context.MarkStageCompleted(PipelineStageNames.Generated);
    }
}
