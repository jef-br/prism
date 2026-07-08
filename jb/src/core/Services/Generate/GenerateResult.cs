namespace Prism.Services.Generate;

/// <summary>
/// What the Generate service hands forward, made up of its two distinct outputs so neither is hidden:
/// <list type="bullet">
/// <item><see cref="MatchedWithGenerations"/> — the same LAMBDA collection, now enriched in place with
/// each hero image's <c>GenerationRouteState</c> and <c>GeneratedChildren</c>.</item>
/// <item><see cref="GeneratedImages"/> — the brand-new synthetic <see cref="ImageRecord_GENERATED"/> records.</item>
/// </list>
/// Positional members so the orchestrator can read it as
/// <c>var (matchedWithGenerations, generatedImages) = ...</c>.
/// </summary>
public sealed record GenerateResult(
    MatchingResult MatchedWithGenerations,
    IReadOnlyList<ImageRecord_GENERATED> GeneratedImages);
