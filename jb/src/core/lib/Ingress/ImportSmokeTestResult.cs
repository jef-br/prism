namespace Prism.Lib.Ingress;

/// <summary>
/// Result of one <see cref="ImportSmokeTest"/> run.
/// </summary>
public sealed record ImportSmokeTestResult
{
    /// <summary>Whether the test could run at all (false when fixtures are missing).</summary>
    public bool CanRun { get; init; }

    /// <summary>Why the test could not run (null when CanRun is true).</summary>
    public string? BlockReason { get; init; }

    /// <summary>Import stage result when CanRun is true.</summary>
    public ImportStageResult? Result { get; init; }

    /// <summary>
    /// Creates a blocked result when the fixture paths are unavailable.
    /// </summary>
    public static ImportSmokeTestResult Blocked(string reason)
        => new() { CanRun = false, BlockReason = reason };

    /// <summary>
    /// Creates a completed result after the stage has run.
    /// </summary>
    public static ImportSmokeTestResult Completed(ImportStageResult result)
        => new() { CanRun = true, Result = result };

    /// <summary>
    /// Returns a human-readable summary of the test outcome.
    /// </summary>
    public override string ToString()
    {
        if (!CanRun)
        {
            return $"SMOKE TEST BLOCKED: {BlockReason}";
        }

        ImportStageResult r = Result!;
        return
            $"SMOKE TEST COMPLETED\n" +
            $"  Normalized images:  {r.NormalizedImages.Count}\n" +
            $"  Family records:     {r.FamilyRecords.Count}\n" +
            $"  Image KO records:   {r.ImageKoRecords.Count}\n" +
            $"  Zip KO records:     {r.ZipKoRecords.Count}\n" +
            $"  Excel diagnostics:  {r.ExcelDiagnostics.Count}\n" +
            $"  Job temp folder:    {r.JobTempFolder}";
    }
}
