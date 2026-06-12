/// <summary>
/// Represents one source image after import normalization, before classification and matching.
/// Handoff record between the Imported stage and all downstream stages.
/// </summary>
public class ImageRecord_INPUT : ImageRecord_Base
{
    // -------------------------------------------------------------------------
    // Import provenance
    // -------------------------------------------------------------------------

    /// <summary>
    /// Describes where the image originated: local path, folder member, stream,
    /// multipart upload, URL, or zip member.
    /// </summary>
    public ImageSourceKind SourceKind { get; set; } = ImageSourceKind.Unknown;

    /// <summary>
    /// Original content type reported by the source when known.
    /// </summary>
    public string? OriginalContentType { get; set; }

    /// <summary>
    /// Original byte length of the source file when known.
    /// </summary>
    public long? ByteLength { get; set; }

    /// <summary>
    /// Accepted media classification determined during import triage.
    /// </summary>
    public ImportedMediaKind AcceptedMediaKind { get; set; } = ImportedMediaKind.Unknown;

    // -------------------------------------------------------------------------
    // Source file reference
    // -------------------------------------------------------------------------

    /// <summary>
    /// Absolute path to a job-temp copy of the uploaded file when the API or caller has
    /// spilled the uploaded bytes to disk before enqueuing. When populated, the Imported
    /// stage reads from this path. When null, <see cref="InitialFullName"/> is treated as
    /// the readable local source file path.
    /// </summary>
    public string? TempFilePath { get; set; }

    // -------------------------------------------------------------------------
    // Normalized artifact
    // -------------------------------------------------------------------------

    /// <summary>
    /// Absolute path to the normalized flat JPG written by the Imported stage.
    /// Null until the image has been successfully normalized.
    /// </summary>
    public string? NormalizedJpgPath { get; set; }

    /// <summary>
    /// Width of the normalized image in pixels. Set after normalization.
    /// </summary>
    public int NormalizedWidth { get; set; }

    /// <summary>
    /// Height of the normalized image in pixels. Set after normalization.
    /// </summary>
    public int NormalizedHeight { get; set; }

    // -------------------------------------------------------------------------
    // Import status
    // -------------------------------------------------------------------------

    /// <summary>
    /// Import outcome for this image.
    /// </summary>
    public ImportStatus ImportStatus { get; set; } = ImportStatus.Pending;

    /// <summary>
    /// Safe KO reason code when the image could not be imported.
    /// </summary>
    public string? KoReasonCode { get; set; }

    /// <summary>
    /// Safe diagnostic message when the image could not be imported.
    /// </summary>
    public string? KoSafeMessage { get; set; }

    // -------------------------------------------------------------------------
    // Downstream matching tokens (populated downstream)
    // -------------------------------------------------------------------------

    /// <summary>Numeric tokens extracted from the original filename.</summary>
    public string[] StringTokens { get; set; } = [];

    /// <summary>String tokens extracted from the original filename.</summary>
    public string[] NumericTokens { get; set; } = [];

    /// <summary>Classification tokens attached during the Classified stage.</summary>
    public ClassificationToken[] ClassificationTokens { get; set; } = [];

    /// <summary>FamilyRecord candidates resolved during the Matched stage.</summary>
    public FamilyRecord[] FamilyIDCandidates { get; set; } = [];
}

/// <summary>
/// Describes where an image originated.
/// </summary>
public enum ImageSourceKind
{
    /// <summary>Source kind not yet determined.</summary>
    Unknown = 0,

    /// <summary>Local file path supplied directly.</summary>
    LocalPath = 1,

    /// <summary>Member of a local folder scan.</summary>
    FolderMember = 2,

    /// <summary>In-memory stream with caller-supplied metadata.</summary>
    Stream = 3,

    /// <summary>Multipart upload from the API.</summary>
    MultipartUpload = 4,

    /// <summary>Fetched from a remote URL.</summary>
    RemoteUrl = 5,

    /// <summary>Extracted member from a zip archive.</summary>
    ZipMember = 6
}

/// <summary>
/// Accepted media classification assigned during import triage.
/// </summary>
public enum ImportedMediaKind
{
    /// <summary>Media kind not yet determined.</summary>
    Unknown = 0,

    /// <summary>JPEG image (original or converted).</summary>
    Jpeg = 1,

    /// <summary>PNG image (will be converted to JPEG).</summary>
    Png = 2,

    /// <summary>TIFF image (may be multipage; converted to JPEG).</summary>
    Tiff = 3,

    /// <summary>PDF document (may be multipage; first page rendered as JPEG).</summary>
    Pdf = 4,

    /// <summary>WebP image (converted to JPEG).</summary>
    Webp = 5,

    /// <summary>BMP image (converted to JPEG).</summary>
    Bmp = 6,

    /// <summary>GIF image (converted to JPEG).</summary>
    Gif = 7
}

/// <summary>
/// Import outcome for a single image.
/// </summary>
public enum ImportStatus
{
    /// <summary>Not yet processed by the Imported stage.</summary>
    Pending = 0,

    /// <summary>Successfully imported and normalized.</summary>
    Ok = 1,

    /// <summary>Failed during import; excluded from downstream stages.</summary>
    Ko = 2
}
