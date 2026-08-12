using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prism.Services.Matching;

/*JB: Note to Claude: Haar-Features can help detect facial features. */

/// <summary>
/// Extracts measurable ImageFeatures from a normalized image using CPU-only methods.
/// Uses ImageSharp for pixel-level analysis; does not require a GPU or external service.
///
/// Features requiring heavier models (pose estimation, orientation, human detection)
/// are recorded as UNKNOWN and will be populated by the CLIP-backed <see cref="ImageClassifier"/>
/// or specialized analyzers when those are implemented.
/// </summary>
public static class ImageFeatureAnalyzer {
    /// <summary>
    /// Thresholds and confidence weights for ImageFeatureAnalyzer, bound from the
    /// "ImageFeatureAnalyzer" section of ClassifyConfig.json. No defaults — every value must be
    /// present in the JSON or deserialization fails loud.
    /// </summary>
    public sealed class Config : IValidatableConfig {
        // Validation bound, not tunable: AlphaOpaqueThreshold is a byte-range alpha value.
        private const int AlphaThresholdUpperBound = 255;

        /// <summary>Corner-sample color variance below which the background counts as a flat solid.</summary>
        public required float BackgroundVarianceSolidColorMax { get; init; }

        /// <summary>Corner-sample color variance above which the background counts as a real-life scene.</summary>
        public required float BackgroundVarianceLifestyleMin { get; init; }

        /// <summary>Per-channel minimum on the [0,1] scale for the sampled background to count as near-white.</summary>
        public required float NearWhiteChannelMin { get; init; }

        /// <summary>Alpha value (0-255) at or above which a pixel counts as opaque.</summary>
        public required int AlphaOpaqueThreshold { get; init; }

        /// <summary>Maximum raw 8-bit channel value, used to normalize R/G/B into [0,1].</summary>
        public required float MaxChannelValueF { get; init; }

        /// <summary>Row/column stride used when sampling pixels for skin-tone detection.</summary>
        public required int PixelSampleStride { get; init; }

        /// <summary>Number of color channels averaged into the background-variance statistic.</summary>
        public required int ChannelCount { get; init; }

        /// <summary>Confidence written on white-background.</summary>
        public required double WhiteBackgroundConfidence { get; init; }

        /// <summary>Confidence written on lifestyle-background=false from low corner variance.</summary>
        public required double LifestyleBackgroundSolidConfidence { get; init; }

        /// <summary>Confidence written on lifestyle-background=true from high corner variance.</summary>
        public required double LifestyleBackgroundRealLifeConfidence { get; init; }

        /// <summary>Confidence written on background-type.</summary>
        public required double BackgroundTypeConfidence { get; init; }

        /// <summary>Confidence written on every intersects-* / intersection-count / fully-in-frame feature.</summary>
        public required double EdgeIntersectionConfidence { get; init; }

        /// <summary>Confidence written on skin-tone-area.</summary>
        public required double SkinToneAreaConfidence { get; init; }

