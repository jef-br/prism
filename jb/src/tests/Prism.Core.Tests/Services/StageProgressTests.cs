using Xunit;

namespace PrismCoreTests.Services;

/// <summary>
/// Direct unit tests for <see cref="StageProgress"/>: no-op behavior with no progress sink,
/// and the count/severity contract of EmitStarted and EmitCompleted.
/// </summary>
public sealed class StageProgressTests {
    [Fact]
    public async Task EmitStarted_NoProgressSink_DoesNotThrow() {
        await StageProgress.EmitStarted(null, Guid.NewGuid(), "Imported", CancellationToken.None);
    }

    [Fact]
    public async Task EmitStarted_PublishesInformationSeverityWithNoCounts() {
        PipelineProgressEvent? captured = null;
        Guid jobId = Guid.NewGuid();

        await StageProgress.EmitStarted(e => { captured = e; return Task.CompletedTask; }, jobId, "Imported", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(jobId, captured!.JobID);
        Assert.Equal("Imported", captured.Stage);
        Assert.Equal("Information", captured.Severity);
        Assert.Null(captured.CompletedCount);
        Assert.Null(captured.TotalCount);
    }

    [Fact]
    public async Task EmitCompleted_NoProgressSink_DoesNotThrow() {
        await StageProgress.EmitCompleted(null, Guid.NewGuid(), "Imported", completedCount: 3, koCount: 0, CancellationToken.None);
    }

    [Fact]
    public async Task EmitCompleted_NoKoRecords_ReportsInformationSeverity() {
        PipelineProgressEvent? captured = null;

        await StageProgress.EmitCompleted(e => { captured = e; return Task.CompletedTask; }, Guid.NewGuid(), "Imported", completedCount: 5, koCount: 0, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Information", captured!.Severity);
        Assert.Equal(5, captured.CompletedCount);
        Assert.Equal(5, captured.TotalCount);
    }

    [Fact]
    public async Task EmitCompleted_WithKoRecords_ReportsWarningSeverityAndCombinedTotal() {
        PipelineProgressEvent? captured = null;

        await StageProgress.EmitCompleted(e => { captured = e; return Task.CompletedTask; }, Guid.NewGuid(), "Exported", completedCount: 4, koCount: 2, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Warning", captured!.Severity);
        Assert.Equal(4, captured.CompletedCount);
        Assert.Equal(6, captured.TotalCount);
    }
}
