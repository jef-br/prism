using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/// <summary>
/// In-process Matching implementation — owns the ImageRecord_INPUT → ImageRecord_LAMBDA conversion.
/// Per image it fans out to FeatureAnalysis and Classification, fans in to ImageNGP for the phenotype,
/// then runs the matching waterfall, det-order assignment, and rename validation. Emits the Classified,
/// Matched, Ordered, and Renamed stage events in order and persists each LAMBDA document to the artifact
/// store so downstream services can read a stage's output without a shared mutable context.
/// </summary>
public sealed class MatchingService : IMatchingService, IDisposable {
    private readonly PrismConfiguration configuration;
    private readonly ImageClassifier _sharedClassifier;
    private readonly ClipPromptCatalog _sharedPromptCatalog;
    private bool _disposed;

    /// <summary>Creates the service with the validated PRISM configuration (thresholds, dedup policy).
    /// Resolves the process-wide shared CLIP ONNX session (see <see cref="ImageClassifier.GetShared"/>) —
    /// loaded once per process regardless of how many MatchingService instances are constructed.</summary>
    public MatchingService(PrismConfiguration configuration) {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this._sharedPromptCatalog = ClassificationService.LoadPromptCatalog();

        (string modelPath, string vocabPath, string mergesPath) = ClassificationService.ResolveClassifierPaths(configuration);
        this._sharedClassifier = ImageClassifier.GetShared(modelPath, vocabPath, mergesPath);
    }

    /// <inheritdoc/>
    public async Task<MatchingResult> MatchAsync(
        IngestResult ingest,
        IArtifactStore store,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken) {
        //  Classified: build one LAMBDA per normalized image (FeatureAnalysis + Classification + ImageNGP)
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Classified, cancellationToken);

        List<ImageRecord_INPUT> okImages = ingest.NormalizedImages
            .Where(r => r.ImportStatus == ImportStatus.Ok && r.NormalizedJpgPath is not null)
            .ToList();

        // Co-deployment guard: the job temp folder is the artifact bus between Ingest and Matching
        // (see PRISM-io-import.md "Co-Deployment Contract"). A Matching host that cannot see it is a
        // deployment topology error — fail loud here instead of KO-ing every image downstream with
        // misleading per-image decode errors.
        if (okImages.Count > 0 && !Directory.Exists(ingest.JobTempFolder))
            throw new InvalidOperationException(
                $"Matching host cannot read job temp folder '{ingest.JobTempFolder}'. " +
                "Ingress, Matching and Export must be co-deployed on one filesystem — " +
                "see jb/docs/PRISM-io-import.md (Co-Deployment Contract).");

        // Fail-fast: max-effort FamilyID detection already ran during Import. With zero parsed
        // families, matching is impossible, so KO every image immediately instead of decoding and
        // feature-analysing the whole batch only to reject all of it.
        if (ingest.FamilyRecords.Count == 0)
            return await this.BuildNoFamiliesResult(ingest, store, okImages, progress, cancellationToken);

        PhenotypeRuleSet ruleSet = LoadRuleSet();
        IImageNgpService ngp = new ImageNgpService(ruleSet);
        IFeatureAnalysisService featureAnalysis = new FeatureAnalysisService(this.configuration);
        using IClassificationService classification =
            new ClassificationService(this._sharedClassifier, this._sharedPromptCatalog, this.configuration);

