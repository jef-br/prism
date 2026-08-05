using System.IO.Compression;

namespace Prism.Lib.Zip;

/// <summary>
/// Extracts processable PRISM members from zip archives.
/// </summary>
public static class ZipHandler {
    private const string UnknownArchiveName = "archive.zip";

    /// <summary>
    /// Extracts image, document, and Excel members from a zip archive.
    /// </summary>
    /// <param name="zipFilePath">Path to the zip archive to inspect.</param>
    /// <param name="extractionRootPath">Job temp folder where healthy members are extracted.</param>
    /// <param name="policy">Extraction limits; defaults to PRISM config defaults when omitted.</param>
    /// <returns>Healthy extracted members plus manifest-facing KO records.</returns>
    public static ZipExtractionResult ExtractProcessableMembers(string zipFilePath, string extractionRootPath, ZipExtractionPolicy? policy = null) {
        ZipExtractionPolicy activePolicy = policy ?? ZipExtractionPolicy.CreateDefault();
        List<ZipExtractedMember> extractedMembers = [];
        List<ZipMemberKoRecord> koRecords = [];

        ValidateZipFilePath(zipFilePath);
        ValidateExtractionRootPath(extractionRootPath);
        Directory.CreateDirectory(extractionRootPath);

        string archiveDisplayPath = zipFilePath;
        ExtractArchiveIntoCollections(
            zipFilePath,
            archiveDisplayPath,
            extractionRootPath,
            activePolicy,
            zipDepth: 0,
            extractedMembers,
            koRecords);

        return new ZipExtractionResult(extractedMembers, koRecords);
    }

    /// <summary>
    /// Extracts one archive into the shared result collections.
    /// </summary>
    /// <param name="zipFilePath">Local path of the archive to inspect.</param>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="extractionRootPath">Job temp folder where healthy members are extracted.</param>
    /// <param name="policy">Extraction limits.</param>
    /// <param name="zipDepth">Nested zip depth for the current archive.</param>
    /// <param name="extractedMembers">Shared collection of healthy extracted members.</param>
    /// <param name="koRecords">Shared collection of KO records.</param>
    private static void ExtractArchiveIntoCollections(
        string zipFilePath,
        string archiveDisplayPath,
        string extractionRootPath,
        ZipExtractionPolicy policy,
        int zipDepth,
        List<ZipExtractedMember> extractedMembers,
        List<ZipMemberKoRecord> koRecords) {
        FileInfo zipFileInfo = new(zipFilePath);
        if (zipFileInfo.Length > policy.MaxZipArchiveBytes) {
            koRecords.Add(CreateOversizedKoRecord(
                archiveDisplayPath,
                memberPath: null,
                originalFileName: Path.GetFileName(zipFilePath),
                expandedByteLength: zipFileInfo.Length,
                limitByteLength: policy.MaxZipArchiveBytes));
            return;
        }

        IReadOnlyDictionary<string, bool> encryptedEntriesByName;
        try {
            encryptedEntriesByName = ZipCentralDirectoryReader.ReadEncryptionFlagsByEntryName(zipFilePath);
        }
        catch (InvalidDataException) {
            koRecords.Add(CreateCorruptKoRecord(
                archiveDisplayPath,
                memberPath: null,
                originalFileName: Path.GetFileName(zipFilePath),
                safeMessage: "The zip archive could not be read."));
            return;
        }
        catch (EndOfStreamException) {
            koRecords.Add(CreateCorruptKoRecord(
                archiveDisplayPath,
                memberPath: null,
                originalFileName: Path.GetFileName(zipFilePath),
                safeMessage: "The zip archive ended before its metadata could be read."));
            return;
        }

        try {
            using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
            ProcessArchiveEntries(
                archive,
                archiveDisplayPath,
                extractionRootPath,
                policy,
                zipDepth,
                encryptedEntriesByName,
                extractedMembers,
                koRecords);
        }
        catch (InvalidDataException) {
            koRecords.Add(CreateCorruptKoRecord(
                archiveDisplayPath,
                memberPath: null,
                originalFileName: Path.GetFileName(zipFilePath),
                safeMessage: "The zip archive could not be opened."));
        }
        catch (IOException) {
            koRecords.Add(CreateCorruptKoRecord(
                archiveDisplayPath,
                memberPath: null,
                originalFileName: Path.GetFileName(zipFilePath),
                safeMessage: "The zip archive could not be opened safely."));
        }
        catch (UnauthorizedAccessException) {
            koRecords.Add(CreateCorruptKoRecord(
                archiveDisplayPath,
                memberPath: null,
                originalFileName: Path.GetFileName(zipFilePath),
                safeMessage: "The zip archive could not be accessed."));
        }
    }

