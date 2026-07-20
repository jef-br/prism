namespace Prism.Api;

/// <summary>Result of lite ingress validation.</summary>
internal sealed record PrismMatchLiteIngressResult
{
    public IReadOnlyList<ImageRecord_INPUT>? Images { get; init; }
    public IReadOnlyList<InputExcelFileRecord>? ExcelFiles { get; init; }
    public string? JobTempDir { get; init; }
    public PrismPreCoreErrorResponse? Error { get; init; }

    public static PrismMatchLiteIngressResult FromData(
        IReadOnlyList<ImageRecord_INPUT> images,
        IReadOnlyList<InputExcelFileRecord> excelFiles,
        string jobTempDir)
        => new() { Images = images, ExcelFiles = excelFiles, JobTempDir = jobTempDir };

    public static PrismMatchLiteIngressResult FromError(PrismPreCoreErrorResponse error)
        => new() { Error = error };

    /// <summary>Deletes the temp folder that holds spilled Excel files.</summary>
    public void CleanUp()
    {
        if (JobTempDir is null) return;
        try { if (Directory.Exists(JobTempDir)) Directory.Delete(JobTempDir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