        // Chunked fan-out/fan-in: decode + hash + feature analysis run CPU-parallel per chunk, then
        // the whole chunk classifies in one batched CLIP Run (ImageClassifier serializes Run() calls
        // internally — safe across concurrent chunks/jobs sharing the process-wide session), then
        // phenotypes evaluate and the chunk's images are disposed. Batching amortizes the prompt text
        // branch across the chunk; a fixed-batch model export transparently degrades to one Run per
        // image inside ApplyClipTagsBatch.
        var results = new (ImageRecord_LAMBDA Lambda, ImageRecord_INPUT Source, UInt128 Hash)[okImages.Count];
        int classifyKo = 0;
        int classifyDegraded = 0;
        int phenotypeAssigned = 0;
        bool doClassify = classification.IsReady && !ingest.Parameters.SkipClassification;

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) };

        for (int chunkStart = 0; chunkStart < okImages.Count; chunkStart += ClipChunkSize) {
            int chunkCount = Math.Min(ClipChunkSize, okImages.Count - chunkStart);
            var chunkImages = new Image<Rgba32>?[chunkCount];

            Parallel.For(0, chunkCount, parallelOptions, i => {
                int index = chunkStart + i;
                var (lambda, image, hash, wasKo) = PrepareLambda(okImages[index], featureAnalysis);
                results[index] = (lambda, okImages[index], hash);
                chunkImages[i] = image;
                if (wasKo) Interlocked.Increment(ref classifyKo);
            });

            if (doClassify) {
                var alive = new List<(Image<Rgba32> Image, ImageRecord_LAMBDA Lambda)>(chunkCount);
                for (int i = 0; i < chunkCount; i++) {
                    if (chunkImages[i] is not null && !results[chunkStart + i].Lambda.IsKo)
                        alive.Add((chunkImages[i]!, results[chunkStart + i].Lambda));
                }

                // CLIP failure → degrade, never KO: tags are optional enrichment, and FamilyID matching
                // keys off filename tokens, so the images must still flow to ImageNGP and the waterfall.
                if (alive.Count > 0) {
                    try {
                        // ImageClassifier serializes its own Run() calls internally (RunLock), so no
                        // external lock is needed here even across concurrent MatchingService jobs.
                        classification.ApplyClipTagsBatch(alive,
                            this.configuration.ThresholdForInfluentialTags,
                            this.configuration.ThresholdForDiscardingClassificationTags);
                    }
                    catch {
                        classifyDegraded += alive.Count;
                    }
                }
            }

            for (int i = 0; i < chunkCount; i++) {
                chunkImages[i]?.Dispose();

                ImageRecord_LAMBDA lambda = results[chunkStart + i].Lambda;
                if (lambda.IsKo) continue;

                string[] candidates = ngp.EvaluateCandidates(lambda.Features);
                lambda.CandidatePhenotypes = candidates;
                lambda.SelectedPhenotype = candidates.Length > 0 ? candidates[0] : null;
                if (lambda.SelectedPhenotype is not null) phenotypeAssigned++;
            }
        }

        // Aggregate into ordered collections (single-threaded; preserves input order for deterministic matching).
        List<ImageRecord_LAMBDA> lambdaRecords = new(okImages.Count);
        Dictionary<ImageRecord_INPUT, ImageRecord_LAMBDA> lambdaByImage = new(okImages.Count);
        var hashEntries = new List<(ImageRecord_INPUT Record, UInt128 Hash)>(okImages.Count);

        foreach (var (lambda, source, hash) in results) {
            lambdaRecords.Add(lambda);
            lambdaByImage[source] = lambda;
            hashEntries.Add((source, hash));
        }

        int duplicatesRemoved = this.configuration.ShouldDeduplicate
            ? this.Deduplicate(lambdaByImage, classification, hashEntries)
            : 0;

        //  Matched: resolve a FamilyID for each image via the waterfall
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Matched, cancellationToken);
        int matchKo = ImageMatcher.Run(lambdaRecords, ingest.FamilyRecords);

        //  Refine: post-match analyzer chain — now that the family (IEM) is known, narrow each
        //  image's phenotype pool with IEM/filename/detector evidence and finalize the phenotype.
        int refinementFailed;
        (phenotypeAssigned, refinementFailed) = RefinePhenotypes(results, ingest.FamilyRecords, featureAnalysis, ruleSet, parallelOptions);

        //  Ordered: assign det slots within each family
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Ordered, cancellationToken);
        ImageOrderer.Run(lambdaRecords, ingest.FamilyRecords);

        //  Renamed: validate det-slot uniqueness, count renamed images
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Renamed, cancellationToken);
        (int okRenamed, int renameKo) = ImageRenamer.Run(lambdaRecords);

        PersistLambdaDocuments(store, ingest.JobID, lambdaRecords);

        return new MatchingResult {
            Ingest = ingest,
            LambdaRecords = lambdaRecords,
            OkRenamedCount = okRenamed,
            KoRecordCount = classifyKo + duplicatesRemoved + matchKo + renameKo,
            DuplicatesRemoved = duplicatesRemoved,
            PhenotypeAssignedCount = phenotypeAssigned,
            Warnings = BuildWarnings(classifyDegraded, refinementFailed)
        };
    }

    /// <summary>
    /// Short-circuit result when Excel parsing produced no FamilyID records: every OK image is KO'd
    /// with NO_FAMILIES and no feature analysis runs, so the job completes near-instantly.
    /// </summary>
    private async Task<MatchingResult> BuildNoFamiliesResult(
        IngestResult ingest,
        IArtifactStore store,
        IReadOnlyList<ImageRecord_INPUT> okImages,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken) {
        List<ImageRecord_LAMBDA> lambdas = new(okImages.Count);
        foreach (ImageRecord_INPUT source in okImages) {
            lambdas.Add(new ImageRecord_LAMBDA {
                InitialFullName = source.InitialFullName,
                Width = source.NormalizedWidth > 0 ? source.NormalizedWidth : source.Width,
                Height = source.NormalizedHeight > 0 ? source.NormalizedHeight : source.Height,
                IsKo = true,
                KoReasonCode = "NO_FAMILIES",
                KoSafeMessage = "No FamilyID records were parsed from the Excel input."
            });
        }

        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Matched, cancellationToken);
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Ordered, cancellationToken);
        await StageProgress.EmitStarted(progress, ingest.JobID, PipelineStageNames.Renamed, cancellationToken);

        PersistLambdaDocuments(store, ingest.JobID, lambdas);

        return new MatchingResult {
            Ingest = ingest,
            LambdaRecords = lambdas,
            OkRenamedCount = 0,
            KoRecordCount = lambdas.Count,
            DuplicatesRemoved = 0,
            PhenotypeAssignedCount = 0,
            Warnings = [$"No FamilyID records were parsed from the Excel input; all {okImages.Count} image(s) were rejected."]
        };
    }

    /// <summary>
    /// Surfaces classification degradation and refinement failures as aggregated, non-silent manifest
    /// warnings. Empty when every image classified and refined cleanly.
    /// </summary>
    private static IReadOnlyList<string> BuildWarnings(int classifyDegraded, int refinementFailed) {
        List<string> warnings = [];

        if (classifyDegraded > 0)
            warnings.Add($"CLIP classification unavailable for {classifyDegraded} image(s); matched on filename tokens only.");

        if (refinementFailed > 0)
            warnings.Add($"Phenotype refinement failed for {refinementFailed} image(s); provisional phenotype kept.");

        return warnings;
    }

    //  Per-image preparation (fan-out: decode + hash + FeatureAnalysis; CLIP and ImageNGP run per chunk)

    // Images per batched CLIP Run — also bounds how many decoded images a chunk holds in memory.
    private const int ClipChunkSize = 8;

    /// <summary>
    /// Loads and measures one LAMBDA. The normalized image is loaded once and shared across
    /// FeatureAnalysis, perceptual-hash computation, and (by the caller) batched CLIP tagging — the
    /// returned image is kept open for the chunk's classification pass and disposed by the caller.
    /// FeatureAnalysis is the core measurement — a failure there KOs the image (and returns no image).
    /// Safe to call from Parallel.For: no shared mutable state.
    /// </summary>
    private static (ImageRecord_LAMBDA Lambda, Image<Rgba32>? Image, UInt128 Hash, bool WasKo) PrepareLambda(
        ImageRecord_INPUT source,
        IFeatureAnalysisService featureAnalysis) {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = source.InitialFullName,
            Width = source.NormalizedWidth > 0 ? source.NormalizedWidth : source.Width,
            Height = source.NormalizedHeight > 0 ? source.NormalizedHeight : source.Height
        };

        if (source.NormalizedJpgPath is null)
            return (lambda, null, UInt128.Zero, false);

        Image<Rgba32> image;
        try {
            // In-process Import->Match fusion (T-3500): when Import ran in this same process it hands
            // forward the encoded normalized JPEG bytes it already produced, so this decode reads from
            // memory instead of re-opening NormalizedJpgPath. Bytes (not a decoded Image) are the carried-
            // forward form deliberately — holding a decoded Image<Rgba32> per OK image in the batch would
            // balloon peak memory between Import finishing and this chunked loop consuming it; the encoded
            // bytes are a small fraction of that size. Absent (cross-process Match, or the Importer fast
            // path that never re-encoded) falls back to the disk read exactly as before.
            image = source.NormalizedJpegBytes is byte[] normalizedBytes
                ? Image.Load<Rgba32>(normalizedBytes)
                : Image.Load<Rgba32>(source.NormalizedJpgPath);

            // Consumed once — release the reference immediately rather than holding it for the rest of
            // the job (the post-match refinement chain re-reads NormalizedJpgPath from disk separately;
            // out of scope for this ticket, see T-3500 in jb/ticketboard/).
            source.NormalizedJpegBytes = null;
        }
        catch (Exception ex) {
            lambda.IsKo = true;
            lambda.KoReasonCode = "CLASSIFY_ERROR";
            lambda.KoSafeMessage = $"Feature extraction failed: {ex.Message}";
            return (lambda, null, UInt128.Zero, true);
        }

        // Hash computed here — same load shared with feature analysis and the chunk's CLIP pass.
        UInt128 hash = UInt128.Zero;
        try { hash = VisualHasher.ComputeHash(image); } catch { }

        // FeatureAnalysis failure → KO: the geometric/visual measurement feeds ImageNGP and ordering.
        try {
            featureAnalysis.Analyze(image, lambda.Features);
        }
        catch (Exception ex) {
            image.Dispose();
            lambda.IsKo = true;
            lambda.KoReasonCode = "CLASSIFY_ERROR";
            lambda.KoSafeMessage = $"Feature extraction failed: {ex.Message}";
            return (lambda, null, hash, true);
        }

        return (lambda, image, hash, false);
    }

    //  Post-match phenotype refinement

    /// <summary>
    /// Runs the refinement chain for every surviving image: resolves its FamilyIDRecord from the
    /// match evidence, hands lambda + family + image path to the analyzer chain, and returns the
    /// refined phenotype-assigned count. A refinement failure keeps the provisional phenotype —
    /// refinement improves evidence, it never KOs an image.
    /// </summary>
    private static (int Assigned, int RefinementFailed) RefinePhenotypes(
        (ImageRecord_LAMBDA Lambda, ImageRecord_INPUT Source, UInt128 Hash)[] results,
        IReadOnlyList<FamilyIDRecord> families,
        IFeatureAnalysisService featureAnalysis,
        PhenotypeRuleSet ruleSet,
        ParallelOptions parallelOptions) {
        Dictionary<string, FamilyIDRecord> familyById = new(StringComparer.OrdinalIgnoreCase);
        foreach (FamilyIDRecord family in families) familyById.TryAdd(family.FamilyID, family);

        int assigned = 0;
        int refinementFailed = 0;

        // Parallel per image (T-6910): this is the second full-resolution pass over the batch — Refine
        // re-reads the image from disk and re-runs YOLO plus the geometry/colour analyzers, so at batch
        // scale it costs as much wall clock as the chunked Analyze loop above despite doing less work.
        // Safe to fan out because every participant is either stateless or read-only by this point:
        // all 12 Analyzer_* types are static with no fields, ImageFeatureAnalyzer holds no static
        // mutable state, SubjectDetector/ProductTypeResolver/PhenotypeRuleSet keep only readonly config,
        // YoloDetector.Detect serializes itself on its own RunLock, and familyById is fully built before
        // the loop starts. Each iteration writes only to its own lambda, so results are order-independent
        // and identical to the sequential version.
        Parallel.ForEach(results, parallelOptions, entry => {
            ImageRecord_LAMBDA lambda = entry.Lambda;
            if (lambda.IsKo) return;

            FamilyIDRecord? family = lambda.MatchEvidence?.FinalFamilyId is string familyId
                && familyById.TryGetValue(familyId, out FamilyIDRecord? match) ? match : null;

            // Refinement never KOs an image — a systemic failure (bad model asset, corrupt image, the
            // shared YOLO/CLIP session under contention) must stay non-fatal, but it must not be silent
            // either: refinementFailed surfaces as a MatchingResult.Warnings entry (BuildWarnings), the
            // same non-fatal-degradation pattern this file already uses for classifyDegraded — this repo
            // has no logging framework to write to instead.
            try { featureAnalysis.Refine(lambda, family, entry.Source.NormalizedJpgPath, ruleSet); }
            catch { Interlocked.Increment(ref refinementFailed); }

            if (lambda.SelectedPhenotype is not null) Interlocked.Increment(ref assigned);
        });

        return (assigned, refinementFailed);
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
        IReadOnlyList<(ImageRecord_INPUT Record, UInt128 Hash)> hashEntries) {
        HashSet<string> exempt = new(this.configuration.DeduplicationExemptPhenotypes, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<DedupGroup> groups = classification.FindDuplicates(hashEntries);
        int removed = 0;

        foreach (DedupGroup group in groups) {
            // Skip groups whose canonical was rejected during classification —
            // its duplicates are not confirmed duplicates of a valid image.
            if (lambdaByImage.TryGetValue(group.Canonical, out ImageRecord_LAMBDA? canonLambda) && canonLambda.IsKo)
                continue;

            foreach (ImageRecord_INPUT duplicate in group.Duplicates) {
                ImageRecord_LAMBDA lambda = lambdaByImage[duplicate];

                // Already rejected (e.g. CLASSIFY_ERROR) — keep its original reason.
                if (lambda.IsKo) continue;

                // Illustrations, technical drawings, and labels are exempt so EU energy labels and
                // tech drawings pass; packshots, closeups, and zooms are removed as duplicates.
                if (lambda.SelectedPhenotype is not null && exempt.Contains(lambda.SelectedPhenotype)) continue;

                lambda.IsKo = true;
                lambda.KoReasonCode = "VISUAL_DUPLICATE";
                lambda.KoSafeMessage = $"Visual duplicate of {Path.GetFileName(group.Canonical.InitialFullName)}";
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Does not dispose <see cref="_sharedClassifier"/> — it is a process-wide shared resource (see
    /// <see cref="ImageClassifier.GetShared"/>) that outlives any individual MatchingService instance,
    /// exactly like nothing disposes YoloDetector's process-wide shared instance.
    /// </summary>
    public void Dispose() { this._disposed = true; }

    //  Helpers

    private static PhenotypeRuleSet LoadRuleSet() {
        return PhenotypeRuleSet.Load(ConfigLoader.RequireFile("ImageRoles.json"));
    }

    /// <summary>
    /// Writes each LAMBDA out as a JSON document under the job folder. This is the retrieval substrate
    /// for distributed deployment; a write failure must never fail an otherwise-successful job, so
    /// persistence is best-effort in the modular monolith.
    /// </summary>
    private static void PersistLambdaDocuments(IArtifactStore store, Guid jobId, IReadOnlyList<ImageRecord_LAMBDA> lambdas) {
        try {
            foreach (ImageRecord_LAMBDA lambda in lambdas)
                store.SaveLambdaDocument(jobId, lambda.InitialFullName, lambda);
        }
        catch {
            // Substrate persistence only — never block job completion on a document write.
        }
    }
}
