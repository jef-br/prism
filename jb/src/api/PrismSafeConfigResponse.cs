/// <summary>
/// Safe public configuration response.
/// </summary>
internal sealed record PrismSafeConfigResponse
{
    public bool ConfigReady { get; init; }
    public bool SafeConfigurationAvailable { get; init; }
    public IReadOnlyList<string> AcceptedMediaTypes { get; init; } = [];
    public IReadOnlyList<string> OutputFormats { get; init; } = [];
    public PrismVisibleFeatureFlags VisibleFeatureFlags { get; init; } = new(false, false, false, false, false);
    public PrismSafeLimitResponse Limits { get; init; } = new();
    public PrismQueueConfigResponse Queue { get; init; } = new(0, 0);
    public string Notes { get; init; } = string.Empty;
}
