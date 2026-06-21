namespace Prism.Core;

/// <summary>
/// Configurable limits used while extracting processable zip members.
/// </summary>
/// <param name="MaxNestedZipDepth">Maximum number of nested zip levels to inspect.</param>
/// <param name="MaxImageMemberBytes">Maximum expanded bytes for image and document members.</param>
/// <param name="MaxExcelMemberBytes">Maximum expanded bytes for Excel members.</param>
/// <param name="MaxZipArchiveBytes">Maximum compressed or expanded bytes for zip archive members.</param>
/// <param name="HeaderProbeBytes">Number of bytes used to triage a member by content signature.</param>
public sealed record ZipExtractionPolicy(
    int MaxNestedZipDepth,
    long MaxImageMemberBytes,
    long MaxExcelMemberBytes,
    long MaxZipArchiveBytes,
    int HeaderProbeBytes)
{
    /// <summary>
    /// Builds a policy from the current PRISM config defaults.
    /// </summary>
    /// <returns>A zip extraction policy matching the documented default limits.</returns>
    public static ZipExtractionPolicy CreateDefault()
    {
        return new ZipExtractionPolicy(
            MaxNestedZipDepth: 5,
            MaxImageMemberBytes: 26_214_400,
            MaxExcelMemberBytes: 1_048_576,
            MaxZipArchiveBytes: 2_147_483_648,
            HeaderProbeBytes: 8192);
    }
}
