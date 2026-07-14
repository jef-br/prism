using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// CLAHE parameters for Tx_LowContrastEnhancement, bound from the "LowContrastEnhancement" section
/// of transform_Config.json. No defaults — every value must be present in the JSON or
/// deserialization fails loud.
/// </summary>
public sealed class LowContrastEnhancementConfig : IValidatableConfig
{
    public required double ClipLimit { get; init; }
    public required int TileSize { get; init; }

    public void Validate()
    {
        List<string> problems = [];

        if (ClipLimit <= 0.0) problems.Add("LowContrastEnhancement.ClipLimit must be > 0");
        if (TileSize < 1) problems.Add("LowContrastEnhancement.TileSize must be >= 1");

        if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
    }
}
