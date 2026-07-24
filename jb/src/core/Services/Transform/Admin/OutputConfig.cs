using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// Shared JPEG output settings for the transform strategies that re-encode pixels directly
/// (<see cref="Tx_CropSquare"/>, <see cref="Tx_ProblemImageProcessor"/>), bound from the "Output"
/// section of transform_Config.json. No defaults — the value must be present in the JSON or
/// deserialization fails loud.
/// </summary>
public sealed class OutputConfig : IValidatableConfig
{
    // JPEG quality's own upper bound — not a tunable, the format's valid range ends here.
    private const int MaxJpegQuality = 100;

    public required int JpegOutputQuality { get; init; }

    public void Validate()
    {
        if (this.JpegOutputQuality is <= 0 or > MaxJpegQuality)
            throw new PrismConfigurationException($"Output.JpegOutputQuality must be in (0,{MaxJpegQuality}]");
    }
}