        public void Validate() {
            List<string> problems = [];

            if (this.BackgroundVarianceSolidColorMax <= 0f) problems.Add("ImageFeatureAnalyzer.BackgroundVarianceSolidColorMax must be > 0");
            if (this.BackgroundVarianceLifestyleMin <= this.BackgroundVarianceSolidColorMax) problems.Add("ImageFeatureAnalyzer.BackgroundVarianceLifestyleMin must be > BackgroundVarianceSolidColorMax");
            if (this.NearWhiteChannelMin is <= 0f or > 1f) problems.Add("ImageFeatureAnalyzer.NearWhiteChannelMin must be in (0,1]");
            if (this.AlphaOpaqueThreshold is < 0 or > AlphaThresholdUpperBound) problems.Add("ImageFeatureAnalyzer.AlphaOpaqueThreshold must be in [0,255]");
            if (this.MaxChannelValueF <= 0f) problems.Add("ImageFeatureAnalyzer.MaxChannelValueF must be > 0");
            if (this.PixelSampleStride < 1) problems.Add("ImageFeatureAnalyzer.PixelSampleStride must be >= 1");
            if (this.ChannelCount < 1) problems.Add("ImageFeatureAnalyzer.ChannelCount must be >= 1");
            if (this.WhiteBackgroundConfidence is < 0.0 or > 1.0) problems.Add("ImageFeatureAnalyzer.WhiteBackgroundConfidence must be in [0,1]");
            if (this.LifestyleBackgroundSolidConfidence is < 0.0 or > 1.0) problems.Add("ImageFeatureAnalyzer.LifestyleBackgroundSolidConfidence must be in [0,1]");
            if (this.LifestyleBackgroundRealLifeConfidence is < 0.0 or > 1.0) problems.Add("ImageFeatureAnalyzer.LifestyleBackgroundRealLifeConfidence must be in [0,1]");
            if (this.BackgroundTypeConfidence is < 0.0 or > 1.0) problems.Add("ImageFeatureAnalyzer.BackgroundTypeConfidence must be in [0,1]");
            if (this.EdgeIntersectionConfidence is < 0.0 or > 1.0) problems.Add("ImageFeatureAnalyzer.EdgeIntersectionConfidence must be in [0,1]");
            if (this.SkinToneAreaConfidence is < 0.0 or > 1.0) problems.Add("ImageFeatureAnalyzer.SkinToneAreaConfidence must be in [0,1]");

            if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Analyzes the pre-loaded <paramref name="image"/> and writes all detectable
    /// feature values into <paramref name="snapshot"/>.
    /// Features that cannot be determined are recorded as UNKNOWN.
    /// </summary>
    public static void Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot, AnalyzerParameters parameters, Config cfg) {
        AnalyzeGeometry(image, snapshot);
        AnalyzeBackground(image, snapshot, out _, out _, out _, cfg);
        WriteEdgeIntersections(SubjectEdgeDetector.Detect(image), snapshot, cfg);
        AnalyzeSkinTone(image, snapshot, parameters.SkinTone, cfg);
        AnalyzeInterior(image, snapshot, parameters.Interior);
        AnalyzeIllustration(image, snapshot, parameters.IsIllustration);
        RecordUnknownFeatures(snapshot);
    }

