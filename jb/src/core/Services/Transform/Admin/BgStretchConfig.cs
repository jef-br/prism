using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// Tier ratio thresholds and seam-feathering width for Tx_util_BgStretch, bound from the
/// "BgStretch" section of transform_Config.json. No defaults — every value must be present in the
/// JSON or deserialization fails loud.
/// </summary>
public sealed class BgStretchConfig : IValidatableConfig
{
    public required float Tier1MaxRatio { get; init; }
    public required float Tier2MaxRatio { get; init; }
    public required float Tier4MinRatio { get; init; }
    public required int FeatherPx { get; init; }

    public void Validate()
    {
        List<string> problems = [];

        if (Tier1MaxRatio <= 1f) problems.Add("BgStretch.Tier1MaxRatio must be > 1");
        if (Tier2MaxRatio <= Tier1MaxRatio) problems.Add("BgStretch.Tier2MaxRatio must be > Tier1MaxRatio");
        if (Tier4MinRatio <= Tier2MaxRatio) problems.Add("BgStretch.Tier4MinRatio must be > Tier2MaxRatio");
        if (FeatherPx < 0) problems.Add("BgStretch.FeatherPx must be >= 0");

        if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
    }
}
