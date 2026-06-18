/// <summary>
/// Feature flags safe to expose to callers.
/// </summary>
internal sealed record PrismVisibleFeatureFlags(
    bool Rename,
    bool Transform,
    bool Generation,
    bool ProgressSse,
    bool MinimalCoreAdapter);