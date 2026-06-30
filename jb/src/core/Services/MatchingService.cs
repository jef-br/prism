using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/// <summary>
/// In-process Matching implementation — owns the ImageRecord_INPUT → ImageRecord_LAMBDA conversion.
/// Per image it fans out to FeatureAnalysis and Classification, fans in to ImageNGP for the phenotype,
/// then runs the matching waterfall, det-order assignment, and rename validation. Emits the Classified,
/// Matched, Ordered, and Renamed stage events in order and persists each LAMBDA document to the artifact
/// store so downstream services can read a stage's output without a shared mutable context.
/// </summary>
public sealed class MatchingService : IMatchingService, IDisposable
{
    private readonly PrismConfiguration configuration;
    private readonly ImageClassifier _sharedClassifier;
    private readonly ClipPromptCatalog _sharedPromptCatalog;
    private readonly object _clipLock = new();
    private bool _disposed;

    /// <summary>Creates the service with the validated PRISM configuration (thresholds, dedup policy).
    /// Initializes the shared CLIP ONNX session once for the app lifetime.</summary>
    public MatchingService(PrismConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _sharedPromptCatalog = ClassificationService.LoadPromptCatalog();
        _sharedClassifier    = new ImageClassifier();
        ClassificationService.InitializeClassifier(_sharedClassifier);
    }

    /// <inheritdoc/>
    public async Task<MatchingResult> MatchAsync(
        IngestResult ingest,
        IArtifactStore store,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        //  Classified: build one LAMBDA per normalized image (FeatureAnalysis + Classification + ImageNGP)
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Classified, cancellationToken);

        List<ImageRecord_INPUT> okImages = ingest.NormalizedImages
            .Where(r => r.ImportStatus == ImportStatus.Ok && r.NormalizedJpgPath is not null)
            .ToList();

        IImageNgpService ngp                     = new ImageNgpService(LoadRuleSet());
        IFeatureAnalysisService featureAnalysis  = new FeatureAnalysisService();
        using IClassificationService classification =
            new ClassificationService(_sharedClassifier, _sharedPromptCatalog, configuration.MaxHammingDistance);

        // Pre-allocate a fixed results array — each thread writes to its own index, no synchronisation needed.
        var results = new (ImageRecord_LAMBDA Lambda, ImageRecord_INPUT Source, UInt128 Hash)[okImages.Count];
        int classifyKo        = 0;
        int classifyDegraded  = 0;
        int phenotypeAssigned = 0;

