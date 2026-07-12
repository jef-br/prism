namespace Prism.Services.Transform;

/// <summary>
/// CLAHE parameters for Tx_LowContrastEnhancement, bound from the "LowContrastEnhancement" section
/// of transform_Config.json. No defaults — every value must be present in the JSON or
/// deserialization fails loud.
/// </summary>
public sealed class LowContrastEnhancementConfig
{
    public required double ClipLimit { get; init; }
    public required int TileSize { get; init; }
}