    /// <summary>
    /// Processes every archive entry using PRISM zip import policy.
    /// </summary>
    /// <param name="archive">Opened zip archive.</param>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="extractionRootPath">Job temp folder where healthy members are extracted.</param>
    /// <param name="policy">Extraction limits.</param>
    /// <param name="zipDepth">Nested zip depth for the current archive.</param>
    /// <param name="encryptedEntriesByName">Encryption flags keyed by entry name.</param>
    /// <param name="extractedMembers">Shared collection of healthy extracted members.</param>
    /// <param name="koRecords">Shared collection of KO records.</param>
    private static void ProcessArchiveEntries(
        ZipArchive archive,
        string archiveDisplayPath,
        string extractionRootPath,
        ZipExtractionPolicy policy,
        int zipDepth,
        IReadOnlyDictionary<string, bool> encryptedEntriesByName,
        List<ZipExtractedMember> extractedMembers,
        List<ZipMemberKoRecord> koRecords) {
        int entryIndex = 0;

        foreach (ZipArchiveEntry entry in archive.Entries) {
            entryIndex++;

            if (IsDirectoryEntry(entry)) {
                continue;
            }

            ProcessArchiveEntry(
                entry,
                entryIndex,
                archiveDisplayPath,
                extractionRootPath,
                policy,
                zipDepth,
                encryptedEntriesByName,
                extractedMembers,
                koRecords);
        }
    }

