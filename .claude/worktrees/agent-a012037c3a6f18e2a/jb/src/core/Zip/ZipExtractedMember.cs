/// <summary>
/// Describes a healthy processable member extracted from a zip archive.
/// </summary>
/// <param name="ArchivePath">Safe path or display name of the owning archive.</param>
/// <param name="MemberPath">Member path inside the archive.</param>
/// <param name="OriginalFileName">Original filename from the member path.</param>
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
