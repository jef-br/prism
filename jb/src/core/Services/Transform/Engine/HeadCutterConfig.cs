namespace Prism.Services.Transform;

/// <summary>
/// Face-height cut factor for Tx_util_HeadCutter, bound from the "HeadCutter" section of
/// transform_Config.json (approximates the nose-to-lips boundary as a fraction of the detected
/// face-box height). No default — the value must be present in the JSON or deserialization fails
/// loud.
/// </summary>
public sealed class HeadCutterConfig
{
    public required double FaceHeightCutFactor { get; init; }
}
