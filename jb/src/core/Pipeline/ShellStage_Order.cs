/// <summary>
/// Shell delegate for the Ordered stage.
/// Assigns det-order indices per FamilyID using classification labels and filename tokens.
/// Real implementation lives in <c>ImageOrderer.cs</c>.
/// </summary>
internal static class ShellStage_Order
{
    /// <summary>
    /// Runs the Ordered stage for a job context.
    /// Delegates to <see cref="ImageOrderer"/> to assign det-slot indices and ordering evidence per FamilyID.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        ImageOrderer.Run(context);
        context.MarkStageCompleted(PipelineStageNames.Ordered);
    }
}
