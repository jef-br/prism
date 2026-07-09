using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Prism.Api;

/// <summary>
/// Single-server in-process job coordinator for T-200 API online behavior.
/// </summary>
internal sealed class PrismJobCoordinator
{
    private readonly PrismService prism;
    private readonly PrismApiConfiguration configuration;
    private readonly Channel<PrismApiJob> queue;
    private readonly ConcurrentDictionary<Guid, PrismApiJob> jobs = new();
    private int activeJobCount;
    private int queuedJobCount;

    /// <summary>
    /// Creates the queue and starts fixed background workers.
    /// </summary>
    public PrismJobCoordinator(PrismService prism, PrismApiConfiguration configuration)
    {
        this.prism = prism;
        this.configuration = configuration;
        MaxQueuedJobs = configuration.MaxQueuedJobs;
        MaxConcurrentJobs = configuration.MaxConcurrentJobs;
        queue = Channel.CreateBounded<PrismApiJob>(new BoundedChannelOptions(MaxQueuedJobs)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        for (int workerIndex = 0; workerIndex < MaxConcurrentJobs; workerIndex++)
        {
            _ = Task.Run(ProcessJobs);
        }
    }

    public int MaxQueuedJobs { get; }
    public int MaxConcurrentJobs { get; }
    public int ActiveJobCount => activeJobCount;
    public int QueuedJobCount => queuedJobCount;
    public bool CanAcceptJobs => queuedJobCount < MaxQueuedJobs;

    /// <summary>
    /// Tries to enqueue one accepted PRISM job.
    /// </summary>
    public bool TryEnqueue(PrismJobRequest request, PrismJobUrls urls, out PrismJobStartEnvelope? envelope)
    {
        RemoveExpiredJobs();

        PrismApiJob job = new(request, urls);
        if (!queue.Writer.TryWrite(job))
        {
            envelope = null;
            return false;
        }

        jobs[request.JobID] = job;
        Interlocked.Increment(ref queuedJobCount);

        envelope = new PrismJobStartEnvelope
        {
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
    public PrismProgressSubscription? Subscribe(Guid jobID)
    {
        if (!jobs.TryGetValue(jobID, out PrismApiJob? job))
        {
            return null;
        }

        if (job.IsTerminal)
        {
            return new PrismProgressSubscription(Channel.CreateUnbounded<PipelineProgressEvent>().Reader, true);
        }

        Channel<PipelineProgressEvent> subscriber = Channel.CreateUnbounded<PipelineProgressEvent>();
        job.AddSubscriber(subscriber);
        return new PrismProgressSubscription(subscriber.Reader, false);
    }

    /// <summary>
    /// Gets a stored result or in-progress status.
    /// </summary>
    public PrismStoredJobResult? GetResult(Guid jobID)
    {
        RemoveExpiredJobs();

        if (!jobs.TryGetValue(jobID, out PrismApiJob? job))
        {
            return null;
        }

        return new PrismStoredJobResult(job.Status, job.Result, job.IsTerminal);
    }

    /// <summary>
    /// Lists all known jobs as summaries, newest first.
    /// </summary>
    public IReadOnlyList<PrismJobSummary> ListJobs()
    {
        RemoveExpiredJobs();

        return jobs.Values
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

    private async Task ProcessJobs()
    {
        await foreach (PrismApiJob job in queue.Reader.ReadAllAsync())
        {
            Interlocked.Decrement(ref queuedJobCount);
            Interlocked.Increment(ref activeJobCount);

            try
            {
                job.MarkRunning();
                await job.Publish(CreateJobStatusEvent(job.Request.JobID, "Running", "PRISM job is running."));

                PrismJobResult result = await prism.Process(job.Request, job.Publish);
                job.MarkCompleted(result);
                await job.Publish(CreateJobStatusEvent(job.Request.JobID, result.Status, "PRISM job reached a terminal state."));
            }
            catch (Exception exception)
            {
                PrismJobResult failedResult = BuildFailedResult(job.Request, exception);
                job.MarkCompleted(failedResult);
                await job.Publish(CreateJobStatusEvent(job.Request.JobID, "Failed", failedResult.FailureReason ?? "PRISM job failed."));
            }
            finally
            {
                job.CompleteSubscribers();
                Interlocked.Decrement(ref activeJobCount);
            }
        }
    }

    private void RemoveExpiredJobs()
    {
        DateTimeOffset expirationCutoff = DateTimeOffset.UtcNow.AddHours(-configuration.JobRetentionPeriodInHours);
        foreach (KeyValuePair<Guid, PrismApiJob> job in jobs)
        {
            if (job.Value.IsTerminal && job.Value.CompletedAt < expirationCutoff)
            {
                jobs.TryRemove(job.Key, out _);
            }
        }
    }

    private static PipelineProgressEvent CreateJobStatusEvent(Guid jobID, string status, string message)
    {
        return new PipelineProgressEvent
        {
            JobID = jobID,
            Stage = status,
            Severity = string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ? "Error" : "Information",
            SafeMessage = message,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static PrismJobResult BuildFailedResult(PrismJobRequest request, Exception exception)
    {
        return new PrismJobResult
        {
            JobID = request.JobID,
            ClientRequestToken = request.ClientRequestToken,
            Status = "Failed",
            OutputFormat = request.PrismProcessingParameters?.Format ?? "json",
            FailureReason = exception.Message,
            Manifest = new BatchManifest
            {
                JobID = request.JobID,
                Warnings = [exception.Message]
            }
        };
    }
}

/// <summary>
/// Internal API job state.
/// </summary>
internal sealed class PrismApiJob
{
    private readonly List<Channel<PipelineProgressEvent>> subscribers = [];
    private readonly object subscriberLock = new();

    public PrismApiJob(PrismJobRequest request, PrismJobUrls urls)
    {
        Request = request;
        Urls = urls;
    }

    public PrismJobRequest Request { get; }
    public PrismJobUrls Urls { get; }
    public string Status { get; private set; } = "Queued";
    public PrismJobResult? Result { get; private set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }
    public bool IsTerminal => CompletedAt.HasValue;

    public void MarkRunning()
    {
        Status = "Running";
    }

    public void MarkCompleted(PrismJobResult result)
    {
        Result = result;
        Status = result.Status;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void AddSubscriber(Channel<PipelineProgressEvent> subscriber)
    {
        lock (subscriberLock)
        {
            if (IsTerminal)
            {
                subscriber.Writer.TryComplete();
                return;
            }

            subscribers.Add(subscriber);
        }
    }

    public async Task Publish(PipelineProgressEvent progressEvent)
    {
        Channel<PipelineProgressEvent>[] currentSubscribers;
        lock (subscriberLock)
        {
            currentSubscribers = subscribers.ToArray();
        }

        foreach (Channel<PipelineProgressEvent> subscriber in currentSubscribers)
        {
            await subscriber.Writer.WriteAsync(progressEvent);
        }
    }

    public void CompleteSubscribers()
    {
        lock (subscriberLock)
        {
            foreach (Channel<PipelineProgressEvent> subscriber in subscribers)
            {
                subscriber.Writer.TryComplete();
            }

            subscribers.Clear();
        }
    }
}

/// <summary>
/// Live-only progress subscription.
/// </summary>
internal sealed record PrismProgressSubscription(ChannelReader<PipelineProgressEvent> Events, bool IsTerminal);

/// <summary>
/// Stored result projection for result endpoint callers.
/// </summary>
internal sealed record PrismStoredJobResult(string Status, PrismJobResult? Result, bool IsTerminal);

/// <summary>
/// Compact per-job summary for the job-list endpoint.
/// </summary>
internal sealed record PrismJobSummary(
    Guid JobID,
    string Status,
    bool IsTerminal,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string ProgressUrl,
    string ResultUrl,
    int OkImages,
    int KoImages);
