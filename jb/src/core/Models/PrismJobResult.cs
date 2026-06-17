/// <summary>
/// Client-facing result returned by PRISM for one accepted job.
/// </summary>
public sealed record PrismJobResult
{
    /// <summary>
    /// PRISM-owned internal job identifier.
    /// </summary>
    public Guid JobID { get; init; }

    /// <summary>
    /// Optional caller token echoed unchanged for correlation.
    /// </summary>
    public string? ClientRequestToken { get; init; }

    /// <summary>
    /// Terminal job status.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Canonical batch manifest projected into zip and JSON responses.
    /// </summary>
    public BatchManifest Manifest { get; init; } = new();

    /// <summary>
    /// Safe warnings emitted by the minimal adapter or future pipeline.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Safe failure reason when the job cannot produce the requested output.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Requested output format.
    /// </summary>
    public string OutputFormat { get; init; } = "json";

    /// <summary>
    /// ZIP archive bytes when <see cref="OutputFormat"/> is "zip" and the job completed successfully.
    /// Null for JSON output or failed jobs.
    /// </summary>
    public byte[]? ZipBytes { get; init; }

    /// <summary>
    /// OK image rows for JSON envelope consumption (<c>images.ok[]</c>).
    /// Sourced from <see cref="BatchManifest.ImageRows"/> filtered to Status == "Ok".
    /// </summary>
    public IReadOnlyList<ManifestImageRow> OkImages { get; init; } = [];

    /// <summary>
    /// KO image rows for JSON envelope consumption (<c>images.ko[]</c>).
    /// Sourced from <see cref="BatchManifest.ImageRows"/> filtered to Status == "Ko".
    /// </summary>
    public IReadOnlyList<ManifestImageRow> KoImages { get; init; } = [];
}
