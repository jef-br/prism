using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// Crop-sizing and positioning budgets consumed by <see cref="ImageTransformer"/> when constructing a
/// transform strategy, bound from the "Crop" section of transform_Config.json. No defaults — every
/// value must be present in the JSON or deserialization fails loud.
/// </summary>
public sealed class CropTransformSettings : IValidatableConfig
{
    // Margin's upper bound: Tx_CenterAndStretch divides by (1 - 2*margin) to size the resized
    // product, which collapses to zero (margin=0.5) or goes negative (margin>0.5) — validation bound.
    private const double WhiteSpaceMarginUpperBound = 0.49;

    public required double WhiteSpaceMargin { get; init; }
    public required double CropCoverage { get; init; }
    public required double CropExtensionOneSided { get; init; }
    public required double CropExtensionBiDirectional { get; init; }

    public void Validate()
    {
        List<string> problems = [];

        // Margin's upper bound is 0.49, not 1.0: Tx_CenterAndStretch divides by (1 - 2*margin) to size
        // the resized product, which collapses to zero (margin=0.5) or goes negative (margin>0.5).
        if (this.WhiteSpaceMargin is < 0.0 or > WhiteSpaceMarginUpperBound) problems.Add("Crop.WhiteSpaceMargin must be in [0,0.49]");
        if (this.CropCoverage is < 0.0 or > 1.0) problems.Add("Crop.CropCoverage must be in [0,1]");
        if (this.CropExtensionOneSided is < 0.0 or > 1.0) problems.Add("Crop.CropExtensionOneSided must be in [0,1]");
        if (this.CropExtensionBiDirectional is < 0.0 or > 1.0) problems.Add("Crop.CropExtensionBiDirectional must be in [0,1]");

        if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
    }
}
