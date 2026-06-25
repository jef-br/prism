namespace Prism.Core;

/// <summary>
/// Per-image journey entry serialized into images.ok[] and images.ko[] in the JSON result envelope.
/// Replaces the flat ManifestImageRow projection for the images envelope.
/// </summary>
public sealed record ImageJourneyItem
{
    public string SourceReference { get; init; } = string.Empty;
    public ImageLambdaJourney Lambda { get; init; } = new();
    public ImageRecord_OUTPUT? Output { get; init; }
}
