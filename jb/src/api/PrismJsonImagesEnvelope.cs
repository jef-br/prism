namespace Prism.Api;


/// <summary>
/// Per-image journey groups returned by JSON result retrieval.
/// </summary>
internal sealed record PrismJsonImagesEnvelope {
    public IReadOnlyList<ImageJourneyItem> Ok { get; init; } = [];
    public IReadOnlyList<ImageJourneyItem> Ko { get; init; } = [];
}
