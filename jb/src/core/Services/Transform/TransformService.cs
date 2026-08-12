using OpenCvSharp;

namespace Prism.Services.Transform;

/// <summary>
/// In-process Transform implementation. Routes each non-KO LAMBDA through <see cref="ImageTransformer"/> and
/// attaches an OutputRecord carrying the transform outcome. When transform is disabled, every non-KO image is
/// marked Skipped. Emits the Transformed stage event. Below-minimum images upscale through the given
/// <see cref="IUpscaleService"/> when one is provided (remote Upscale host, PRISM_UPSCALE_URL); otherwise
/// through the local static Real-ESRGAN session.
/// </summary>
public sealed class TransformService : ITransformService {
    private readonly IUpscaleService? remoteUpscale;

    public TransformService() : this(null) { }

    public TransformService(IUpscaleService? remoteUpscale) => this.remoteUpscale = remoteUpscale;

    /// <inheritdoc/>
    public async Task<TransformResult> TransformAsync(
        MatchingResult matched,
        bool transformEnabled,
        bool headcut,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken) {
        await StageProgress.EmitStarted(progress, matched.Ingest.JobID, PipelineStageNames.Transformed, cancellationToken);

        if (!transformEnabled) {
            foreach (ImageRecord_LAMBDA lambda in matched.LambdaRecords) {
                if (lambda.IsKo) continue;
                lambda.OutputRecord = new ImageRecord_OUTPUT {
                    TransformStatus = TransformationStatus.Skipped,
                    InputWidth = lambda.Width,
                    InputHeight = lambda.Height,
                    SafeSummaryText = "Transform disabled by job parameters."
                };
            }

            return new TransformResult { Matched = matched, OkTransformedCount = 0 };
        }

        PrismConfiguration prismConfig = PrismConfiguration.LoadPrismConfig(
            ConfigLoader.RequireFile(PrismConfiguration.FileName));

        // Load + validate every transform_Config.json section once per stage run, then hand the bundle
        // to each per-image transform — no config lookup inside the parallel loop below.
        TransformParameters parameters = TransformParameters.FromConfig();

        // Read off the job parameters rather than a method argument: the parameters already ride inside
        // MatchingResult across the matching→transform HTTP boundary (the ServiceHost route reads
        // Transform and Headcut the same way), so one read here cannot be dropped at a call site.
        // ...and ANDed with the Models.Upscaling.UseIt toggle: with Real-ESRGAN switched off there is no
        // session to reach, so the job parameter cannot opt in. False routes through the Lanczos path
        // T-4900 already built (ImagePreProcessor.UpscaleAsync) — no new upscaling logic.
        bool allowEsrganUpscale = matched.Ingest.Parameters.AllowEsrganUpscale && prismConfig.AiUpscalingEnabled;

        Dictionary<string, ImageRecord_INPUT> inputByName = matched.Ingest.NormalizedImages
            .ToDictionary(r => r.InitialFullName, StringComparer.OrdinalIgnoreCase);

        // Family lookup so each image can reach its Excel record (product type/colour) for seeding —
        // the lambda only carries the Family id string. Duplicate FamilyIDs collapse to the first record.
        Dictionary<string, FamilyIDRecord> familyById = matched.Ingest.FamilyRecords
            .GroupBy(f => f.FamilyID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Per-image transforms are independent CPU-bound OpenCV work — safe to fan out. Each thread
        // writes only its own lambda; the GPU upscaler serializes its InferenceSession.Run calls
        // internally (Upscaler._sessionLock), so parallel callers are safe there too.
        int okTransformed = 0;
        await Parallel.ForEachAsync(
            matched.LambdaRecords.Where(l => !l.IsKo),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken },
            async (lambda, ct) => {
                inputByName.TryGetValue(lambda.InitialFullName, out ImageRecord_INPUT? input);

                // Seeding moved ahead of preprocessing (T-4910): the shadow-accounting toggle it feeds
                // changes the bounding box, and the upscale decision inside PreprocessAsync sizes
                // against that box. Resolving it afterwards would size against geometry the stage then
                // discards.
                familyById.TryGetValue(lambda.Family, out FamilyIDRecord? family);
                TransformSeed seed = TransformSeed.Resolve(lambda, family);

                (byte[]? preprocessed, Mat? colorMat) = await ImagePreProcessor.PreprocessAsync(
                    lambda, input?.NormalizedJpgPath, prismConfig, parameters, seed, allowEsrganUpscale, this.remoteUpscale, ct);

                if (lambda.IsKo) { colorMat?.Dispose(); return; }

                lambda.ProcessedBytes = preprocessed;

                using (colorMat) {
                    ImageTransformer.TransformImage(lambda, colorMat, headcut, parameters, seed);
                }

                Interlocked.Increment(ref okTransformed);
            });

        return new TransformResult { Matched = matched, OkTransformedCount = okTransformed };
    }
}
