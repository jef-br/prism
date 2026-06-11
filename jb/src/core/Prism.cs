/// <summary>
/// PRISM facade. It accepts core-facing job requests and delegates real pipeline work to named helpers.
/// </summary>
public sealed class Prism
{
    private static readonly string[] StageNames =
    [
        "Imported",
        "Classified",
        "Matched",
        "Ordered",
        "Renamed",
        "Generated",
        "Transformed",
        "Exported"
    ];

    /// <summary>
    /// Processes a PRISM job through the minimal T-200 adapter.
    /// </summary>
    /// <param name="request">The normalized core-facing job request.</param>
    /// <param name="progress">Progress callback used by API SSE and future workbench direct invocation.</param>
    /// <param name="cancellationToken">Token used only for host shutdown, not user cancellation.</param>
    /// <returns>A structured PRISM job result.</returns>
    public async Task<PrismJobResult> Process(
        PrismJobRequest request,
        Func<PipelineProgressEvent, Task>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        await EmitMinimalSmokeProgress(request, progress, cancellationToken);
        return BuildMinimalSmokeResult(request);
    }

    private static void ValidateRequest(PrismJobRequest request)
    {
        if (request.JobID == Guid.Empty)
        {
            throw new ArgumentException("PrismJobRequest.JobID is required.", nameof(request));
        }

        if (request.PrismProcessingParameters is null)
        {
            throw new ArgumentException("PrismProcessingParameters is required.", nameof(request));
        }

        if (request.ImageRecords.Count == 0)
        {
            throw new ArgumentException("At least one accepted image record is required.", nameof(request));
        }

        if (request.ExcelRecords.Count == 0)
        {
            throw new ArgumentException("At least one accepted Excel record is required.", nameof(request));
        }
    }

    private static async Task EmitMinimalSmokeProgress(
        PrismJobRequest request,
        Func<PipelineProgressEvent, Task>? progress,
        CancellationToken cancellationToken)
    {
        if (progress is null)
        {
            return;
        }

        for (int stageIndex = 0; stageIndex < StageNames.Length; stageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await progress(new PipelineProgressEvent
            {
                JobID = request.JobID,
                Stage = StageNames[stageIndex],
                CompletedCount = stageIndex + 1,
                TotalCount = StageNames.Length,
                Severity = "Information",
                SafeMessage = $"T-200 adapter reached {StageNames[stageIndex]} stage.",
                Timestamp = DateTimeOffset.UtcNow
            });

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }
    }

    private static PrismJobResult BuildMinimalSmokeResult(PrismJobRequest request)
    {
        string outputFormat = request.PrismProcessingParameters?.Format ?? "json";
        bool zipRequested = string.Equals(outputFormat, "zip", StringComparison.OrdinalIgnoreCase);

        string status = zipRequested ? "Failed" : "Completed";
        string? failureReason = zipRequested
            ? "ZIP export is not implemented by the T-200 minimal core adapter."
            : null;

        string[] warnings =
        [
            "T-200 minimal adapter verified API-to-core wiring only; full pipeline behavior is deferred to T-300 and later stage tickets."
        ];

        return new PrismJobResult
        {
            JobID = request.JobID,
            ClientRequestToken = request.ClientRequestToken,
            Status = status,
            OutputFormat = outputFormat,
            FailureReason = failureReason,
            Warnings = warnings,
            Manifest = new BatchManifest
            {
                JobID = request.JobID,
                Summary = new BatchManifestSummary
                {
                    ImageCount = request.ImageRecords.Count,
                    ExcelCount = request.ExcelRecords.Count,
                    ZipCount = request.ZipFileRecords.Count,
                    OkRenamed = 0,
                    KoRecords = zipRequested ? request.ImageRecords.Count : 0
                },
                RouteSummaries = StageNames.Select(stage => $"{stage}: reached by T-200 adapter.").ToArray(),
                Warnings = warnings
            }
        };
    }
}
