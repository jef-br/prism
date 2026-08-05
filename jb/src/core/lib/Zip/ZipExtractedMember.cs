namespace Prism.Lib.Zip;

/// <summary>
/// Describes a healthy processable member extracted from a zip archive.
/// </summary>
/// <param name="ArchivePath">Safe path or display name of the owning archive.</param>
/// <param name="MemberPath">Member path inside the archive, in the archive's own separator convention.</param>
/// <param name="OriginalFileName">
/// The member path relative to its owning archive root, separators normalized to forward slash.
/// Carries folder structure (not just the bare leaf name) so it can flow into
/// <c>ImageRecord_INPUT.InitialFullName</c> and support folder-based matching downstream.
/// </param>
/// <param name="MediaKind">Triaged processable media kind.</param>
/// <param name="ExtractedFilePath">Local extracted file path in the job temp folder.</param>
/// <param name="ExpandedByteLength">Expanded member size in bytes.</param>
/// <param name="ZipDepth">Nested zip depth where the member was found.</param>
public sealed record ZipExtractedMember(
    string ArchivePath,
    string MemberPath,
    string OriginalFileName,
    ZipMemberMediaKind MediaKind,
    string ExtractedFilePath,
    long ExpandedByteLength,
    int ZipDepth);