    /// <summary>
    /// Post-match refinement chain. Runs after the Matched stage, when the image's family (IEM)
    /// is known, and narrows the phenotype pool wave by wave in cheap-first, most-eliminating-first
    /// order: IEM + filename evidence, then detector-backed visual analyzers. At the start the image
    /// qualifies for every phenotype; each wave eliminates those with strong contra-evidence.
    /// The final assignment overwrites the provisional phenotype set at the Classified stage.
    /// </summary>
    public static void Refine(ImageRecord_LAMBDA lambda, FamilyIDRecord? family, string? imagePath, PhenotypeRuleSet ruleSet, AnalyzerParameters parameters, string? yoloModelPath, bool aiDetectionEnabled, ProductTypeResolver productTypes, Action<ImageRecord_LAMBDA, Image<Rgba32>>? subjectStep) {
        PhenotypePool pool = new(ruleSet);

        // Wave 1 — IEM + filename evidence. Phase-1 measurements (background, edge intersections)
        // already sit in the snapshot, so this first elimination is also the big intersection cut.
        Analyzer_ProductType.Analyze(lambda, family, productTypes);
        Analyzer_FilenameEvidence.Analyze(lambda, productTypes, parameters.Filename);
        pool.Eliminate(lambda.Features);

        if (imagePath is not null && File.Exists(imagePath)) {
            using Image<Rgba32> image = Image.Load<Rgba32>(imagePath);

            // Wave 2 — human evidence: YOLO person detections.
            IReadOnlyList<YoloDetection> detections = yoloModelPath is null
                ? []
                : YoloDetector.GetShared(yoloModelPath).Detect(image, parameters.Yolo);
            Analyzer_HasHuman.Analyze(detections, lambda.Features, parameters.Yolo, aiDetectionEnabled);
            pool.Eliminate(lambda.Features);

            // Wave 3 — remaining visual analyzers: geometry from the shared subject box, then
            // colors sampling the same box, exposure, and detection-count features.
            SubjectBox? subject = Analyzer_SubjectGeometry.Analyze(image, detections, lambda.Features, parameters.SubjectGeometry);
            IReadOnlyList<ColorBucket> buckets = Analyzer_DominantColors.Analyze(image, subject, lambda.Features, parameters.Colors, parameters.SkinTone);
            Analyzer_ProductColor.Analyze(buckets, lambda.Features, parameters.Colors);
            Analyzer_BackgroundColor.Analyze(image, lambda.Features, parameters.Colors);
            Analyzer_Exposure.Analyze(image, lambda.Features, parameters.Exposure, parameters.Colors);
            Analyzer_MultipleProducts.Analyze(detections, lambda.Features, parameters.MultipleProducts);

            // Subject isolation runs last in wave 3 and, critically, before the phenotype is finalized.
            // It needs product-color/background-color (measured three lines up) to steer itself, and
            // shadow-present has to exist before the rules evaluate or it would always read UNKNOWN.
            // The step itself lives in Prism.Core: the detector is OpenCvSharp and this project is not.
            subjectStep?.Invoke(lambda, image);
            Analyzer_ShadowPresence.Analyze(lambda.Subject, lambda.Features, parameters.ShadowPresence);
        }

        pool.Eliminate(lambda.Features);
        FinalizePhenotype(lambda, pool, ruleSet);
    }

    // The refined phenotype: first fully-satisfied rule wins; otherwise the provisional pick
    // survives only while it is still in the pool. CandidatePhenotypes lists the selected
    // phenotype first, then the remaining uncontradicted pool members in rule order.
    private static void FinalizePhenotype(ImageRecord_LAMBDA lambda, PhenotypePool pool, PhenotypeRuleSet ruleSet) {
        string[] satisfied = ruleSet.EvaluateCandidates(lambda.Features);
        string? provisional = lambda.SelectedPhenotype;

        string? selected = satisfied.Length > 0
            ? satisfied[0]
            : provisional is not null && pool.Contains(provisional) ? provisional : null;

        List<string> candidates = [];
        if (selected is not null) candidates.Add(selected);
        foreach (string id in pool.Candidates) {
            if (!candidates.Contains(id, StringComparer.OrdinalIgnoreCase))
                candidates.Add(id);
        }

        lambda.SelectedPhenotype = selected;
        lambda.CandidatePhenotypes = [.. candidates];
    }

    //  Geometry

    private static void AnalyzeGeometry(Image<Rgba32> image, ImageFeatureSnapshot snapshot) {
        double aspectRatio = (double)image.Width / image.Height;
        snapshot.Set("aspect-ratio",
            aspectRatio.ToString("F4", CultureInfo.InvariantCulture), 1.0, "geometry");
    }

    //  Background 

