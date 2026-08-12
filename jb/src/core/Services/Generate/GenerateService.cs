namespace Prism.Services.Generate;

/// <summary>
/// In-process Generate implementation. Wraps <see cref="ImageGenerator"/>. Enriches hero LAMBDAs in place
/// and returns the new synthetic records as a distinct output. Emits the Generated stage event.
/// </summary>
public sealed class GenerateService : IGenerateService {
    private readonly bool generationBackendAvailable;

    /// <summary>Creates the service with the validated PRISM configuration; reads Models.Generation.UseIt.</summary>
    public GenerateService(PrismConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(configuration);
        this.generationBackendAvailable = configuration.AiGenerationEnabled;
    }

    /// <inheritdoc/>
    public async Task<GenerateResult> GenerateAsync(
        MatchingResult matched,
        bool generationEnabled,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken) {
        await StageProgress.EmitStarted(progress, matched.Ingest.JobID, PipelineStageNames.Generated, cancellationToken);

        IReadOnlyList<ImageRecord_GENERATED> generatedImages =
            ImageGenerator.Run(matched.LambdaRecords, generationEnabled, this.generationBackendAvailable);

        return new GenerateResult(matched, generatedImages);
    }
}
