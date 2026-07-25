namespace Prism.Lib.Ingress;

/// <summary>
/// Safe KO record emitted by the Imported stage for one image that could not be normalized.
/// Projected into <see cref="BatchManifest"/> KO groups.
/// </summary>
public sealed record ImportKoRecord {
    /// <summary>Source stage constant used in all import-stage KO records.</summary>
    public const string ImportSourceStage = "import";

    /// <summary>Reason code for a file that cannot be opened or decoded.</summary>
    public const string CorruptImageReason = "500";

    /// <summary>Reason code for a file where conversion to JPEG fails.</summary>
    public const string ConversionFailureReason = "541";

    /// <summary>Reason code for a file that is empty or below the configured minimum size.</summary>
    public const string FileTooSmallReason = "import.file_too_small";

    /// <summary>Reason code for a file that exceeds the configured maximum size.</summary>
    public const string FileTooLargeReason = "import.file_too_large";

    /// <summary>Reason code for an image whose pixel dimensions are below the configured input minimum.</summary>
    public const string ImageTooSmallReason = "import.image_too_small";

    /// <summary>Reason code for a file with an extension that is not accepted.</summary>
    public const string UnsupportedFormatReason = "import.unsupported_format";

    /// <summary>KO group for corrupt or undecodable images.</summary>
    public const string CorruptImagesKoGroup = "corrupt images";

    /// <summary>KO group for images that could not be converted.</summary>
    public const string ConversionFailedKoGroup = "conversion failed";

    /// <summary>KO group for oversized input files.</summary>
    public const string OversizedKoGroup = "oversized inputs";

    /// <summary>KO group for images whose pixel dimensions are below the accepted input minimum.</summary>
    public const string UndersizedKoGroup = "undersized inputs";

    /// <summary>KO group for unsupported input file formats.</summary>
    public const string UnsupportedFormatKoGroup = "unsupported format";

    /// <summary>
    /// Original filename as reported by the upload source.
    /// </summary>
    public string OriginalFileName { get; init; } = string.Empty;

    /// <summary>
    /// Safe source provenance (local path, zip archive name, remote URL host, etc.).
    /// </summary>
    public string SourceProvenance { get; init; } = string.Empty;

    /// <summary>
    /// Definitive stage name where the failure occurred.
    /// </summary>
    public string SourceStage { get; init; } = ImportSourceStage;

    /// <summary>
    /// Stable reason code for this KO.
    /// </summary>
    public string ReasonCode { get; init; } = string.Empty;

    /// <summary>
    /// KO group label for manifest grouping.
    /// </summary>
    public string KoGroup { get; init; } = string.Empty;

    /// <summary>
    /// Safe human-readable description of the failure.
    /// </summary>
    public string SafeMessage { get; init; } = string.Empty;

    /// <summary>
    /// Whether the caller can retry with the same input.
    /// </summary>
    public bool Retryable { get; init; }

    /// <summary>
    /// True — import KO does not stop the batch.
    /// </summary>
    public bool BatchContinues { get; init; } = true;

    /// <summary>
    /// Creates a KO record for a corrupt or undecodable image.
    /// </summary>
    /// <param name="originalFileName">Original filename.</param>
    /// <param name="sourceProvenance">Safe source provenance.</param>
    /// <param name="safeMessage">Safe description.</param>
    /// <returns>A corrupt-image KO record.</returns>
    public static ImportKoRecord CorruptImage(
        string originalFileName,
        string sourceProvenance,
        string safeMessage) {
        return new ImportKoRecord {
            OriginalFileName = originalFileName,
            SourceProvenance = sourceProvenance,
            ReasonCode = CorruptImageReason,
            KoGroup = CorruptImagesKoGroup,
            SafeMessage = safeMessage,
            Retryable = false,
            BatchContinues = true
        };
    }

    /// <summary>
    /// Creates a KO record for a JPEG conversion failure.
    /// </summary>
    /// <param name="originalFileName">Original filename.</param>
    /// <param name="sourceProvenance">Safe source provenance.</param>
    /// <param name="safeMessage">Safe description.</param>
    /// <returns>A conversion-failure KO record.</returns>
    public static ImportKoRecord ConversionFailure(
        string originalFileName,
        string sourceProvenance,
        string safeMessage) {
        return new ImportKoRecord {
            OriginalFileName = originalFileName,
            SourceProvenance = sourceProvenance,
            ReasonCode = ConversionFailureReason,
            KoGroup = ConversionFailedKoGroup,
            SafeMessage = safeMessage,
            Retryable = false,
            BatchContinues = true
        };
    }

    /// <summary>
    /// Creates a KO record for an unsupported input format.
    /// </summary>
    /// <param name="originalFileName">Original filename.</param>
    /// <param name="sourceProvenance">Safe source provenance.</param>
    /// <returns>An unsupported-format KO record.</returns>
    public static ImportKoRecord UnsupportedFormat(
        string originalFileName,
        string sourceProvenance) {
        return new ImportKoRecord {
            OriginalFileName = originalFileName,
            SourceProvenance = sourceProvenance,
            ReasonCode = UnsupportedFormatReason,
            KoGroup = UnsupportedFormatKoGroup,
            SafeMessage = "The input file format is not accepted by PRISM.",
            Retryable = false,
            BatchContinues = true
        };
    }
}
