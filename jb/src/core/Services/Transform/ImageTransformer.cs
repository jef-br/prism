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
///
/// While <see cref="BypassPhenotypes"/> is on (temporary PoC gate), phenotype drops out of the
/// decision: the phenotype-null half of guard 1 is skipped, and intersecting images route to
/// <see cref="Tx_CropSquare"/> instead of <see cref="Tx_DetailCropper"/> (which is phenotype-driven).
/// Routing then depends only on <see cref="ImageRecord_LAMBDA.BoundingBox"/> and edge intersects.
/// </remarks>
public static class ImageTransformer {
    // Temporary gate — see jb/src/core/Images/Classify/jbtodo.md ("HANDMADE BY ME: Temporarily
    // GATE the phenotypes"). While true, routing ignores SelectedPhenotype so basic transforms
    // run off geometry alone. Flip to false once phenotype assignment is validated.
    // static readonly (not const) so the preserved phenotype path stays reachable and warning-free.
    private static readonly bool BypassPhenotypes = true;

    /// <summary>
    /// Selects and applies the transform strategy for <paramref name="lambda"/>, records the
    /// outcome in <see cref="ImageRecord_LAMBDA.OutputRecord"/>, and returns the record.
    /// <paramref name="seed"/> carries the resolved Excel + CLIP seeding signals (T-4820); the
    /// behaviour toggles (T-4860) read it. Null when no family/seeding context is available.
    /// </summary>
    public static ImageRecord_LAMBDA TransformImage(ImageRecord_LAMBDA lambda, Mat? colorMat, bool headcut, TransformParameters parameters, TransformSeed? seed = null) {
        PreferSubjectGeometry(lambda);
        TransformToggles toggles = TransformToggles.Resolve(seed, lambda.Subject);
        ApplyShadowAccounting(lambda, toggles, parameters);
        IImageTransformation transformer = SelectTransformer(lambda, colorMat, headcut, parameters, seed);
        ImageRecord_LAMBDA result = transformer.Transform(lambda);
        AppendTransformEvidence(result, toggles);
        return result;
    }

    // T-4870: fold the detection + toggle evidence into OutputRecord.SafeSummaryText — the carrier the
    // Export transform-manifest (lib/Export/jbtodo.md Todo 4) reads back. Compact, parseable, non-sensitive
    // (the pixel mask stays on lambda.Subject.MaskPng, never here).
    private static void AppendTransformEvidence(ImageRecord_LAMBDA lambda, TransformToggles toggles) {
        if (lambda.OutputRecord is null) return;
        SubjectDetection? s = lambda.Subject;
        string subject = s is null
            ? "subject=none"
            : string.Format(CultureInfo.InvariantCulture,
                "subject.producer={0}; subject.box={1},{2},{3},{4}; subject.conf={5:F2}; subject.intersects={6}{7}{8}{9}; subject.hardShadow={10}; subject.wholeFrame={11}",
                s.Producer, s.Box.X, s.Box.Y, s.Box.Width, s.Box.Height, s.Confidence,
                s.IntersectsTop ? "T" : "-", s.IntersectsBottom ? "B" : "-", s.IntersectsLeft ? "L" : "-", s.IntersectsRight ? "R" : "-",
                s.HasHardShadowEvidence, s.IsWholeFrameFallback);
        string toggleEvidence = string.Format(CultureInfo.InvariantCulture,
            "toggle.nearBg={0}; toggle.nonFlat={1}; toggle.shadow={2}",
            toggles.ProductNearBackground, toggles.NonFlatBackground, toggles.ShadowAccounting);
        lambda.OutputRecord.SafeSummaryText = $"{lambda.OutputRecord.SafeSummaryText} | {subject}; {toggleEvidence}";
    }

    // T-4860 shadow-accounting toggle: when the detector reports hard-shadow evidence and the subject
    // does not run off the bottom edge, trim the box bottom by the configured fraction so a cast shadow
    // below the product is not centred as product. The other two toggles (product≈background, non-flat
    // background) are computed for evidence and future upstream detection-effort steering.
    private static void ApplyShadowAccounting(ImageRecord_LAMBDA lambda, TransformToggles toggles, TransformParameters parameters) {
        if (!toggles.ShadowAccounting || lambda.BoundingBox is null) return;
        if (lambda.Features.GetValue("intersects-bottom") == "true") return;
        BoundingBox box = lambda.BoundingBox.Value;
        int shrink = (int)(box.Height * parameters.Crop.ShadowBottomShrinkFraction);
        if (shrink <= 0) return;
        box.Height = Math.Max(1, box.Height - shrink);
        box.Bottom = box.Top + box.Height;
        lambda.BoundingBox = box;
    }

    // T-4850: a confident subject detection (shadow/background-excluded box + per-edge intersects)
    // supersedes the legacy salient bbox. Promote it into the fields routing and every Tx strategy
    // already read, so the whole stage runs on the better geometry with no per-strategy change. The
    // whole-frame fallback (no subject found) is ignored — the legacy salient bbox stands.
    private static void PreferSubjectGeometry(ImageRecord_LAMBDA lambda) {
        if (lambda.Subject is not { IsWholeFrameFallback: false } subject) return;
        lambda.BoundingBox = subject.Box;
        lambda.Features.Set("intersects-top", subject.IntersectsTop ? "true" : "false", 1.0, "subject-detector");
        lambda.Features.Set("intersects-bottom", subject.IntersectsBottom ? "true" : "false", 1.0, "subject-detector");
        lambda.Features.Set("intersects-left", subject.IntersectsLeft ? "true" : "false", 1.0, "subject-detector");
        lambda.Features.Set("intersects-right", subject.IntersectsRight ? "true" : "false", 1.0, "subject-detector");
    }

    //  Strategy selection

    private static IImageTransformation SelectTransformer(ImageRecord_LAMBDA lambda, Mat? colorMat, bool headcut, TransformParameters parameters, TransformSeed? seed) {
        // Step 1 — prerequisites missing: route to conservative processor.
        // The phenotype-null guard is suppressed while phenotypes are bypassed.
        if (lambda.BoundingBox is null || (!BypassPhenotypes && lambda.SelectedPhenotype is null)) return new Tx_ProblemImageProcessor(parameters.ProblemImageProcessor, parameters.Output);

        // Step 2 — object touches at least one image edge.
        if (hasEdgeIntersect(lambda.Features)) {
            // DetailCropper is phenotype-driven; while bypassing, fall back to the square crop.
            if (BypassPhenotypes) return new Tx_CropSquare(parameters.Output);

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
    private static bool hasEdgeIntersect(ImageFeatureSnapshot ImgFeat) {
        return ImgFeat.GetValue("intersects-top") == "true" || ImgFeat.GetValue("intersects-bottom") == "true" || ImgFeat.GetValue("intersects-left") == "true" || ImgFeat.GetValue("intersects-right") == "true";
    }
}
