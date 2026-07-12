using OpenCvSharp;

namespace Prism.Services.Transform;

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
        bool headcut,
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

        PrismConfiguration prismConfig = ConfigCache.GetOrLoad(
            () => PrismConfiguration.LoadPrismConfig(prismConfigPath), prismConfigPath);

        // Load + validate every transform_Config.json section once per stage run, then hand the bundle
        // to each per-image transform — no config lookup inside the parallel loop below.
        TransformParameters parameters = TransformParameters.FromConfig();

        Dictionary<string, ImageRecord_INPUT> inputByName = matched.Ingest.NormalizedImages
            .ToDictionary(r => r.InitialFullName, StringComparer.OrdinalIgnoreCase);

        // Per-image transforms are independent CPU-bound OpenCV work — safe to fan out. Each thread
        // writes only its own lambda; the GPU upscaler serializes its InferenceSession.Run calls
        // internally (Upscaler_g_p_u._sessionLock), so parallel callers are safe there too.
        int okTransformed = 0;
        Parallel.ForEach(
            matched.LambdaRecords.Where(l => !l.IsKo),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            lambda =>
            {
                inputByName.TryGetValue(lambda.InitialFullName, out ImageRecord_INPUT? input);
                (byte[]? preprocessed, Mat? colorMat) = ImagePreProcessor.Preprocess(lambda, input?.NormalizedJpgPath, prismConfig);

                if (lambda.IsKo) { colorMat?.Dispose(); return; }

                lambda.ProcessedBytes = preprocessed;

                using (colorMat)
                {
                    ImageTransformer.TransformImage(lambda, colorMat, headcut, parameters);
                }

                Interlocked.Increment(ref okTransformed);
            });

        return new TransformResult { Matched = matched, OkTransformedCount = okTransformed };
    }
}