    /// <summary>
    /// Processes one archive entry using PRISM zip import policy.
    /// </summary>
    /// <param name="entry">Archive entry to process.</param>
    /// <param name="entryIndex">Stable archive-order index used for duplicate filenames.</param>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="extractionRootPath">Job temp folder where healthy members are extracted.</param>
    /// <param name="policy">Extraction limits.</param>
    /// <param name="zipDepth">Nested zip depth for the current archive.</param>
    /// <param name="encryptedEntriesByName">Encryption flags keyed by entry name.</param>
    /// <param name="extractedMembers">Shared collection of healthy extracted members.</param>
    /// <param name="koRecords">Shared collection of KO records.</param>
    private static void ProcessArchiveEntry(
        ZipArchiveEntry entry,
        int entryIndex,
        string archiveDisplayPath,
        string extractionRootPath,
        ZipExtractionPolicy policy,
        int zipDepth,
        IReadOnlyDictionary<string, bool> encryptedEntriesByName,
        List<ZipExtractedMember> extractedMembers,
        List<ZipMemberKoRecord> koRecords) {
        string memberPath = entry.FullName;

        // Carries the member's folder structure (forward-slash normalized) rather than only the bare
        // leaf name, so downstream folder-name matching (FolderNameEnricher) can see path segments.
        // memberPath itself stays untouched below: extraction-path building, KO lookups, and the
        // encrypted-entry check all key on the archive's own separator convention.
        string originalFileName = BuildOriginalFileName(memberPath);
        bool hasProcessableFileName = ZipMemberTriage.HasProcessableFileName(memberPath);

        if (IsEncryptedEntry(memberPath, encryptedEntriesByName)) {
            if (hasProcessableFileName) {
                koRecords.Add(CreatePasswordProtectedKoRecord(
                    archiveDisplayPath,
                    memberPath,
                    originalFileName,
                    entry.Length));
            }

            return;
        }

        byte[] headerBytes;
        try {
            headerBytes = ReadHeaderBytes(entry, policy.HeaderProbeBytes);
        }
        catch (InvalidDataException) {
            AddCorruptKoRecordForProcessableEntry(
                hasProcessableFileName,
                koRecords,
                archiveDisplayPath,
                memberPath,
                originalFileName,
                "The zip member could not be read.");
            return;
        }
        catch (NotSupportedException) {
            AddPasswordProtectedKoRecordForProcessableEntry(
                hasProcessableFileName,
                koRecords,
                archiveDisplayPath,
                memberPath,
                originalFileName,
                entry.Length);
            return;
        }
        catch (IOException) {
            AddCorruptKoRecordForProcessableEntry(
                hasProcessableFileName,
                koRecords,
                archiveDisplayPath,
                memberPath,
                originalFileName,
                "The zip member could not be read safely.");
            return;
        }

        ZipMemberMediaKind mediaKind = ZipMemberTriage.TriageProcessableMediaKind(
            memberPath,
            headerBytes);

        if (mediaKind == ZipMemberMediaKind.Ignored) {
            if (hasProcessableFileName) {
                koRecords.Add(CreateMalformedKoRecord(
                    archiveDisplayPath,
                    memberPath,
                    originalFileName,
                    entry.Length,
                    "The zip member extension is processable, but its bytes do not match supported media."));
            }

            return;
        }

        long byteLimit = GetExpandedByteLimit(mediaKind, policy);
        if (entry.Length > byteLimit) {
            koRecords.Add(CreateOversizedKoRecord(
                archiveDisplayPath,
                memberPath,
                originalFileName,
                entry.Length,
                byteLimit));
            return;
        }

        if (!TryBuildSafeExtractionPath(
            extractionRootPath,
            archiveDisplayPath,
            entryIndex,
            memberPath,
            out string extractedFilePath)) {
            koRecords.Add(CreateMalformedKoRecord(
                archiveDisplayPath,
                memberPath,
                originalFileName,
                entry.Length,
                "The zip member path is malformed or unsafe."));
            return;
        }

        if (!TryExtractEntryToPath(
            entry,
            extractedFilePath,
            byteLimit,
            out ZipMemberKoRecord? extractionKoRecord,
            archiveDisplayPath,
            memberPath,
            originalFileName)) {
            if (extractionKoRecord is not null) {
                koRecords.Add(extractionKoRecord);
            }

            return;
        }

        if (mediaKind == ZipMemberMediaKind.NestedZip) {
            ProcessNestedZipEntry(
                extractedFilePath,
                archiveDisplayPath,
                memberPath,
                extractionRootPath,
                policy,
                zipDepth,
                extractedMembers,
                koRecords);
            return;
        }

        extractedMembers.Add(new ZipExtractedMember(
            archiveDisplayPath,
            memberPath,
            originalFileName,
            mediaKind,
            extractedFilePath,
            entry.Length,
            zipDepth));
    }

    /// <summary>
    /// Builds the member's original name from its in-archive path: separators normalized to forward
    /// slash so folder structure survives (needed by downstream folder-name matching), regardless of
    /// whether the archive was written with '/' or '\' entry names.
    /// </summary>
    /// <param name="memberPath">Member path inside the archive.</param>
    /// <returns>The member path with backslashes normalized to forward slashes.</returns>
    private static string BuildOriginalFileName(string memberPath) {
        return memberPath.Replace('\\', '/');
    }

