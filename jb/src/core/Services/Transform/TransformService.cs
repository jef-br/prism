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
        CropTransformSettings cropSettings = new(
            prismConfig.WhiteSpaceMargin,
            prismConfig.CropCoverage,
            prismConfig.CropExtensionOneSided,
            prismConfig.CropExtensionBiDirectional);

        string? transformConfigPath = PrismConfigLocator.FindFolderLocalConfig("transform_Config.json");
        if (transformConfigPath is null)
            throw new PrismConfigurationException(
                "transform_Config.json not found. Ensure transform_Config.json is present in the config directory next to Prism_Config.json.");

        TransformConfig transformConfig = ConfigCache.GetOrLoad(
            () => TransformConfig.Load(transformConfigPath), transformConfigPath);

        // Static utility Tx_ classes have a fixed-signature webservice Process() method with no
        // room for a config parameter — they read the config set here once per stage run instead.
        Tx_util_BgStretch.Configure(transformConfig.BgStretch);
        Tx_LowContrastEnhancement.Configure(transformConfig.LowContrastEnhancement);

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
                    ImageTransformer.TransformImage(lambda, colorMat, cropSettings, headcut, transformConfig);
                }

                Interlocked.Increment(ref okTransformed);
            });

        return new TransformResult { Matched = matched, OkTransformedCount = okTransformed };
    }
}
