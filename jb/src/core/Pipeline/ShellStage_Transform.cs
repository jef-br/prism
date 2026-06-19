/// <summary>
/// Shell delegate for the Transformed stage.
/// Routes each non-KO image to its appropriate <see cref="IImageTransformation"/> strategy
/// via <see cref="ImageTransformer"/>, then updates per-job counters.
/// </summary>
internal static class ShellStage_Transform
{
    /// <summary>
    /// Runs the Transformed stage for a job context.
    /// When <c>Parameters.Transform</c> is false, all non-KO images are marked Skipped and the stage completes immediately.
    /// Otherwise each non-KO image is routed through <see cref="ImageTransformer.TransformImage"/>.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        if (!context.Parameters.Transform)
        {
            foreach (ImageRecord_LAMBDA lambda in context.LambdaRecords)
            {
                if (lambda.IsKo) continue;
                lambda.TransformationResult = new ImageTransformationResult
                {
                    Status          = TransformationStatus.Skipped,
                    InputWidth      = lambda.Width,
                    InputHeight     = lambda.Height,
                    SafeSummaryText = "Transform disabled by job parameters."
                };
            }
            context.MarkStageCompleted(PipelineStageNames.Transformed);
            return;
        }

        foreach (ImageRecord_LAMBDA lambda in context.LambdaRecords)
        {
            if (lambda.IsKo) continue;
            ImageTransformer.TransformImage(lambda);
            context.OkTransformedCount++;
        }

        context.MarkStageCompleted(PipelineStageNames.Transformed);
    }
}
