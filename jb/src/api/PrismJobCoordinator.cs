using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Prism.Api;

/// <summary>
/// Single-server in-process job coordinator for T-200 API online behavior.
/// </summary>
internal sealed class PrismJobCoordinator {
    private readonly PrismService prism;
    private readonly PrismApiConfiguration configuration;
    private readonly Channel<PrismApiJob> queue;
    private readonly ConcurrentDictionary<Guid, PrismApiJob> jobs = new();
    private int activeJobCount;
    private int queuedJobCount;

    /// <summary>
    /// Creates the queue and starts fixed background workers.
    /// </summary>
    public PrismJobCoordinator(PrismService prism, PrismApiConfiguration configuration) {
        this.prism = prism;
        this.configuration = configuration;
        this.MaxQueuedJobs = configuration.MaxQueuedJobs;
        this.MaxConcurrentJobs = configuration.MaxConcurrentJobs;
        this.queue = Channel.CreateBounded<PrismApiJob>(new BoundedChannelOptions(this.MaxQueuedJobs) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        for (int workerIndex = 0; workerIndex < this.MaxConcurrentJobs; workerIndex++) {
            _ = Task.Run(this.ProcessJobs);
        }
    }

    public int MaxQueuedJobs { get; }
    public int MaxConcurrentJobs { get; }
    public int ActiveJobCount => this.activeJobCount;
    public int QueuedJobCount => this.queuedJobCount;
    public bool CanAcceptJobs => this.queuedJobCount < this.MaxQueuedJobs;

    /// <summary>
    /// Tries to enqueue one accepted PRISM job.
    /// </summary>
    public bool TryEnqueue(PrismJobRequest request, PrismJobUrls urls, out PrismJobStartEnvelope? envelope) {
        this.RemoveExpiredJobs();

        PrismApiJob job = new(request, urls);
        if (!this.queue.Writer.TryWrite(job)) {
            envelope = null;
            return false;
        }

        this.jobs[request.JobID] = job;
        Interlocked.Increment(ref this.queuedJobCount);

        envelope = new PrismJobStartEnvelope {
            JobID = request.JobID,
            ClientRequestToken = request.ClientRequestToken,
            ProgressUrl = urls.ProgressUrl,
            ResultUrl = urls.ResultUrl,
            Status = "Queued"
        };

        return true;
    }

    /// <summary>
    /// Subscribes to live progress for a non-terminal job.
    /// </summary>
    public PrismProgressSubscription? Subscribe(Guid jobID) {
        if (!this.jobs.TryGetValue(jobID, out PrismApiJob? job)) {
            return null;
        }

        if (job.IsTerminal) {
            return new PrismProgressSubscription(Channel.CreateUnbounded<PipelineProgressEvent>().Reader, true);
        }

        Channel<PipelineProgressEvent> subscriber = Channel.CreateUnbounded<PipelineProgressEvent>();
        job.AddSubscriber(subscriber);
        return new PrismProgressSubscription(subscriber.Reader, false);
    }

    /// <summary>
    /// Gets a stored result or in-progress status.
    /// </summary>
    public PrismStoredJobResult? GetResult(Guid jobID) {
        this.RemoveExpiredJobs();

        if (!this.jobs.TryGetValue(jobID, out PrismApiJob? job)) {
            return null;
        }

        return new PrismStoredJobResult(job.Status, job.Result, job.IsTerminal);
    }

    /// <summary>
    /// Lists all known jobs as summaries, newest first.
    /// </summary>
    public IReadOnlyList<PrismJobSummary> ListJobs() {
        this.RemoveExpiredJobs();

        return this.jobs.Values
            .OrderByDescending(job => job.CreatedAt)
            .Select(job => new PrismJobSummary(
                job.Request.JobID,
                job.Status,
                job.IsTerminal,
                job.CreatedAt,
                job.CompletedAt,
                job.Urls.ProgressUrl,
                job.Urls.ResultUrl,
                job.Result?.OkImages.Count ?? 0,
                job.Result?.KoImages.Count ?? 0))
            .ToList();
    }

    private async Task ProcessJobs() {
        await foreach (PrismApiJob job in this.queue.Reader.ReadAllAsync()) {
            Interlocked.Decrement(ref this.queuedJobCount);
            Interlocked.Increment(ref this.activeJobCount);

            try {
                job.MarkRunning();
                await job.Publish(CreateJobStatusEvent(job.Request.JobID, "Running", "PRISM job is running."));

                PrismJobResult result = await this.prism.Process(job.Request, job.Publish);
                job.MarkCompleted(result);
                await job.Publish(CreateJobStatusEvent(job.Request.JobID, result.Status, "PRISM job reached a terminal state."));
            }
            catch (Exception exception) {
                PrismJobResult failedResult = BuildFailedResult(job.Request, exception);
                job.MarkCompleted(failedResult);
                await job.Publish(CreateJobStatusEvent(job.Request.JobID, "Failed", failedResult.FailureReason ?? "PRISM job failed."));
            }
            finally {
                job.CompleteSubscribers();
                Interlocked.Decrement(ref this.activeJobCount);
            }
        }
    }

    private void RemoveExpiredJobs() {
        DateTimeOffset expirationCutoff = DateTimeOffset.UtcNow.AddHours(-this.configuration.JobRetentionPeriodInHours);
        foreach (KeyValuePair<Guid, PrismApiJob> job in this.jobs) {
            if (job.Value.IsTerminal && job.Value.CompletedAt < expirationCutoff) {
                this.jobs.TryRemove(job.Key, out _);
            }
        }
    }

    private static PipelineProgressEvent CreateJobStatusEvent(Guid jobID, string status, string message) {
        return new PipelineProgressEvent {
            JobID = jobID,
            Stage = status,
            Severity = string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ? "Error" : "Information",
            SafeMessage = message,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static PrismJobResult BuildFailedResult(PrismJobRequest request, Exception exception) {
        return new PrismJobResult {
            JobID = request.JobID,
            ClientRequestToken = request.ClientRequestToken,
            Status = "Failed",
            OutputFormat = request.PrismProcessingParameters?.Format ?? "json",
            FailureReason = exception.Message,
            Manifest = new BatchManifest {
                JobID = request.JobID,
                Warnings = [exception.Message]
            }
        };
    }
}