    private static void AnalyzeBackground(Image<Rgba32> image, ImageFeatureSnapshot snapshot, out float bgR, out float bgG, out float bgB, Config cfg) {
        // Import composites every accepted input format onto white before any analyzer runs (T-5030),
        // so no image this method ever sees carries a real alpha channel — the feature is now a
        // structural fact rather than a per-image measurement. clipping-path was removed outright in
        // the same ticket: it only ever meant "this file had an alpha channel", which cannot be true
        // downstream of Import, so it was deleted rather than given a new meaning.
        snapshot.Set("transparent-background", "false", 1.0, "imagesharp");

        SampleCorners(image, out bgR, out bgG, out bgB, out float variance, cfg);

        bool nearWhite = bgR > cfg.NearWhiteChannelMin && bgG > cfg.NearWhiteChannelMin && bgB > cfg.NearWhiteChannelMin;
        snapshot.Set("white-background", nearWhite ? "true" : "false", cfg.WhiteBackgroundConfidence, "imagesharp");

        string bgType;
        if (variance < cfg.BackgroundVarianceSolidColorMax) {
            bgType = "SOLIDCOLOR";
            snapshot.Set("lifestyle-background", "false", cfg.LifestyleBackgroundSolidConfidence, "imagesharp");
        }
        else if (variance > cfg.BackgroundVarianceLifestyleMin) {
            bgType = "REALLIFE";
            snapshot.Set("lifestyle-background", "true", cfg.LifestyleBackgroundRealLifeConfidence, "heuristic");
        }
        else {
            bgType = "UNKNOWN";
            snapshot.Set("lifestyle-background", "UNKNOWN", 0.0, "heuristic");
        }

        snapshot.Set("background-type", bgType, cfg.BackgroundTypeConfidence, "imagesharp");
    }

    //  Border intersections 

    private static void WriteEdgeIntersections(SubjectEdgeDetectionResult r, ImageFeatureSnapshot snapshot, Config cfg) {
        snapshot.Set("intersects-top", r.IntersectsTop ? "true" : "false", cfg.EdgeIntersectionConfidence, "heuristic");
        snapshot.Set("intersects-bottom", r.IntersectsBottom ? "true" : "false", cfg.EdgeIntersectionConfidence, "heuristic");
        snapshot.Set("intersects-left", r.IntersectsLeft ? "true" : "false", cfg.EdgeIntersectionConfidence, "heuristic");
        snapshot.Set("intersects-right", r.IntersectsRight ? "true" : "false", cfg.EdgeIntersectionConfidence, "heuristic");
        snapshot.Set("intersection-count", r.IntersectionCount.ToString(CultureInfo.InvariantCulture), cfg.EdgeIntersectionConfidence, "heuristic");
        snapshot.Set("fully-in-frame", r.FullyInFrame ? "true" : "false", cfg.EdgeIntersectionConfidence, "heuristic");
    }

    //  Skin tone

    private static void AnalyzeSkinTone(Image<Rgba32> image, ImageFeatureSnapshot snapshot, SkinToneAnalyzerConfig skinCfg, Config cfg) {
        int total = 0;
        int skinPx = 0;

        // Sample every other pixel for performance; row spans instead of the per-pixel indexer.
        image.ProcessPixelRows(accessor => {
            for (int y = 0; y < accessor.Height; y += cfg.PixelSampleStride) {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x += cfg.PixelSampleStride) {
                    Rgba32 px = row[x];
                    if (px.A < cfg.AlphaOpaqueThreshold) continue;
                    total++;
                    if (AnalyzerMath.IsSkinTone(px, skinCfg)) skinPx++;
                }
            }
        });

        float ratio = total == 0 ? 0f : (float)skinPx / total;
        snapshot.Set("skin-tone-area",
            ratio.ToString("F4", CultureInfo.InvariantCulture), cfg.SkinToneAreaConfidence, "imagesharp");
    }

    //  Interior detection

    private static void AnalyzeInterior(Image<Rgba32> image, ImageFeatureSnapshot snapshot, Analyzer_Interior.Config cfg) {
        bool detected = Analyzer_Interior.Analyze(image, cfg);
        snapshot.Set("interior-detected", detected ? "true" : "false", 1.0, "geometry");
    }

    //  Illustration / technical drawing detection

    private static void AnalyzeIllustration(Image<Rgba32> image, ImageFeatureSnapshot snapshot, Analyzer_IsIllustration.Config cfg) {
        bool detected = Analyzer_IsIllustration.Analyze(image, cfg);
        snapshot.Set("is-illustration", detected ? "true" : "false", 1.0, "topology");
    }

