using System.Collections.Generic;

namespace Prism.Lib.Zip;

/// <summary>
/// Manifest-facing KO data for a processable zip member that could not be extracted.
/// </summary>
/// <param name="ArchivePath">Safe path or display name of the owning archive.</param>
/// <param name="MemberPath">Member path inside the archive when available.</param>
/// <param name="OriginalFileName">Original member filename when available.</param>
/// <param name="SourceStage">Owning source stage for the KO reason.</param>
/// <param name="ReasonCode">Stable KO reason code.</param>
/// <param name="KoGroup">Manifest KO group name.</param>
/// <param name="SafeMessage">Human-readable safe message.</param>
/// <param name="ExpandedByteLength">Expanded member size when available.</param>
/// <param name="LimitByteLength">Configured byte limit when relevant.</param>
/// <param name="SafeDetails">Bounded, safe details for manifest projection.</param>
public sealed record ZipMemberKoRecord(
    string ArchivePath,
    string? MemberPath,
    string? OriginalFileName,
    string SourceStage,
    string ReasonCode,
    string KoGroup,
    string SafeMessage,
    long? ExpandedByteLength,
    long? LimitByteLength,
    IReadOnlyDictionary<string, string> SafeDetails) {
    /// <summary>
    /// Source stage used for zip extraction KO records.
    /// </summary>
    public const string ZipExtractSourceStage = "zip-extract";

    /// <summary>
    /// Reason code for corrupt or unextractable processable zip members.
    /// </summary>
    public const string CorruptZipMemberReason = "corrupt-zip-member";

    /// <summary>
    /// Reason code for encrypted archives or encrypted entries.
    /// </summary>
    public const string PasswordProtectedReason = "password-protected";

    /// <summary>
    /// Reason code for processable members that exceed configured extraction limits.
    /// </summary>
    public const string OversizedZipMemberReason = "oversized-zip-member";

    /// <summary>
    /// Reason code for processable members with unsafe or malformed zip metadata.
    /// </summary>
    public const string MalformedZipMemberReason = "malformed-zip-member";

    /// <summary>
    /// Manifest KO group for corrupt images and corrupt zip/image members.
    /// </summary>
    public const string CorruptImagesKoGroup = "corrupt images";

    /// <summary>
    /// Manifest KO group for encrypted zip archives and encrypted entries.
    /// </summary>
    public const string PasswordProtectedZipKoGroup = "password protected zip";

    /// <summary>
    /// Manifest KO group for oversized processable zip members.
    /// </summary>
    public const string OversizedZipMembersKoGroup = "oversized zip members";
}
