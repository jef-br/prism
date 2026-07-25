using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// Face-height cut factor for Tx_util_HeadCutter, bound from the "HeadCutter" section of
/// transform_Config.json (approximates the nose-to-lips boundary as a fraction of the detected
/// face-box height). No default — the value must be present in the JSON or deserialization fails
/// loud.
/// </summary>
public sealed class HeadCutterConfig : IValidatableConfig {
    public required double FaceHeightCutFactor { get; init; }

    public void Validate() {
        if (this.FaceHeightCutFactor is <= 0.0 or >= 1.0)
            throw new PrismConfigurationException("HeadCutter.FaceHeightCutFactor must be in (0,1)");
    }
}
