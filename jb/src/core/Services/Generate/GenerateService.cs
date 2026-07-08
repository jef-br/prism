namespace Prism.Services.Generate;

/// <summary>
/// In-process Generate implementation. Wraps <see cref="ImageGenerator"/>. Enriches hero LAMBDAs in place
/// and returns the new synthetic records as a distinct output. Emits the Generated stage event.
/// </summary>
public sealed class GenerateService : IGenerateService
{
    /// <inheritdoc/>
    public async Task<GenerateResult> GenerateAsync(
        MatchingResult matched,
        bool generationEnabled,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        await StageProgress.EmitStarted(progress, matched.Ingest.JobID, PipelineStageNames.Generated, cancellationToken);

        IReadOnlyList<ImageRecord_GENERATED> generatedImages =
            ImageGenerator.Run(matched.LambdaRecords, generationEnabled);

        return new GenerateResult(matched, generatedImages);
    }
}