    /// <summary>
    /// Processes an extracted nested zip archive.
    /// </summary>
    /// <param name="nestedZipPath">Local path to the extracted nested archive.</param>
    /// <param name="archiveDisplayPath">Safe path of the parent archive.</param>
    /// <param name="memberPath">Member path of the nested archive.</param>
    /// <param name="extractionRootPath">Job temp folder where healthy members are extracted.</param>
    /// <param name="policy">Extraction limits.</param>
    /// <param name="zipDepth">Current nested zip depth.</param>
    /// <param name="extractedMembers">Shared collection of healthy extracted members.</param>
    /// <param name="koRecords">Shared collection of KO records.</param>
    private static void ProcessNestedZipEntry(
        string nestedZipPath,
        string archiveDisplayPath,
        string memberPath,
        string extractionRootPath,
        ZipExtractionPolicy policy,
        int zipDepth,
        List<ZipExtractedMember> extractedMembers,
        List<ZipMemberKoRecord> koRecords) {
        if (zipDepth >= policy.MaxNestedZipDepth) {
            koRecords.Add(CreateMalformedKoRecord(
                archiveDisplayPath,
                memberPath,
                BuildOriginalFileName(memberPath),
                new FileInfo(nestedZipPath).Length,
                "The nested zip depth exceeds the configured limit."));
            return;
        }

        string nestedArchiveDisplayPath = BuildNestedArchiveDisplayPath(archiveDisplayPath, memberPath);
        ExtractArchiveIntoCollections(
            nestedZipPath,
            nestedArchiveDisplayPath,
            extractionRootPath,
            policy,
            zipDepth + 1,
            extractedMembers,
            koRecords);
    }

    /// <summary>
    /// Reads the initial bytes needed to classify a member.
    /// </summary>
    /// <param name="entry">Archive entry to read.</param>
    /// <param name="headerProbeBytes">Maximum number of bytes to read.</param>
    /// <returns>The bytes read from the entry stream.</returns>
    private static byte[] ReadHeaderBytes(ZipArchiveEntry entry, int headerProbeBytes) {
        using Stream entryStream = entry.Open();
        byte[] buffer = new byte[headerProbeBytes];
        int totalReadBytes = 0;

        while (totalReadBytes < buffer.Length) {
            int readBytes = entryStream.Read(
                buffer,
                totalReadBytes,
                buffer.Length - totalReadBytes);

            if (readBytes == 0) {
                break;
            }

            totalReadBytes += readBytes;
        }

        byte[] headerBytes = new byte[totalReadBytes];
        Array.Copy(buffer, headerBytes, totalReadBytes);
        return headerBytes;
    }

    /// <summary>
    /// Extracts a member to disk while enforcing the expanded byte limit.
    /// </summary>
    /// <param name="entry">Archive entry to extract.</param>
    /// <param name="extractedFilePath">Destination path inside the job temp folder.</param>
    /// <param name="byteLimit">Maximum expanded bytes allowed.</param>
    /// <param name="koRecord">KO record when extraction fails.</param>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="memberPath">Member path inside the archive.</param>
    /// <param name="originalFileName">Original member filename.</param>
    /// <returns>True when extraction succeeds.</returns>
    private static bool TryExtractEntryToPath(
        ZipArchiveEntry entry,
        string extractedFilePath,
        long byteLimit,
        out ZipMemberKoRecord? koRecord,
        string archiveDisplayPath,
        string memberPath,
        string originalFileName) {
        koRecord = null;
        Directory.CreateDirectory(Path.GetDirectoryName(extractedFilePath)!);

        try {
            using Stream entryStream = entry.Open();
            using FileStream outputStream = File.Create(extractedFilePath);
            CopyEntryStreamWithLimit(entryStream, outputStream, byteLimit);
            return true;
        }
        catch (InvalidDataException) {
            koRecord = CreateCorruptKoRecord(
                archiveDisplayPath,
                memberPath,
                originalFileName,
                "The zip member could not be extracted.");
            DeletePartialExtraction(extractedFilePath);
            return false;
        }
        catch (NotSupportedException) {
            koRecord = CreatePasswordProtectedKoRecord(
                archiveDisplayPath,
                memberPath,
                originalFileName,
                entry.Length);
            DeletePartialExtraction(extractedFilePath);
            return false;
        }
        catch (ZipMemberOversizedException oversizedException) {
            koRecord = CreateOversizedKoRecord(
                archiveDisplayPath,
                memberPath,
                originalFileName,
                oversizedException.ObservedByteLength,
                byteLimit);
            DeletePartialExtraction(extractedFilePath);
            return false;
        }
        catch (IOException) {
            koRecord = CreateCorruptKoRecord(
                archiveDisplayPath,
                memberPath,
                originalFileName,
                "The zip member could not be written safely.");
            DeletePartialExtraction(extractedFilePath);
            return false;
        }
        catch (UnauthorizedAccessException) {
            koRecord = CreateMalformedKoRecord(
                archiveDisplayPath,
                memberPath,
                originalFileName,
                entry.Length,
                "The zip member could not be written to the extraction folder.");
            DeletePartialExtraction(extractedFilePath);
            return false;
        }
    }

