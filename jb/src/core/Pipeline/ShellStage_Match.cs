/// <summary>
/// Shell delegate for the Matched stage.
/// Tokenizes each image and resolves a FamilyID above threshold using the matcher waterfall.
/// Real implementation lives in <c>ImageMatcher.cs</c>.
/// </summary>
internal static class ShellStage_Match
{
    /// <summary>
    /// Runs the Matched stage for a job context.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        ImageMatcher.Run(context);
        context.MarkStageCompleted(PipelineStageNames.Matched);
    }
}
