using System.Globalization;
using OpenCvSharp;

namespace Prism.Services.Transform;

/// <summary>
/// Entry point for the Transformed stage. Applies the confirmed routing matrix to select the
/// appropriate <see cref="IImageTransformation"/> strategy for each image, then delegates pixel
/// work to that strategy.
/// </summary>
/// <remarks>
/// Routing order (evaluated top-to-bottom, first match wins):
/// 1. <see cref="ImageRecord_LAMBDA.BoundingBox"/> null or <see cref="ImageRecord_LAMBDA.SelectedPhenotype"/> null
///    → <see cref="Tx_ProblemImageProcessor"/> (conservative resize, no crop or fill).
/// 2. Any edge intersects (<c>intersects-top/bottom/left/right</c> = "true"):
///    a. Phenotype is <c>"closeup-image"</c> or <c>"model-detail-closeup"</c>
///       AND det-slot is not in the exclusion range for this product type
///       → <see cref="Tx_DetailCropper"/>.
///    b. Otherwise → <see cref="Tx_CropSquare"/> (fallback for intersecting images).
/// 3. No edge intersects → <see cref="Tx_CenterAndStretch"/>.
/// </remarks>
public static class ImageTransformer {
    /// <summary>
    /// Selects and applies the transform strategy for <paramref name="lambda"/>, records the
    /// outcome in <see cref="ImageRecord_LAMBDA.OutputRecord"/>, and returns the record.
    /// <paramref name="seed"/> carries the resolved Excel + CLIP seeding signals (T-4820); the
    /// behaviour toggles (T-4860) read it. Null when no family/seeding context is available.
    /// </summary>
    public static ImageRecord_LAMBDA TransformImage(ImageRecord_LAMBDA lambda, Mat? colorMat, bool headcut, TransformParameters parameters, TransformSeed? seed = null) {
        TransformToggles toggles = TransformToggles.Resolve(seed, lambda.Subject);
        IImageTransformation transformer = SelectTransformer(lambda, colorMat, headcut, parameters, seed);
        ImageRecord_LAMBDA result = transformer.Transform(lambda);
        AppendTransformEvidence(result, toggles, lambda.SubjectGeometryPromoted);
        return result;
    }

    /// <summary>
    /// Settles the geometry the Transformed stage will crop on — subject promotion first, then shadow
    /// accounting — and records whether promotion fired. Called from
    /// <c>ImagePreProcessor.PreprocessAsync</c> before the upscale decision (T-4910), so upscale sizes
    /// against exactly the box the transform strategies later use rather than the pre-promotion one.
    /// Runs once per image; <see cref="TransformImage"/> assumes it has already run.
    /// </summary>
    public static void FinalizeGeometry(ImageRecord_LAMBDA lambda, TransformParameters parameters, TransformSeed? seed) {
        lambda.SubjectGeometryPromoted = PreferSubjectGeometry(lambda, parameters);
        ApplyShadowAccounting(lambda, TransformToggles.Resolve(seed, lambda.Subject), parameters);
    }

    // T-4870: fold the detection + toggle evidence into OutputRecord.SafeSummaryText — the carrier the
    // Export transform-manifest (lib/Export/jbtodo.md Todo 4) reads back. Compact, parseable, non-sensitive
    // (the pixel mask stays on lambda.Subject.MaskPng, never here).
    private static void AppendTransformEvidence(ImageRecord_LAMBDA lambda, TransformToggles toggles, bool promoted) {
        if (lambda.OutputRecord is null) return;
        SubjectDetectionResult? s = lambda.Subject;
        string subject = s is null
            ? "subject=none"
            : string.Format(CultureInfo.InvariantCulture,
                "subject.producer={0}; subject.box={1},{2},{3},{4}; subject.conf={5:F2}; subject.intersects={6}{7}{8}{9}; subject.hardShadow={10}; subject.wholeFrame={11}",
                s.Producer, s.Box.X, s.Box.Y, s.Box.Width, s.Box.Height, s.Confidence,
                s.IntersectsTop ? "T" : "-", s.IntersectsBottom ? "B" : "-", s.IntersectsLeft ? "L" : "-", s.IntersectsRight ? "R" : "-",
                s.HasHardShadowEvidence, s.IsWholeFrameFallback);
        string legacyBox = lambda.LegacySalientBox is { } lb
            ? string.Format(CultureInfo.InvariantCulture, "legacy.box={0},{1},{2},{3}", lb.X, lb.Y, lb.Width, lb.Height)
            : "legacy.box=none";
        string toggleEvidence = string.Format(CultureInfo.InvariantCulture,
            "toggle.nearBg={0}; toggle.nonFlat={1}; toggle.shadow={2}",
            toggles.ProductNearBackground, toggles.NonFlatBackground, toggles.ShadowAccounting);
        lambda.OutputRecord.SafeSummaryText = $"{lambda.OutputRecord.SafeSummaryText} | {subject}; {legacyBox}; promoted={promoted}; {toggleEvidence}";
    }

