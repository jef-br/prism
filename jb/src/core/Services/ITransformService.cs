namespace Prism.Core;

/// <summary>
/// Pipeline-visible Transform service. Routes each non-KO LAMBDA through its transformation strategy and
/// enriches it in place with a TransformationResult. When transform is disabled, every non-KO image is
/// marked Skipped. Emits the Transformed event.
/// </summary>
public interface ITransformService
{
    /// <summary>Runs the Transformed stage over the enriched LAMBDA collection.</summary>
    Task<TransformResult> TransformAsync(
        MatchingResult matched,
        bool transformEnabled,
        bool headcut,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken);
}
