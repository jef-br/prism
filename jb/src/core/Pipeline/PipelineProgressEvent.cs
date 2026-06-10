/*
Represents one progress event emitted by a Prism pipeline stage.

*/

/// <summary>
/// Progress event emitted by a PRISM pipeline stage.
/// </summary>
public sealed record PipelineProgressEvent
{
    /// <summary>
    /// Route stage that emitted this progress event.
    /// </summary>
    public string Stage { get; init; } = string.Empty;
}
