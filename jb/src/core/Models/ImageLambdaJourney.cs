namespace Prism.Contracts;

/// <summary>
/// Bounded per-image stage journey for web visualization.
/// Ordered by pipeline stage; each step carries a name, status, and optional safe message.
/// </summary>
public sealed record ImageLambdaJourney {
    public IReadOnlyList<ImageStageStep> Stages { get; init; } = [];
}
