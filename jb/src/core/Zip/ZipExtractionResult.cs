using System.Collections.Generic;

/// <summary>
/// Result returned by the zip foundation module after extracting processable members.
/// </summary>
/// <param name="ExtractedMembers">Healthy image, document, and Excel members extracted for later import.</param>
/// <param name="KoRecords">Manifest-facing KO records created during zip extraction.</param>
public sealed record ZipExtractionResult(
    IReadOnlyList<ZipExtractedMember> ExtractedMembers,
    IReadOnlyList<ZipMemberKoRecord> KoRecords);
