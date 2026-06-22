namespace Prism.Core;

/// <summary>
/// The Export step's output: the canonical manifest plus the ZIP bytes when ZIP format was requested.
/// Named for what it carries so the orchestrator line reads <c>var manifestAndZip = await Export(...)</c>.
/// Replaces the old <c>ExportStageResult</c>.
/// </summary>
public sealed record ExportArtifacts
{
    /// <summary>Fully-populated batch manifest, reused for both JSON and ZIP responses.</summary>
    public required BatchManifest Manifest { get; init; }

    /// <summary>ZIP archive bytes when output format is "zip"; null for JSON output.</summary>
    public byte[]? ZipBytes { get; init; }
}
