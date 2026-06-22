namespace Prism.Core;

/// <summary>
/// Single place that emits a stage-start progress event. Each service calls this as it begins a
/// pipeline stage so the SSE stream still reports all eight stage events in immutable order, exactly
/// as the old per-stage <c>Pipeline</c> loop did.
/// </summary>
internal static class StageProgress
{
    /// <summary>
    /// Emits the "stage started" event for <paramref name="stageName"/> when a progress sink is attached.
    /// No-op when <paramref name="progress"/> is null.
    /// </summary>
    internal static async Task EmitStarted(
        Func<PipelineProgressEvent, Task>? progress,
        Guid jobId,
        string stageName,
        CancellationToken cancellationToken)
    {
        if (progress is null) return;

        cancellationToken.ThrowIfCancellationRequested();

        await progress(new PipelineProgressEvent
        {
            JobID       = jobId,
            Stage       = stageName,
            Severity    = "Information",
            SafeMessage = $"Stage {stageName} started.",
            Timestamp   = DateTimeOffset.UtcNow
        });
    }
}
