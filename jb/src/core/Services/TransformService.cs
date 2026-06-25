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

        string? prismConfigPath = PrismConfigLocator.FindPrismConfigPath();
        if (prismConfigPath is null)
            throw new PrismConfigurationException("Prism_Config.json not found — cannot run preprocessor.");

        PrismConfiguration prismConfig = PrismConfiguration.LoadPrismConfig(prismConfigPath);

        Dictionary<string, ImageRecord_INPUT> inputByName = matched.Ingest.NormalizedImages
            .ToDictionary(r => r.InitialFullName, StringComparer.OrdinalIgnoreCase);

        int okTransformed = 0;
        foreach (ImageRecord_LAMBDA lambda in matched.LambdaRecords)
        {
            if (lambda.IsKo) continue;

            inputByName.TryGetValue(lambda.InitialFullName, out ImageRecord_INPUT? input);
            byte[]? preprocessed = ImagePreProcessor.Preprocess(lambda, input?.NormalizedJpgPath, prismConfig);

            if (lambda.IsKo) continue;

            lambda.ProcessedBytes = preprocessed;

            ImageTransformer.TransformImage(lambda);
            okTransformed++;
        }

        return new TransformResult { Matched = matched, OkTransformedCount = okTransformed };
    }
}
