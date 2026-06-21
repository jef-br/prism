namespace Prism.Core;

/// <summary>
/// Structured result returned by <see cref="Pipeline.RunAsync"/> to the Prism facade.
/// </summary>
internal sealed record PipelineResult(
    string Status,
    string OutputFormat,
    BatchManifest Manifest,
    string? FailureReason,
    IReadOnlyList<string> Warnings,
    byte[]? ZipBytes = null);
