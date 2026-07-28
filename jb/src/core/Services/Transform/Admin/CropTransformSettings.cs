using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// Crop-sizing and positioning budgets consumed by <see cref="ImageTransformer"/> when constructing a
/// transform strategy, bound from the "Crop" section of transform_Config.json. No defaults — every
/// value must be present in the JSON or deserialization fails loud.
/// </summary>
public sealed class CropTransformSettings : IValidatableConfig {
    // Margin's upper bound: Tx_CenterAndStretch divides by (1 - 2*margin) to size the resized
    // product, which collapses to zero (margin=0.5) or goes negative (margin>0.5) — validation bound.
    private const double WhiteSpaceMarginUpperBound = 0.49;

    // ShadowBottomShrinkFraction's upper bound: at 0.5 the shrink would trim the entire box height —
    // validation bound.
    private const double ShadowBottomShrinkFractionUpperBound = 0.5;

    public required double WhiteSpaceMargin { get; init; }
    public required double CropCoverage { get; init; }
    public required double CropExtensionOneSided { get; init; }
    public required double CropExtensionBiDirectional { get; init; }

    // T-4860 shadow-accounting: fraction of the subject box height trimmed from the bottom when the
    // detector reports hard-shadow evidence and the subject does not run off the bottom edge, so a cast
    // shadow below the product is not centred as if it were product.
    public required double ShadowBottomShrinkFraction { get; init; }

    // T-4850: minimum detector confidence required before a detected subject box is promoted over the
    // legacy salient bbox. A sparse-blob detection can score as low as 0.1; below this floor the legacy
    // bbox stands, same as the whole-frame fallback path.
    public required double SubjectPromotionMinConfidence { get; init; }

    public void Validate() {
        List<string> problems = [];

        // Margin's upper bound is 0.49, not 1.0: Tx_CenterAndStretch divides by (1 - 2*margin) to size
        // the resized product, which collapses to zero (margin=0.5) or goes negative (margin>0.5).
        if (this.WhiteSpaceMargin is < 0.0 or > WhiteSpaceMarginUpperBound) problems.Add("Crop.WhiteSpaceMargin must be in [0,0.49]");
        if (this.CropCoverage is < 0.0 or > 1.0) problems.Add("Crop.CropCoverage must be in [0,1]");
        if (this.CropExtensionOneSided is < 0.0 or > 1.0) problems.Add("Crop.CropExtensionOneSided must be in [0,1]");
        if (this.CropExtensionBiDirectional is < 0.0 or > 1.0) problems.Add("Crop.CropExtensionBiDirectional must be in [0,1]");
        if (this.ShadowBottomShrinkFraction is < 0.0 or > ShadowBottomShrinkFractionUpperBound) problems.Add("Crop.ShadowBottomShrinkFraction must be in [0,0.5]");
        if (this.SubjectPromotionMinConfidence is <= 0.0 or > 1.0) problems.Add("Crop.SubjectPromotionMinConfidence must be in (0,1]");

        if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
    }
}