    /// <summary>
    /// Copies a stream and stops as soon as the configured byte limit is exceeded.
    /// </summary>
    /// <param name="inputStream">Entry stream to read.</param>
    /// <param name="outputStream">Output stream to write.</param>
    /// <param name="byteLimit">Maximum expanded bytes allowed.</param>
    private static void CopyEntryStreamWithLimit(
        Stream inputStream,
        Stream outputStream,
        long byteLimit) {
        byte[] buffer = new byte[81920];
        long totalReadBytes = 0;

        while (true) {
            int readBytes = inputStream.Read(buffer, 0, buffer.Length);
            if (readBytes == 0) {
                return;
            }

            totalReadBytes += readBytes;
            if (totalReadBytes > byteLimit) {
                throw new ZipMemberOversizedException(totalReadBytes);
            }

            outputStream.Write(buffer, 0, readBytes);
        }
    }

    /// <summary>
    /// Builds a safe extraction path that cannot escape the extraction root.
    /// </summary>
    /// <param name="extractionRootPath">Job temp folder where healthy members are extracted.</param>
    /// <param name="archiveDisplayPath">Safe archive path or display name.</param>
    /// <param name="entryIndex">Stable archive-order index used for duplicate filenames.</param>
    /// <param name="memberPath">Member path inside the archive.</param>
    /// <param name="extractedFilePath">Safe destination path when successful.</param>
    /// <returns>True when the path is valid and inside the extraction root.</returns>
    private static bool TryBuildSafeExtractionPath(
        string extractionRootPath,
        string archiveDisplayPath,
        int entryIndex,
        string memberPath,
        out string extractedFilePath) {
        extractedFilePath = string.Empty;

        if (!TryGetSafePathSegments(memberPath, out IReadOnlyList<string> safeMemberSegments)) {
            return false;
        }

        string archiveFolderName = BuildSafeArchiveFolderName(archiveDisplayPath);
        string entryFolderName = entryIndex.ToString("D6");
        string extractionBasePath = Path.Combine(
            extractionRootPath,
            archiveFolderName,
            entryFolderName);
        string memberRelativePath = Path.Combine(safeMemberSegments.ToArray());
        string candidatePath = Path.GetFullPath(Path.Combine(extractionBasePath, memberRelativePath));
        string rootFullPath = Path.GetFullPath(extractionRootPath);

        if (!IsPathInsideRoot(candidatePath, rootFullPath)) {
            return false;
        }

        extractedFilePath = candidatePath;
        return true;
    }

    /// <summary>
    /// Converts a zip member path into safe path segments.
    /// </summary>
    /// <param name="memberPath">Member path inside the archive.</param>
    /// <param name="safeSegments">Safe path segments when successful.</param>
    /// <returns>True when every segment is safe.</returns>
    private static bool TryGetSafePathSegments(
        string memberPath,
        out IReadOnlyList<string> safeSegments) {
        safeSegments = [];

        if (string.IsNullOrWhiteSpace(memberPath)
            || Path.IsPathFullyQualified(memberPath)) {
            return false;
        }

        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        string[] rawSegments = memberPath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);

        if (rawSegments.Length == 0) {
            return false;
        }

