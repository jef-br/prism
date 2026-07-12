namespace Prism.Services.Transform;

/// <summary>
/// Tier ratio thresholds and seam-feathering width for Tx_util_BgStretch, bound from the
/// "BgStretch" section of transform_Config.json. No defaults — every value must be present in the
/// JSON or deserialization fails loud.
/// </summary>
public sealed class BgStretchConfig
{
    public required float Tier1MaxRatio { get; init; }
    public required float Tier2MaxRatio { get; init; }
    public required float Tier4MinRatio { get; init; }
    public required int FeatherPx { get; init; }
}