    //  Stubs for features that need heavier models

    private static void RecordUnknownFeatures(ImageFeatureSnapshot snapshot) {
        // These will be populated by the CLIP-backed classifier or specialized detectors.
        SetUnknownIfNotSet(snapshot, "hero-is-human");
        SetUnknownIfNotSet(snapshot, "hero-orientation");
        SetUnknownIfNotSet(snapshot, "has-human");
        SetUnknownIfNotSet(snapshot, "human-count");
        SetUnknownIfNotSet(snapshot, "head-visible");
        SetUnknownIfNotSet(snapshot, "body-visible");
        SetUnknownIfNotSet(snapshot, "product-type-label");
        SetUnknownIfNotSet(snapshot, "multiple-products");
        SetUnknownIfNotSet(snapshot, "overlap-count");
        SetUnknownIfNotSet(snapshot, "product-coverage-ratio");
        SetUnknownIfNotSet(snapshot, "image-occupancy");
        SetUnknownIfNotSet(snapshot, "crop-tightness");
        SetUnknownIfNotSet(snapshot, "dominant-colors");
        SetUnknownIfNotSet(snapshot, "product-color");
        SetUnknownIfNotSet(snapshot, "background-color");
        SetUnknownIfNotSet(snapshot, "product-aspect-ratio");
        SetUnknownIfNotSet(snapshot, "vertical-centering");
        SetUnknownIfNotSet(snapshot, "horizontal-centering");
    }

    private static void SetUnknownIfNotSet(ImageFeatureSnapshot snapshot, string featureId) {
        if (!snapshot.TryGet(featureId, out _))
            snapshot.Set(featureId, "UNKNOWN", 0.0, "heuristic");
    }

    //  Pixel helpers

    private static void SampleCorners(Image<Rgba32> image, out float avgR, out float avgG, out float avgB, out float variance, Config cfg) {
        int cw = Math.Max(1, image.Width / 10);
        int ch = Math.Max(1, image.Height / 10);

        // Single pass over the four corner blocks via row spans, accumulating sums and squared sums.
        // variance = E[x²] − E[x]² summed over channels — same statistic as the former two-pass
        // mean-then-deviation computation.
        double sumR = 0, sumG = 0, sumB = 0;
        double sumR2 = 0, sumG2 = 0, sumB2 = 0;
        int n = 0;

        image.ProcessPixelRows(accessor => {
            int width = accessor.Width;
            int height = accessor.Height;

            void AddPixel(Rgba32 px) {
                if (px.A < cfg.AlphaOpaqueThreshold) return;
                float r = px.R / cfg.MaxChannelValueF, g = px.G / cfg.MaxChannelValueF, b = px.B / cfg.MaxChannelValueF;
                sumR += r; sumG += g; sumB += b;
                sumR2 += r * r; sumG2 += g * g; sumB2 += b * b;
                n++;
            }

            void AddRowCorners(Span<Rgba32> row) {
                for (int dx = 0; dx < cw; dx++) {
                    AddPixel(row[dx]);
                    if (width - 1 - dx >= cw) AddPixel(row[width - 1 - dx]);
                }
            }

            for (int dy = 0; dy < ch; dy++) {
                AddRowCorners(accessor.GetRowSpan(dy));
                int bottomY = height - 1 - dy;
                if (bottomY >= ch) AddRowCorners(accessor.GetRowSpan(bottomY));
            }
        });

        if (n == 0) { avgR = avgG = avgB = variance = 0f; return; }

        avgR = (float)(sumR / n);
        avgG = (float)(sumG / n);
        avgB = (float)(sumB / n);

        double varSum = (sumR2 - sumR * sumR / n) + (sumG2 - sumG * sumG / n) + (sumB2 - sumB * sumB / n);
        variance = (float)(varSum / (n * cfg.ChannelCount));
    }

}