        List<string> validatedSegments = [];
        foreach (string rawSegment in rawSegments) {
            if (rawSegment == "." || rawSegment == "..") {
                return false;
            }

            if (rawSegment.IndexOfAny(invalidFileNameChars) >= 0) {
                return false;
            }

            validatedSegments.Add(rawSegment);
        }

        safeSegments = validatedSegments;
        return true;
    }

    /// <summary>
    /// Builds a stable, filesystem-safe folder name for extracted archive members.
    /// </summary>
    /// <param name="archiveDisplayPath">Safe archive path or display name.</param>
    /// <returns>A safe folder name.</returns>
    private static string BuildSafeArchiveFolderName(string archiveDisplayPath) {
        string archiveFileName = Path.GetFileNameWithoutExtension(archiveDisplayPath);
        if (string.IsNullOrWhiteSpace(archiveFileName)) {
            archiveFileName = UnknownArchiveName;
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] safeChars = archiveFileName
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray();

        return new string(safeChars);
    }

    /// <summary>
    /// Determines whether a path is contained by an extraction root.
    /// </summary>
    /// <param name="candidatePath">Candidate extracted file path.</param>
    /// <param name="rootFullPath">Full extraction root path.</param>
    /// <returns>True when the candidate path stays inside the root.</returns>
    private static bool IsPathInsideRoot(string candidatePath, string rootFullPath) {
        string normalizedRoot = rootFullPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether an archive entry represents a directory.
    /// </summary>
    /// <param name="entry">Archive entry to inspect.</param>
    /// <returns>True when the entry is a directory marker.</returns>
    private static bool IsDirectoryEntry(ZipArchiveEntry entry) {
        return string.IsNullOrEmpty(entry.Name)
            || entry.FullName.EndsWith("/", StringComparison.Ordinal)
            || entry.FullName.EndsWith("\\", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether central-directory metadata marks an entry as encrypted.
    /// </summary>
    /// <param name="memberPath">Member path inside the archive.</param>
    /// <param name="encryptedEntriesByName">Encryption flags keyed by entry name.</param>
    /// <returns>True when the entry is encrypted.</returns>
    private static bool IsEncryptedEntry(
        string memberPath,
        IReadOnlyDictionary<string, bool> encryptedEntriesByName) {
        return encryptedEntriesByName.TryGetValue(memberPath, out bool isEncrypted)
            && isEncrypted;
    }

    /// <summary>
    /// Gets the expanded byte limit for a media kind.
    /// </summary>
    /// <param name="mediaKind">Triaged media kind.</param>
    /// <param name="policy">Extraction limits.</param>
    /// <returns>The maximum expanded bytes for this member kind.</returns>
    private static long GetExpandedByteLimit(
        ZipMemberMediaKind mediaKind,
        ZipExtractionPolicy policy) {
        return mediaKind switch {
            ZipMemberMediaKind.Image => policy.MaxImageMemberBytes,
            ZipMemberMediaKind.Excel => policy.MaxExcelMemberBytes,
            ZipMemberMediaKind.NestedZip => policy.MaxZipArchiveBytes,
            _ => 0
        };
    }

    /// <summary>
    /// Builds a nested archive display path for manifest source references.
    /// </summary>
    /// <param name="archiveDisplayPath">Parent archive display path.</param>
    /// <param name="memberPath">Nested archive member path.</param>
    /// <returns>A display path using archive/member notation.</returns>
    private static string BuildNestedArchiveDisplayPath(
        string archiveDisplayPath,
        string memberPath) {
        return $"{archiveDisplayPath}!{memberPath}";
    }

    /// <summary>
    /// Adds a corrupt KO record only when the entry is processable.
    /// </summary>
    /// <param name="hasProcessableFileName">Whether the entry filename is processable.</param>
    /// <param name="koRecords">Shared collection of KO records.</param>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="memberPath">Member path inside the archive.</param>
    /// <param name="originalFileName">Original member filename.</param>
    /// <param name="safeMessage">Safe message for manifest projection.</param>
    private static void AddCorruptKoRecordForProcessableEntry(
        bool hasProcessableFileName,
        List<ZipMemberKoRecord> koRecords,
        string archiveDisplayPath,
        string memberPath,
        string originalFileName,
        string safeMessage) {
        if (!hasProcessableFileName) {
            return;
        }

        koRecords.Add(CreateCorruptKoRecord(
            archiveDisplayPath,
            memberPath,
            originalFileName,
            safeMessage));
    }

    /// <summary>
    /// Adds a password-protected KO record only when the entry is processable.
    /// </summary>
    /// <param name="hasProcessableFileName">Whether the entry filename is processable.</param>
    /// <param name="koRecords">Shared collection of KO records.</param>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="memberPath">Member path inside the archive.</param>
    /// <param name="originalFileName">Original member filename.</param>
    /// <param name="expandedByteLength">Expanded member size when available.</param>
    private static void AddPasswordProtectedKoRecordForProcessableEntry(
        bool hasProcessableFileName,
        List<ZipMemberKoRecord> koRecords,
        string archiveDisplayPath,
        string memberPath,
        string originalFileName,
        long expandedByteLength) {
        if (!hasProcessableFileName) {
            return;
        }

        koRecords.Add(CreatePasswordProtectedKoRecord(
            archiveDisplayPath,
            memberPath,
            originalFileName,
            expandedByteLength));
    }

    /// <summary>
    /// Creates a KO record for a corrupt zip member.
    /// </summary>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="memberPath">Member path inside the archive when available.</param>
    /// <param name="originalFileName">Original filename when available.</param>
    /// <param name="safeMessage">Safe message for manifest projection.</param>
    /// <returns>A manifest-facing KO record.</returns>
    private static ZipMemberKoRecord CreateCorruptKoRecord(
        string archiveDisplayPath,
        string? memberPath,
        string? originalFileName,
        string safeMessage) {
        return new ZipMemberKoRecord(
            archiveDisplayPath,
            memberPath,
            originalFileName,
            ZipMemberKoRecord.ZipExtractSourceStage,
            ZipMemberKoRecord.CorruptZipMemberReason,
            ZipMemberKoRecord.CorruptImagesKoGroup,
            safeMessage,
            ExpandedByteLength: null,
            LimitByteLength: null,
            BuildSafeDetails(archiveDisplayPath, memberPath));
    }

    /// <summary>
    /// Creates a KO record for an encrypted zip member.
    /// </summary>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="memberPath">Member path inside the archive.</param>
    /// <param name="originalFileName">Original member filename.</param>
    /// <param name="expandedByteLength">Expanded member size when available.</param>
    /// <returns>A manifest-facing KO record.</returns>
    private static ZipMemberKoRecord CreatePasswordProtectedKoRecord(
        string archiveDisplayPath,
        string memberPath,
        string originalFileName,
        long expandedByteLength) {
        return new ZipMemberKoRecord(
            archiveDisplayPath,
            memberPath,
            originalFileName,
            ZipMemberKoRecord.ZipExtractSourceStage,
            ZipMemberKoRecord.PasswordProtectedReason,
            ZipMemberKoRecord.PasswordProtectedZipKoGroup,
            "The zip member is password-protected.",
            expandedByteLength,
            LimitByteLength: null,
            BuildSafeDetails(archiveDisplayPath, memberPath));
    }

    /// <summary>
    /// Creates a KO record for an oversized processable zip member.
    /// </summary>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="memberPath">Member path inside the archive when available.</param>
    /// <param name="originalFileName">Original filename when available.</param>
    /// <param name="expandedByteLength">Expanded member size.</param>
    /// <param name="limitByteLength">Configured byte limit.</param>
    /// <returns>A manifest-facing KO record.</returns>
    private static ZipMemberKoRecord CreateOversizedKoRecord(
        string archiveDisplayPath,
        string? memberPath,
        string? originalFileName,
        long expandedByteLength,
        long limitByteLength) {
        return new ZipMemberKoRecord(
            archiveDisplayPath,
            memberPath,
            originalFileName,
            ZipMemberKoRecord.ZipExtractSourceStage,
            ZipMemberKoRecord.OversizedZipMemberReason,
            ZipMemberKoRecord.OversizedZipMembersKoGroup,
            "The zip member is larger than the configured limit.",
            expandedByteLength,
            limitByteLength,
            BuildSafeDetails(archiveDisplayPath, memberPath));
    }

    /// <summary>
    /// Creates a KO record for malformed processable zip metadata.
    /// </summary>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="memberPath">Member path inside the archive.</param>
    /// <param name="originalFileName">Original member filename.</param>
    /// <param name="expandedByteLength">Expanded member size when available.</param>
    /// <param name="safeMessage">Safe message for manifest projection.</param>
    /// <returns>A manifest-facing KO record.</returns>
    private static ZipMemberKoRecord CreateMalformedKoRecord(
        string archiveDisplayPath,
        string memberPath,
        string originalFileName,
        long expandedByteLength,
        string safeMessage) {
        return new ZipMemberKoRecord(
            archiveDisplayPath,
            memberPath,
            originalFileName,
            ZipMemberKoRecord.ZipExtractSourceStage,
            ZipMemberKoRecord.MalformedZipMemberReason,
            ZipMemberKoRecord.CorruptImagesKoGroup,
            safeMessage,
            expandedByteLength,
            LimitByteLength: null,
            BuildSafeDetails(archiveDisplayPath, memberPath));
    }

    /// <summary>
    /// Builds bounded details shared by zip KO records.
    /// </summary>
    /// <param name="archiveDisplayPath">Safe archive path to expose in KO records.</param>
    /// <param name="memberPath">Member path inside the archive when available.</param>
    /// <returns>Safe detail key/value pairs.</returns>
    private static IReadOnlyDictionary<string, string> BuildSafeDetails(
        string archiveDisplayPath,
        string? memberPath) {
        Dictionary<string, string> safeDetails = new() {
            ["archive"] = archiveDisplayPath
        };

        if (!string.IsNullOrWhiteSpace(memberPath)) {
            safeDetails["member"] = memberPath;
        }

        return safeDetails;
    }

    /// <summary>
    /// Deletes a partially extracted file after member failure.
    /// </summary>
    /// <param name="extractedFilePath">Partial extracted file path.</param>
    private static void DeletePartialExtraction(string extractedFilePath) {
        if (File.Exists(extractedFilePath)) {
            File.Delete(extractedFilePath);
        }
    }

    /// <summary>
    /// Validates the input zip path before extraction starts.
    /// </summary>
    /// <param name="zipFilePath">Path to validate.</param>
    private static void ValidateZipFilePath(string zipFilePath) {
        if (string.IsNullOrWhiteSpace(zipFilePath)) {
            throw new ArgumentException("Zip file path is required.", nameof(zipFilePath));
        }

        if (!File.Exists(zipFilePath)) {
            throw new FileNotFoundException("Zip file was not found.", zipFilePath);
        }
    }

    /// <summary>
    /// Validates the extraction root path before extraction starts.
    /// </summary>
    /// <param name="extractionRootPath">Extraction root path to validate.</param>
    private static void ValidateExtractionRootPath(string extractionRootPath) {
        if (string.IsNullOrWhiteSpace(extractionRootPath)) {
            throw new ArgumentException(
                "Extraction root path is required.",
                nameof(extractionRootPath));
        }
    }

    /// <summary>
    /// Signals that a zip member expanded past the configured byte limit while streaming.
    /// </summary>
    private sealed class ZipMemberOversizedException : Exception {
        /// <summary>
        /// Initializes a new oversized-member exception.
        /// </summary>
        /// <param name="observedByteLength">Observed expanded bytes before extraction stopped.</param>
        public ZipMemberOversizedException(long observedByteLength)
            : base("Zip member expanded beyond the configured byte limit.") {
            this.ObservedByteLength = observedByteLength;
        }

        /// <summary>
        /// Gets the observed expanded bytes before extraction stopped.
        /// </summary>
        public long ObservedByteLength { get; }
    }
}
