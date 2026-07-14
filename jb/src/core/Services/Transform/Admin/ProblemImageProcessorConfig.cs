using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// Size thresholds for Tx_ProblemImageProcessor, bound from the "ProblemImageProcessor" section of
/// transform_Config.json. No defaults — every value must be present in the JSON or deserialization
/// fails loud.
/// </summary>
public sealed class ProblemImageProcessorConfig : IValidatableConfig
{
    public required int MinInputPx { get; init; }
    public required int MinOutputPx { get; init; }
    public required double MaxUpscale { get; init; }

    public void Validate()
    {
        List<string> problems = [];

        if (MinInputPx <= 0) problems.Add("ProblemImageProcessor.MinInputPx must be > 0");
        if (MinOutputPx <= 0) problems.Add("ProblemImageProcessor.MinOutputPx must be > 0");
        if (MinOutputPx < MinInputPx) problems.Add("ProblemImageProcessor.MinOutputPx must be >= MinInputPx");
        if (MaxUpscale <= 1.0) problems.Add("ProblemImageProcessor.MaxUpscale must be > 1.0");

        if (problems.Count > 0) throw new InvalidOperationException(string.Join("; ", problems));
    }
}
