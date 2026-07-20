using System.Threading.Channels;

namespace Prism.Api;

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