        Parallel.For(0, okImages.Count,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) },
            i =>
            {
                var (lambda, hash, wasKo, wasDegraded, wasPhenotype) = BuildLambda(
                    okImages[i], featureAnalysis, classification, ngp, ingest.Parameters.SkipClassification);
                results[i] = (lambda, okImages[i], hash);
                if (wasKo)        Interlocked.Increment(ref classifyKo);
                if (wasDegraded)  Interlocked.Increment(ref classifyDegraded);
                if (wasPhenotype) Interlocked.Increment(ref phenotypeAssigned);
            });

        // Aggregate into ordered collections (single-threaded; preserves input order for deterministic matching).
        List<ImageRecord_LAMBDA> lambdaRecords = new(okImages.Count);
        Dictionary<ImageRecord_INPUT, ImageRecord_LAMBDA> lambdaByImage = new(okImages.Count);
        var hashEntries = new List<(ImageRecord_INPUT Record, UInt128 Hash)>(okImages.Count);

        foreach (var (lambda, source, hash) in results)
        {
            lambdaRecords.Add(lambda);
            lambdaByImage[source] = lambda;
            hashEntries.Add((source, hash));
        }

        int duplicatesRemoved = configuration.ShouldDeduplicate
            ? Deduplicate(lambdaByImage, classification, hashEntries)
            : 0;

        //  Matched: resolve a FamilyID for each image via the waterfall
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Matched, cancellationToken);
        int matchKo = ImageMatcher.Run(lambdaRecords, ingest.FamilyRecords);

        //  Ordered: assign det slots within each family
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Ordered, cancellationToken);
        ImageOrderer.Run(lambdaRecords, ingest.FamilyRecords);

        //  Renamed: validate det-slot uniqueness, count renamed images
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Renamed, cancellationToken);
        (int okRenamed, int renameKo) = ImageRenamer.Run(lambdaRecords);

        PersistLambdaDocuments(store, ingest.JobID, lambdaRecords);

        return new MatchingResult
        {
            Ingest                 = ingest,
            LambdaRecords          = lambdaRecords,
            OkRenamedCount         = okRenamed,
            KoRecordCount          = classifyKo + duplicatesRemoved + matchKo + renameKo,
            DuplicatesRemoved      = duplicatesRemoved,
            PhenotypeAssignedCount = phenotypeAssigned,
            Warnings               = BuildWarnings(classifyDegraded)
        };
    }

    /// <summary>
    /// Surfaces classification degradation as one aggregated, non-silent manifest warning.
    /// Empty when every image classified cleanly.
    /// </summary>
    private static IReadOnlyList<string> BuildWarnings(int classifyDegraded)
    {
        if (classifyDegraded == 0) return [];
        return [$"CLIP classification unavailable for {classifyDegraded} image(s); matched on filename tokens only."];
    }

    //  Per-image classification (fan-out FA + Classification, fan-in ImageNGP)

    /// <summary>
    /// Builds and classifies one LAMBDA. Loads the normalized image once and shares it across
    /// FeatureAnalysis, CLIP tagging, and perceptual-hash computation (one disk read for all three).
    /// FeatureAnalysis is the core measurement — a failure there KOs the image.
    /// CLIP tagging is optional enrichment — a failure there degrades the image to "no tags", never KO.
    /// Returns the lambda plus per-image counters so the caller can aggregate with Interlocked — no shared
    /// mutable state, safe to call from Parallel.For.
    /// </summary>
    private (ImageRecord_LAMBDA Lambda, UInt128 Hash, bool WasKo, bool WasDegraded, bool WasPhenotypeAssigned) BuildLambda(
        ImageRecord_INPUT source,
        IFeatureAnalysisService featureAnalysis,
        IClassificationService classification,
        IImageNgpService ngp,
        bool skipClassification = false)
    {
        ImageRecord_LAMBDA lambda = new()
        {
            InitialFullName = source.InitialFullName,
            Width  = source.NormalizedWidth  > 0 ? source.NormalizedWidth  : source.Width,
            Height = source.NormalizedHeight > 0 ? source.NormalizedHeight : source.Height
        };

        if (source.NormalizedJpgPath is null)
            return (lambda, UInt128.Zero, false, false, false);

        Image<Rgba32> image;
        try
        {
            image = Image.Load<Rgba32>(source.NormalizedJpgPath);
        }
        catch (Exception ex)
        {
            lambda.IsKo          = true;
            lambda.KoReasonCode  = "CLASSIFY_ERROR";
            lambda.KoSafeMessage = $"Feature extraction failed: {ex.Message}";
            return (lambda, UInt128.Zero, true, false, false);
        }

        bool wasDegraded = false;

        using (image)
        {
            // Hash computed here — same load shared with feature analysis and CLIP below.
            UInt128 hash = UInt128.Zero;
            try { hash = VisualHasher.ComputeHash(image); } catch { }

            // FeatureAnalysis failure → KO: the geometric/visual measurement feeds ImageNGP and ordering.
            try
            {
                featureAnalysis.Analyze(image, lambda.Features);
            }
            catch (Exception ex)
            {
                lambda.IsKo          = true;
                lambda.KoReasonCode  = "CLASSIFY_ERROR";
                lambda.KoSafeMessage = $"Feature extraction failed: {ex.Message}";
                return (lambda, hash, true, false, false);
            }

            // CLIP failure → degrade, never KO: tags are optional enrichment, and FamilyID matching keys
            // off filename tokens, so the image must still flow to ImageNGP and the matching waterfall.
            // _clipLock serializes inference across all images (intra-job) and all concurrent jobs — required
            // because the DML execution provider does not support concurrent InferenceSession.Run calls.
            if (classification.IsReady && !skipClassification)
            {
                try
                {
                    lock (_clipLock)
                    {
                        classification.ApplyClipTags(image, lambda,
                            configuration.ThresholdForInfluentialTags,
                            configuration.ThresholdForDiscardingClassificationTags);
                    }
                }
                catch
                {
                    wasDegraded = true;
                }
            }

            string[] candidates = ngp.EvaluateCandidates(lambda.Features);
            lambda.CandidatePhenotypes = candidates;
            lambda.SelectedPhenotype   = candidates.Length > 0 ? candidates[0] : null;

            return (lambda, hash, false, wasDegraded, lambda.SelectedPhenotype is not null);
        }
    }

    //  Post-classification visual deduplication

    /// <summary>
    /// KOs visual duplicates after classification using pre-computed perceptual hashes, exempting
    /// configured phenotypes (illustrations, technical drawings, labels).
    /// Returns the number of duplicates suppressed.
    /// </summary>
    private int Deduplicate(
        Dictionary<ImageRecord_INPUT, ImageRecord_LAMBDA> lambdaByImage,
        IClassificationService classification,
        IReadOnlyList<(ImageRecord_INPUT Record, UInt128 Hash)> hashEntries)
    {
        HashSet<string> exempt = new(configuration.DeduplicationExemptPhenotypes, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<DedupGroup> groups = classification.FindDuplicates(hashEntries);
        int removed = 0;

        foreach (DedupGroup group in groups)
        {
            // Skip groups whose canonical was rejected during classification —
            // its duplicates are not confirmed duplicates of a valid image.
            if (lambdaByImage.TryGetValue(group.Canonical, out ImageRecord_LAMBDA? canonLambda) && canonLambda.IsKo)
                continue;

            foreach (ImageRecord_INPUT duplicate in group.Duplicates)
            {
                ImageRecord_LAMBDA lambda = lambdaByImage[duplicate];

                // Already rejected (e.g. CLASSIFY_ERROR) — keep its original reason.
                if (lambda.IsKo) continue;

                // Illustrations, technical drawings, and labels are exempt so EU energy labels and
                // tech drawings pass; packshots, closeups, and zooms are removed as duplicates.
                if (lambda.SelectedPhenotype is not null && exempt.Contains(lambda.SelectedPhenotype)) continue;

                lambda.IsKo          = true;
                lambda.KoReasonCode  = "VISUAL_DUPLICATE";
                lambda.KoSafeMessage = $"Visual duplicate of {Path.GetFileName(group.Canonical.InitialFullName)}";
                removed++;
            }
        }

        return removed;
    }

    /// <inheritdoc/>
    public void Dispose() { if (_disposed) return; _disposed = true; _sharedClassifier.Dispose(); }

    //  Helpers

    private static PhenotypeRuleSet LoadRuleSet()
    {
        string? imageRolesPath = PrismConfigLocator.FindFolderLocalConfig("ImageRoles.json");
        if (imageRolesPath is null)
            throw new PrismConfigurationException(
                "ImageRoles.json not found. Ensure ImageRoles.json is present in the config directory next to Prism_Config.json.");

        return PhenotypeRuleSet.Load(imageRolesPath);
    }

    /// <summary>
    /// Writes each LAMBDA out as a JSON document under the job folder. This is the retrieval substrate
    /// for distributed deployment; a write failure must never fail an otherwise-successful job, so
    /// persistence is best-effort in the modular monolith.
    /// </summary>
    private static void PersistLambdaDocuments(IArtifactStore store, Guid jobId, IReadOnlyList<ImageRecord_LAMBDA> lambdas)
    {
        try
        {
            foreach (ImageRecord_LAMBDA lambda in lambdas)
                store.SaveLambdaDocument(jobId, lambda.InitialFullName, lambda);
        }
        catch
        {
            // Substrate persistence only — never block job completion on a document write.
        }
    }
}
