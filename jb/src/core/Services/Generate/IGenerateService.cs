namespace Prism.Core;

/// <summary>
/// Pipeline-visible Generate service. For families below the minimum image count it enriches the hero
/// LAMBDA in place (GenerationRouteState + GeneratedChildren) and creates new ImageRecord_GENERATED
/// records. Returns both outputs explicitly so neither enrichment is hidden. Emits the Generated event.
/// </summary>
public interface IGenerateService
{
    /// <summary>Runs the Generated stage; returns the enriched LAMBDAs and the new synthetic records.</summary>
    Task<GenerateResult> GenerateAsync(
        MatchingResult matched,
        bool generationEnabled,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken);
}
