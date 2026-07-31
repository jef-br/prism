using System.Text.Json.Serialization;

namespace Prism.Contracts;

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

    // Source file reference

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

    /// <summary>
    /// Encoded normalized JPEG bytes, carried forward in memory only when the Imported stage ran the
    /// full decode/re-encode path (not the already-conforming-JPEG fast path). Lets an in-process Match
    /// build its <c>Image&lt;Rgba32&gt;</c> from these bytes instead of re-reading <see cref="NormalizedJpgPath"/>
    /// from disk (T-3500). <see cref="JsonIgnoreAttribute"/> keeps this out of the HTTP wire contract —
    /// a cross-process Match always falls back to <see cref="NormalizedJpgPath"/>. Null once consumed.
    /// </summary>
    [JsonIgnore]
    public byte[]? NormalizedJpegBytes { get; set; }

    /// <summary>
    /// Alpha-derived subject detection captured before the Imported stage flattens transparency onto
    /// white, when the source image carried a real alpha channel. Null when the source had no alpha
    /// channel, or no pixel reached the configured opacity threshold. When the opaque region covers the
    /// whole frame, <see cref="SubjectDetectionResult.IsWholeFrameFallback"/> is true instead of this being
    /// null. Copied onto <c>ImageRecord_LAMBDA.Subject</c> at lambda creation so downstream detection can
    /// prefer this measured mask over an inferred one.
    /// </summary>
    public SubjectDetectionResult? Subject { get; set; }

    // Downstream matching tokens (populated downstream)

    /// <summary>String tokens extracted from the original filename.</summary>
    public string[] StringTokens { get; set; } = [];

    /// <summary>Numeric tokens extracted from the original filename.</summary>
    public string[] NumericTokens { get; set; } = [];

    /// <summary>Classification tokens attached during the Classified stage.</summary>
    public ClassificationToken[] ClassificationTokens { get; set; } = [];

    /// <summary>FamilyIDRecord candidates resolved during the Matched stage.</summary>
    public FamilyIDRecord[] FamilyIDCandidates { get; set; } = [];
}
