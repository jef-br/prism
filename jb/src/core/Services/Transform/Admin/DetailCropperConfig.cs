using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// Adjacent-edge crop cap for Tx_DetailCropper's 2-adjacent-edge branch, bound from the
/// "DetailCropper" section of transform_Config.json. The Coverage/OneSided/BiDirectional budgets live
/// in the sibling "Crop" section (see CropTransformSettings) — this section covers only the tunable
/// local to this class. No default — the value must be present in the JSON or deserialization
/// fails loud.
/// </summary>
public sealed class DetailCropperConfig : IValidatableConfig {
    public required double AdjacentCropCap { get; init; }

    public void Validate() {
        if (this.AdjacentCropCap is <= 0.0 or >= 1.0)
            throw new PrismConfigurationException("DetailCropper.AdjacentCropCap must be in (0,1)");
    }
}
