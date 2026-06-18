
/// <summary>
/// Per-image journey groups returned by JSON result retrieval.
/// </summary>
internal sealed record PrismJsonImagesEnvelope
{
    public IReadOnlyList<ManifestImageRow> Ok { get; init; } = [];
    public IReadOnlyList<ManifestImageRow> Ko { get; init; } = [];
}