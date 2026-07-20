using System.Threading.Channels;

namespace Prism.Api;

/// <summary>
/// Live-only progress subscription.
/// </summary>
internal sealed record PrismProgressSubscription(ChannelReader<PipelineProgressEvent> Events, bool IsTerminal);
