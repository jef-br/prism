namespace Prism.Core;

/// <summary>
/// Entry point for the Transformed stage. Applies the confirmed routing matrix to select the
/// appropriate <see cref="IImageTransformation"/> strategy for each image, then delegates pixel
/// work to that strategy.
/// </summary>
/// <remarks>
/// Routing order (evaluated top-to-bottom, first match wins):
/// 1. <c>salient-bbox</c> UNKNOWN or <see cref="ImageRecord_LAMBDA.SelectedPhenotype"/> null
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
/// Routing then depends only on <c>salient-bbox</c> and edge intersects.
/// </remarks>
public static class ImageTransformer
{
    // Temporary gate — see jb/src/core/Images/Classify/jbtodo.md ("HANDMADE BY ME: Temporarily
    // GATE the phenotypes"). While true, routing ignores SelectedPhenotype so basic transforms
    // run off geometry alone. Flip to false once phenotype assignment is validated.
    // static readonly (not const) so the preserved phenotype path stays reachable and warning-free.
    private static readonly bool BypassPhenotypes = true;

    /// <summary>
    /// Selects and applies the transform strategy for <paramref name="lambda"/>, records the
    /// outcome in <see cref="ImageRecord_LAMBDA.TransformationResult"/>, and returns the record.
    /// </summary>
    public static ImageRecord_LAMBDA TransformImage(ImageRecord_LAMBDA lambda)
    {
        IImageTransformation transformer = SelectTransformer(lambda);
        return transformer.Transform(lambda);
    }

    //  Strategy selection 

    private static IImageTransformation SelectTransformer(ImageRecord_LAMBDA lambda)
    {
        // Step 1 — prerequisites missing: route to conservative processor.
        // The phenotype-null guard is suppressed while phenotypes are bypassed.
        if (lambda.Features.GetValue("salient-bbox") == "UNKNOWN" || (!BypassPhenotypes && lambda.SelectedPhenotype is null))
            return new Tx_ProblemImageProcessor();

        bool hasEdgeIntersect = lambda.Features.GetValue("intersects-top")    == "true"
                             || lambda.Features.GetValue("intersects-bottom")  == "true"
                             || lambda.Features.GetValue("intersects-left")    == "true"
                             || lambda.Features.GetValue("intersects-right")   == "true";

        // Step 2 — object touches at least one image edge.
        if (hasEdgeIntersect)
        {
            // DetailCropper is phenotype-driven; while bypassing, fall back to the square crop.
            if (BypassPhenotypes)
                return new Tx_CropSquare();

            bool isCloseupPhenotype = lambda.SelectedPhenotype is "closeup-image" or "model-detail-closeup";
            return isCloseupPhenotype && !IsDetailCropperDetSlotExcluded(lambda)
                ? new Tx_DetailCropper()
                : new Tx_CropSquare();
        }

        // Step 3 — object fully in frame: center on canvas and fill.
        return new Tx_CenterAndStretch();
    }

    /// <summary>
    /// Returns true when the image's det-slot falls in the exclusion range for its product type,
    /// disqualifying it from <see cref="Tx_DetailCropper"/> routing.
    /// Default products exclude slots 0–2; clothing products (<c>clothing-*</c>) exclude slots 0–1.
    /// </summary>
    private static bool IsDetailCropperDetSlotExcluded(ImageRecord_LAMBDA lambda)
    {
        bool isClothing = lambda.ProductTypeId?
            .StartsWith("clothing-", System.StringComparison.OrdinalIgnoreCase) == true;
        return isClothing ? lambda.DetOrder <= 1 : lambda.DetOrder <= 2;
    }
}
