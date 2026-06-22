namespace Prism.Core;

/// <summary>
/// In-process Transform implementation. Routes each non-KO LAMBDA through <see cref="ImageTransformer"/> and
/// enriches it in place with a TransformationResult. When transform is disabled, every non-KO image is
/// marked Skipped. Emits the Transformed stage event.
/// </summary>
public sealed class TransformService : ITransformService
{
    /// <inheritdoc/>
    public async Task<TransformResult> TransformAsync(
        MatchingResult matched,
        bool transformEnabled,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        await StageProgress.EmitStarted(progress, matched.Ingest.JobID, PipelineStageNames.Transformed, cancellationToken);

        if (!transformEnabled)
        {
            foreach (ImageRecord_LAMBDA lambda in matched.LambdaRecords)
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

            return new TransformResult { Matched = matched, OkTransformedCount = 0 };
        }

        int okTransformed = 0;
        foreach (ImageRecord_LAMBDA lambda in matched.LambdaRecords)
        {
            if (lambda.IsKo) continue;
            ImageTransformer.TransformImage(lambda);
            okTransformed++;
        }

        return new TransformResult { Matched = matched, OkTransformedCount = okTransformed };
    }
}
