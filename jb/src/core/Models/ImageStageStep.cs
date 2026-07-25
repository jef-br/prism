namespace Prism.Contracts;

/// <summary>
/// One pipeline stage's contribution to an image's journey.
/// Serialized into the bounded lambda journey returned in the JSON result.
/// </summary>
public sealed record ImageStageStep {
    public string StageName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? SafeMessage { get; init; }
}
