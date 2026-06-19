/// <summary>
/// One feature entry from <c>ImageNGP.json</c>: the canonical id, its datatype, and —
/// for <c>enum</c>/<c>boolean</c> features — the closed set of allowed values.
/// For <c>integer</c>/<c>float</c>/<c>string</c> features <see cref="Values"/> is empty
/// and validation is by datatype only.
/// </summary>
public sealed record FeatureDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Datatype { get; init; } = string.Empty;
    public IReadOnlyList<string> Values { get; init; } = [];
}
