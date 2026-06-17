/// <summary>
/// Result produced by the Exported stage and held on <see cref="PipelineContext.ExportResult"/>.
/// <see cref="Pipeline"/> reads <see cref="FinalManifest"/> and <see cref="ZipBytes"/> when building the success result.
/// </summary>
internal sealed record ExportStageResult
{
    /// <summary>ZIP archive bytes when output format is "zip". Null for JSON output.</summary>
    internal byte[]? ZipBytes { get; init; }

    /// <summary>Fully-populated manifest built by the Exporter, reused by Pipeline.BuildSuccessResult.</summary>
    internal BatchManifest? FinalManifest { get; init; }
}
