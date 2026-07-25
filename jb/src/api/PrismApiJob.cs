using System.Threading.Channels;

namespace Prism.Api;

/// <summary>
/// Internal API job state.
/// </summary>
internal sealed class PrismApiJob {
    private readonly List<Channel<PipelineProgressEvent>> subscribers = [];
    private readonly object subscriberLock = new();

    public PrismApiJob(PrismJobRequest request, PrismJobUrls urls) {
        this.Request = request;
        this.Urls = urls;
    }

    public PrismJobRequest Request { get; }
    public PrismJobUrls Urls { get; }
    public string Status { get; private set; } = "Queued";
    public PrismJobResult? Result { get; private set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }
    public bool IsTerminal => this.CompletedAt.HasValue;

    public void MarkRunning() {
        this.Status = "Running";
    }

    public void MarkCompleted(PrismJobResult result) {
        this.Result = result;
        this.Status = result.Status;
        this.CompletedAt = DateTimeOffset.UtcNow;
    }

    public void AddSubscriber(Channel<PipelineProgressEvent> subscriber) {
        lock (this.subscriberLock) {
            if (this.IsTerminal) {
                subscriber.Writer.TryComplete();
                return;
            }

            this.subscribers.Add(subscriber);
        }
    }

    public async Task Publish(PipelineProgressEvent progressEvent) {
        Channel<PipelineProgressEvent>[] currentSubscribers;
        lock (this.subscriberLock) {
            currentSubscribers = this.subscribers.ToArray();
        }

        foreach (Channel<PipelineProgressEvent> subscriber in currentSubscribers) {
            await subscriber.Writer.WriteAsync(progressEvent);
        }
    }

    public void CompleteSubscribers() {
        lock (this.subscriberLock) {
            foreach (Channel<PipelineProgressEvent> subscriber in this.subscribers) {
                subscriber.Writer.TryComplete();
            }

            this.subscribers.Clear();
        }
    }
}