    // T-4860 shadow-accounting toggle: when the detector reports hard-shadow evidence and the subject
    // does not run off the bottom edge, trim the box bottom by the configured fraction so a cast shadow
    // below the product is not centred as product. Scoped to the Tx_CenterAndStretch route only — the
    // shrink is not part of the crop-square or detail-crop contracts. The other two toggles
    // (product≈background, non-flat background) are computed for evidence and future upstream
    // detection-effort steering.
    private static void ApplyShadowAccounting(ImageRecord_LAMBDA lambda, TransformToggles toggles, TransformParameters parameters) {
        // Routing is decided by geometry alone here rather than by inspecting the selected strategy,
        // because the shrink now runs before SelectTransformer. Same set of images either way: only a
        // non-null, edge-free bbox reaches Tx_CenterAndStretch, and that also covers the old separate
        // intersects-bottom guard.
        if (!FinalOutputSize.RoutesToCenterAndStretch(lambda) || !toggles.ShadowAccounting) return;
        BoundingBox box = lambda.BoundingBox!.Value;
        int shrink = (int)(box.Height * parameters.Crop.ShadowBottomShrinkFraction);
        if (shrink <= 0) return;
        box.Height = Math.Max(1, box.Height - shrink);
        box.Bottom = box.Top + box.Height;
        lambda.BoundingBox = box;
    }

    // T-4850: a confident subject detection (shadow/background-excluded box + per-edge intersects)
    // supersedes the legacy salient bbox. Promote it into the fields routing and every Tx strategy
    // already read, so the whole stage runs on the better geometry with no per-strategy change. The
    // whole-frame fallback (no subject found) and a detection below the configured confidence floor are
    // both ignored — the legacy salient bbox stands, captured on LegacySalientBox for A/B evidence.
    // Returns true when promotion actually fired.
    private static bool PreferSubjectGeometry(ImageRecord_LAMBDA lambda, TransformParameters parameters) {
        if (lambda.Subject is not { IsWholeFrameFallback: false } subject) return false;
        if (subject.Confidence < parameters.Crop.SubjectPromotionMinConfidence) return false;
        lambda.LegacySalientBox = lambda.BoundingBox;
        lambda.BoundingBox = subject.Box;
        SetFlag(lambda, "intersects-top", subject.IntersectsTop);
        SetFlag(lambda, "intersects-bottom", subject.IntersectsBottom);
        SetFlag(lambda, "intersects-left", subject.IntersectsLeft);
        SetFlag(lambda, "intersects-right", subject.IntersectsRight);
        WriteDerivedEdgeFeatures(lambda, subject);
        return true;
    }

    private const string SubjectDetectorProducer = "subject-detector";

    private static void SetFlag(ImageRecord_LAMBDA lambda, string featureId, bool value) {
        lambda.Features.Set(featureId, value ? "true" : "false", 1.0, SubjectDetectorProducer);
    }

    // T-4955: intersection-count and fully-in-frame are derived from the four intersects-* booleans,
    // so promoting the detector's booleans without recomputing them leaves the snapshot contradicting
    // itself — measured at 36 of 86 SPACINI29 images (42%) before this ran. It matters because the
    // phenotype rules read both halves in one evaluation: front-on-model-partial gates on
    // intersects-top|bottom while front-on-model-full-product gates on intersection-count=0, so a
    // stale pair lets one image satisfy two mutually-exclusive rules and first-rule-wins picks wrong.
    private static void WriteDerivedEdgeFeatures(ImageRecord_LAMBDA lambda, SubjectDetectionResult subject) {
        int count = (subject.IntersectsTop ? 1 : 0) + (subject.IntersectsBottom ? 1 : 0)
                  + (subject.IntersectsLeft ? 1 : 0) + (subject.IntersectsRight ? 1 : 0);
        lambda.Features.Set("intersection-count", count.ToString(CultureInfo.InvariantCulture), 1.0, SubjectDetectorProducer);
        SetFlag(lambda, "fully-in-frame", count == 0);
    }

    //  Strategy selection

    private static IImageTransformation SelectTransformer(ImageRecord_LAMBDA lambda, Mat? colorMat, bool headcut, TransformParameters parameters, TransformSeed? seed) {
        // Step 1 — prerequisites missing: route to conservative processor.
        if (lambda.BoundingBox is null || lambda.SelectedPhenotype is null) return new Tx_ProblemImageProcessor(parameters.ProblemImageProcessor, parameters.Output);

        // Step 2 — object touches at least one image edge.
        if (FinalOutputSize.HasEdgeIntersect(lambda.Features)) {
            bool isCloseupPhenotype = lambda.SelectedPhenotype is "closeup-image" or "model-detail-closeup";
            if (!isCloseupPhenotype || IsDetailCropperDetSlotExcluded(lambda)) return new Tx_CropSquare(parameters.Output);

            CropTransformSettings crop = parameters.Crop;
            return new Tx_DetailCropper(crop.CropCoverage, crop.CropExtensionOneSided, crop.CropExtensionBiDirectional, headcut, colorMat, parameters.DetailCropper, parameters.BgStretch, parameters.HeadCutter);
        }

        // Step 3 — object fully in frame: center on canvas and fill.
        return new Tx_CenterAndStretch(parameters.Crop.WhiteSpaceMargin, headcut, colorMat, parameters.BgStretch, parameters.HeadCutter);
    }

    /// <summary>
    /// Returns true when the image's det-slot falls in the exclusion range for its product type,
    /// disqualifying it from <see cref="Tx_DetailCropper"/> routing.
    /// Default products exclude slots 0–2; clothing products (<c>topwear</c>, <c>bottomwear</c>)
    /// exclude slots 0–1.
    /// </summary>
    private const int DefaultDetSlotExclusionMax = 2;

    private static bool IsDetailCropperDetSlotExcluded(ImageRecord_LAMBDA lambda) {
        bool isClothing = lambda.ProductTypeId is "topwear" or "bottomwear";
        return isClothing ? lambda.DetOrder <= 1 : lambda.DetOrder <= DefaultDetSlotExclusionMax;
    }
}
