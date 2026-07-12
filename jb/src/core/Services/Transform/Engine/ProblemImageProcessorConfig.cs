namespace Prism.Services.Transform;

/// <summary>
/// Size thresholds for Tx_ProblemImageProcessor, bound from the "ProblemImageProcessor" section of
/// transform_Config.json. No defaults — every value must be present in the JSON or deserialization
/// fails loud.
/// </summary>
public sealed class ProblemImageProcessorConfig
{
    public required int MinInputPx { get; init; }
    public required int MinOutputPx { get; init; }
    public required double MaxUpscale { get; init; }
}
