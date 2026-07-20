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

    /// <summary>
    /// Emits the "stage completed" event for <paramref name="stageName"/> with accepted/KO counts.
    /// No-op when <paramref name="progress"/> is null. Severity is "Warning" when <paramref name="koCount"/>
    /// is greater than zero so the workbench can distinguish a clean stage from a blocked one.
    /// </summary>
    internal static async Task EmitCompleted(
        Func<PipelineProgressEvent, Task>? progress,
        Guid jobId,
        string stageName,
        int completedCount,
        int koCount,
        CancellationToken cancellationToken)
    {
        if (progress is null) return;

        cancellationToken.ThrowIfCancellationRequested();

        int totalCount = completedCount + koCount;
        string severity = koCount > 0 ? "Warning" : "Information";

        await progress(new PipelineProgressEvent
        {
            JobID          = jobId,
            Stage          = stageName,
            CompletedCount = completedCount,
            TotalCount     = totalCount,
            Severity       = severity,
            SafeMessage    = $"Stage {stageName} completed. {completedCount}/{totalCount} accepted, {koCount} KO.",
            Timestamp      = DateTimeOffset.UtcNow
        });
    }
}
