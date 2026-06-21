/// <summary>
/// Represents one source image after import normalization, before classification and matching.
/// Handoff record between the Imported stage and all downstream stages.
/// </summary>
public class ImageRecord_INPUT : ImageRecord_Base {
    // Import provenance

    /// <summary>Describes where the image originated: local path, folder member, stream, multipart upload, URL, or zip member.</summary>
    public ImageSourceKind SourceKind { get; set; } = ImageSourceKind.Unknown;

    /// <summary>Original content type reported by the source when known.</summary>
    public string? OriginalContentType { get; set; }

    /// <summary>Original byte length of the source file when known.</summary>
    public long? ByteLength { get; set; }

    //─── Source file reference

    /// <summary>
    /// Absolute path to a job-temp copy of the uploaded file when the API or caller has
    /// spilled the uploaded bytes to disk before enqueuing. When populated, the Imported
    /// stage reads from this path. When null, <see cref="InitialFullName"/> is treated as
    /// the readable local source file path.
    /// </summary>
    public string? TempFilePath { get; set; }

    // Normalized artifact

    /// <summary>
    /// Absolute path to the normalized flat JPG written by the Imported stage.
    /// Null until the image has been successfully normalized.
    /// </summary>
    public string? NormalizedJpgPath { get; set; }

    /// <summary>Width of the normalized image in pixels. Set after normalization.</summary>
    public int NormalizedWidth { get; set; }

    /// <summary>Height of the normalized image in pixels. Set after normalization.</summary>
    public int NormalizedHeight { get; set; }

    //─── Import status

    /// <summary>Import outcome for this image.</summary>
    public ImportStatus ImportStatus { get; set; } = ImportStatus.Pending;

    /// <summary>Safe KO reason code when the image could not be imported.</summary>
    public string? KoReasonCode { get; set; }

    /// <summary>Safe diagnostic message when the image could not be imported.</summary>
    public string? KoSafeMessage { get; set; }

    //─── Downstream matching tokens (populated downstream)

    /// <summary>String tokens extracted from the original filename.</summary>
    public string[] StringTokens { get; set; } = [];

    /// <summary>Numeric tokens extracted from the original filename.</summary>
    public string[] NumericTokens { get; set; } = [];

    /// <summary>Classification tokens attached during the Classified stage.</summary>
    public ClassificationToken[] ClassificationTokens { get; set; } = [];

    /// <summary>FamilyRecord candidates resolved during the Matched stage.</summary>
    public FamilyRecord[] FamilyIDCandidates { get; set; } = [];
}