namespace Prism.Services.Transform;

/// <summary>
/// Adjacent-edge crop cap for Tx_DetailCropper's 2-adjacent-edge branch, bound from the
/// "DetailCropper" section of transform_Config.json. Coverage/OneSided/BiDirectional budgets stay
/// sourced from Prism_Config.json via CropTransformSettings — this section covers only the tunable
/// local to this class. No default — the value must be present in the JSON or deserialization
/// fails loud.
/// </summary>
public sealed class DetailCropperConfig
{
    public required double AdjacentCropCap { get; init; }
}
