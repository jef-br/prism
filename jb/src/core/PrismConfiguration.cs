using System.Text.Json;

/// <summary>
/// Typed PRISM configuration loaded from Prism_Config.json at startup.
/// Owns all threshold, limit, and queue values used by the pipeline.
/// </summary>
public sealed class PrismConfiguration {
    // --- Input limits 

    public long MaximumRequestBytes { get; private set; }
    public int MinimumImageCountPerJob { get; private set; }
    public int MaximumImageCountPerJob { get; private set; }
    public long MinBytesPerImg { get; private set; }
    public long MaxBytesPerImg { get; private set; }
    public int MinXLSCount { get; private set; }
    public int MaxXLSCount { get; private set; }
    public long MinXLSBytes { get; private set; }
    public long MaxXLSBytes { get; private set; }
    public int MaxZipCount { get; private set; }
    public long MaxZipBytes { get; private set; }
    public int MaxNestDepthZip { get; private set; }

    // --- Classification thresholds 
    public double ThresholdForInfluentialTags { get; private set; }
    public double ThresholdForDiscardingClassificationTags { get; private set; }
    public bool ShouldDeduplicate { get; private set; }
    public int MaxHammingDistance { get; private set; }

    /// <summary>Phenotype ids exempt from visual deduplication (illustrations, technical drawings, labels). Never removed as duplicates.</summary>
    public IReadOnlyList<string> DeduplicationExemptPhenotypes { get; private set; } = [];

    // --- Matching weights 
    public double Weight_NumTokens { get; private set; }
    public double Weight_StringTokens { get; private set; }
    public double Weight_ClassifyingTags { get; private set; }
    public double Weight_SemanticRelevance { get; private set; }
    public double Weight_MatchingSignalsConverging { get; private set; }

    // --- Pixel & Scaling limits
    public int MinOutputWidth { get; private set; }
    public int MinOutputHeight { get; private set; }
    public int MaxOutputWidth { get; private set; }
    public int MaxOutputHeight { get; private set; }
    public double MaxUpScaleFactor { get; private set; }
    public double MaxDownScaleFactor { get; private set; }
    public int MinGeneratedImgWidth { get; private set; }
    public int MinGeneratedImgWidthHeight { get; private set; }
    public bool ShouldCenterProducts { get; private set; }
    public double WhiteSpaceMargin { get; private set; }

    // --- Pipeline / job settings 

    /// <summary>Number of pipeline retry attempts for a failed job step.</summary>
    public int JobRetries { get; private set; }

    /// <summary>How long completed jobs are retained in-process before expiry (hours).</summary>
    public int JobRetentionPeriodInHours { get; private set; }

    /// <summary>Maximum number of jobs that may sit in the queue at once.</summary>
    public int MaxQueuedJobs { get; private set; }

    /// <summary>Maximum number of jobs processed concurrently.</summary>
    public int MaxConcurrentJobs { get; private set; }

    // --- Accepted media types
    public IReadOnlyList<string> AcceptedImageExtensions { get; private set; } = [];
    public IReadOnlyList<string> AcceptedExcelExtensions { get; private set; } = [];
    public IReadOnlyList<string> AcceptedZipExtensions { get; private set; } = [];
    public IReadOnlyList<string> AcceptedMediaTypes => [.. AcceptedImageExtensions, .. AcceptedExcelExtensions, .. AcceptedZipExtensions];

    // --- Factory
    public static PrismConfiguration LoadPrismConfig( string configPath ) {
        if (string.IsNullOrWhiteSpace(configPath)) {
            throw new PrismConfigurationException("Prism_Config.json path must not be null or empty.");
        }

        if (!File.Exists(configPath)) {
            throw new PrismConfigurationException($"Prism_Config.json was not found at: {configPath}");
        }

        string rawJson;
        try {
            rawJson = File.ReadAllText(configPath);
        } catch (Exception readException) {
            throw new PrismConfigurationException(
                $"Prism_Config.json could not be read at: {configPath}",
                readException);
        }

        JsonDocument document;
        try {
            document = JsonDocument.Parse(rawJson);
        } catch (JsonException parseException) {
            throw new PrismConfigurationException(
                $"Prism_Config.json is not valid JSON: {parseException.Message}",
                parseException);
        }

        using (document) {
            PrismConfiguration config = ParseAndValidate(document.RootElement, configPath);
            string coreConfigDirectory = Path.GetDirectoryName(configPath) ?? string.Empty;
            ImageNgpValidator.Validate(coreConfigDirectory);
            return config;
        }
    }

    // --- Parsing helpers 
    private static PrismConfiguration ParseAndValidate( JsonElement root, string configPath ) {
        PrismConfiguration config = new();

        config.MaximumRequestBytes = RequireInt64(root, configPath, "Input", "MAXIMUM_REQUEST_SIZE");
        config.MinimumImageCountPerJob = RequireInt32(root, configPath, "Input", "Images", "amount", "min");
        config.MaximumImageCountPerJob = RequireInt32(root, configPath, "Input", "Images", "amount", "max");
        config.MinBytesPerImg = RequireInt64(root, configPath, "Input", "Images", "filesize", "min");
        config.MaxBytesPerImg = RequireInt64(root, configPath, "Input", "Images", "filesize", "max");
        config.MinXLSCount = RequireInt32(root, configPath, "Input", "EXCEL", "amount", "min");
        config.MaxXLSCount = RequireInt32(root, configPath, "Input", "EXCEL", "amount", "max");
        config.MinXLSBytes = RequireInt64(root, configPath, "Input", "EXCEL", "filesize", "min");
        config.MaxXLSBytes = RequireInt64(root, configPath, "Input", "EXCEL", "filesize", "max");
        config.MaxZipCount = RequireInt32(root, configPath, "Input", "ZIP", "amount", "max");
        config.MaxZipBytes = RequireInt64(root, configPath, "Input", "ZIP", "filesize", "max");
        config.MaxNestDepthZip = RequireInt32(root, configPath, "Input", "ZIP", "NestDepth");

        config.ThresholdForInfluentialTags = RequireDouble(root, configPath, "Classification", "Confidence_Threshold");
        config.ThresholdForDiscardingClassificationTags = RequireDouble(root, configPath, "Classification", "Cutoff_Threshold");

        // Deduplication is optional with safe defaults so existing configs keep working.
        config.ShouldDeduplicate = OptionalBool(root, "Classification", "Deduplication", "Enabled") ?? true;
        config.MaxHammingDistance = OptionalInt32(root, "Classification", "Deduplication", "HammingThreshold") ?? 6;
        config.DeduplicationExemptPhenotypes = OptionalStringArray(root, "Classification", "Deduplication", "ExemptPhenotypes") ?? [];

        config.Weight_NumTokens = RequireDouble(root, configPath, "Classification", "Weights", "NumericToken_Weight");
        config.Weight_StringTokens = RequireDouble(root, configPath, "Classification", "Weights", "StringToken_Weight");
        config.Weight_ClassifyingTags = RequireDouble(root, configPath, "Classification", "Weights", "Classification_Weight");
        config.Weight_SemanticRelevance = RequireDouble(root, configPath, "Classification", "Weights", "SemanticalRelevanceWeight");
        config.Weight_MatchingSignalsConverging = RequireDouble(root, configPath, "Classification", "Weights", "CONVERGENCE_WEIGHT");

        config.MinOutputWidth = RequireInt32(root, configPath, "Output", "Images", "Processed", "MINIMUM_SIZE_IN_PIXELS", "width");
        config.MinOutputHeight = RequireInt32(root, configPath, "Output", "Images", "Processed", "MINIMUM_SIZE_IN_PIXELS", "height");
        config.MaxOutputWidth = RequireInt32(root, configPath, "Output", "Images", "Processed", "MAXIMUM_SIZE_IN_PIXELS", "width");
        config.MaxOutputHeight = RequireInt32(root, configPath, "Output", "Images", "Processed", "MAXIMUM_SIZE_IN_PIXELS", "height");
        config.MaxUpScaleFactor = RequireDouble(root, configPath, "Output", "Images", "Resize", "MAXIMUM_UpScale");
        config.MaxDownScaleFactor = RequireDouble(root, configPath, "Output", "Images", "Resize", "MAXIMUM_DownScale");

        config.MinGeneratedImgWidth = RequireInt32(root, configPath, "Generation", "InputImages", "MINIMUM_SIZE_IN_PIXELS", "width");
        config.MinGeneratedImgWidthHeight = RequireInt32(root, configPath, "Generation", "InputImages", "MINIMUM_SIZE_IN_PIXELS", "height");

        config.ShouldCenterProducts = RequireBool(root, configPath, "Transformation", "Positioning", "Center");
        config.WhiteSpaceMargin = RequireDouble(root, configPath, "Transformation", "Positioning", "Margin");

        config.JobRetries = RequireInt32(root, configPath, "Pipeline", "JobRetries");
        config.JobRetentionPeriodInHours = RequireInt32(root, configPath, "Jobs", "JobRetentionPeriodInHours");
        config.MaxQueuedJobs = RequireInt32(root, configPath, "Jobs", "MaxQueuedJobs");
        config.MaxConcurrentJobs = RequireInt32(root, configPath, "Jobs", "MaxConcurrentJobs");

        config.AcceptedImageExtensions = RequireStringArray(root, configPath, "Input", "Images", "extensions");
        config.AcceptedExcelExtensions = RequireStringArray(root, configPath, "Input", "EXCEL", "extensions");
        config.AcceptedZipExtensions = RequireStringArray(root, configPath, "Input", "ZIP", "extensions");

        config.Validate(configPath);
        return config;
    }

    /// <summary>
    /// Validates that all loaded values are within acceptable ranges.
    /// Throws <see cref="PrismConfigurationException"/> on any invalid value.
    /// </summary>
    /// <param name="configPath">Source path used in error messages.</param>
    private void Validate( string configPath ) {
        AssertPositive(MaximumRequestBytes, configPath, "Input.MAXIMUM_REQUEST_SIZE");
        AssertPositive(MinimumImageCountPerJob, configPath, "Input.Images.amount.min");
        AssertPositive(MaximumImageCountPerJob, configPath, "Input.Images.amount.max");
        AssertInRange(MinimumImageCountPerJob, 1, MaximumImageCountPerJob, configPath, "Input.Images.amount.min");
        AssertPositive(MinBytesPerImg, configPath, "Input.Images.filesize.min");
        AssertPositive(MaxBytesPerImg, configPath, "Input.Images.filesize.max");
        AssertPositive(MinXLSCount, configPath, "Input.EXCEL.amount.min");
        AssertPositive(MaxXLSCount, configPath, "Input.EXCEL.amount.max");
        AssertPositive(MaxNestDepthZip, configPath, "Input.ZIP.NestDepth");

        AssertInRange(ThresholdForInfluentialTags, 0.0, 1.0, configPath, "Classification.Confidence_Threshold");
        AssertInRange(ThresholdForDiscardingClassificationTags, 0.0, 1.0, configPath, "Classification.Cutoff_Threshold");

        if (ThresholdForDiscardingClassificationTags > ThresholdForInfluentialTags) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Classification.Cutoff_Threshold ({ThresholdForDiscardingClassificationTags}) must be <= Classification.Confidence_Threshold ({ThresholdForInfluentialTags}).");
        }

        if (MaxHammingDistance < 0) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Classification.Deduplication.HammingThreshold must be >= 0 but was {MaxHammingDistance}.");
        }
        AssertInRange(Weight_NumTokens, 0.0, 1.0, configPath, "Classification.Weights.NumericToken_Weight");
        AssertInRange(Weight_StringTokens, 0.0, 1.0, configPath, "Classification.Weights.StringToken_Weight");
        AssertInRange(Weight_ClassifyingTags, 0.0, 1.0, configPath, "Classification.Weights.Classification_Weight");
        AssertInRange(Weight_SemanticRelevance, 0.0, 1.0, configPath, "Classification.Weights.SemanticalRelevanceWeight");

        AssertPositive(MinOutputWidth, configPath, "Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(MinOutputHeight, configPath, "Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS.height");
        AssertPositive(MaxOutputWidth, configPath, "Output.Images.Processed.MAXIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(MaxOutputHeight, configPath, "Output.Images.Processed.MAXIMUM_SIZE_IN_PIXELS.height");
        AssertPositive(MaxUpScaleFactor, configPath, "Output.Images.Resize.MAXIMUM_UpScale");
        AssertPositive(MaxDownScaleFactor, configPath, "Output.Images.Resize.MAXIMUM_DownScale");
        AssertPositive(MinGeneratedImgWidth, configPath, "Generation.InputImages.MINIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(MinGeneratedImgWidthHeight, configPath, "Generation.InputImages.MINIMUM_SIZE_IN_PIXELS.height");
        AssertInRange(WhiteSpaceMargin, 0.0, 1.0, configPath, "Transformation.Positioning.Margin");

        if (JobRetries < 0) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Pipeline.JobRetries must be >= 0 but was {JobRetries}.");
        }

        if (JobRetentionPeriodInHours <= 0) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Jobs.JobRetentionPeriodInHours must be > 0 but was {JobRetentionPeriodInHours}.");
        }

        AssertPositive(MaxQueuedJobs, configPath, "Jobs.MaxQueuedJobs");
        AssertPositive(MaxConcurrentJobs, configPath, "Jobs.MaxConcurrentJobs");

        if (AcceptedImageExtensions.Count == 0) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Input.Images.extensions must contain at least one entry.");
        }

        if (AcceptedExcelExtensions.Count == 0) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Input.EXCEL.extensions must contain at least one entry.");
        }

        if (AcceptedZipExtensions.Count == 0) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Input.ZIP.extensions must contain at least one entry.");
        }
    }

    // --- JSON navigation helpers 
    private static int RequireInt32( JsonElement root, string configPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt32(out int value)) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required integer field '{string.Join(".", path)}' is missing or not a valid integer.");
        }

        return value;
    }

    private static long RequireInt64( JsonElement root, string configPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt64(out long value)) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required integer field '{string.Join(".", path)}' is missing or not a valid integer.");
        }

        return value;
    }

    private static double RequireDouble( JsonElement root, string configPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetDouble(out double value)) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required numeric field '{string.Join(".", path)}' is missing or not a valid number.");
        }

        return value;
    }

    private static bool RequireBool( JsonElement root, string configPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue
            || (element.Value.ValueKind != JsonValueKind.True && element.Value.ValueKind != JsonValueKind.False)) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required boolean field '{string.Join(".", path)}' is missing or not a valid boolean.");
        }

        return element.Value.GetBoolean();
    }

    private static IReadOnlyList<string> RequireStringArray( JsonElement root, string configPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Array) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required array field '{string.Join(".", path)}' is missing or not an array.");
        }

        List<string> values = [];
        foreach (JsonElement item in element.Value.EnumerateArray()) {
            if (item.ValueKind != JsonValueKind.String) {
                throw new PrismConfigurationException(
                    $"Prism_Config.json at '{configPath}': array field '{string.Join(".", path)}' must contain only strings.");
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    private static bool? OptionalBool( JsonElement root, params string[] path ) {
        JsonElement? element = Navigate(root, path);
        if (!element.HasValue || (element.Value.ValueKind != JsonValueKind.True && element.Value.ValueKind != JsonValueKind.False))
            return null;
        return element.Value.GetBoolean();
    }

    private static int? OptionalInt32( JsonElement root, params string[] path ) {
        JsonElement? element = Navigate(root, path);
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt32(out int value))
            return null;
        return value;
    }

    private static IReadOnlyList<string>? OptionalStringArray( JsonElement root, params string[] path ) {
        JsonElement? element = Navigate(root, path);
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Array)
            return null;

        List<string> values = [];
        foreach (JsonElement item in element.Value.EnumerateArray()) {
            if (item.ValueKind == JsonValueKind.String)
                values.Add(item.GetString()!);
        }
        return values;
    }

    private static JsonElement? Navigate( JsonElement root, string[] path ) {
        JsonElement current = root;

        foreach (string segment in path) {
            if (!current.TryGetProperty(segment, out current)) {
                return null;
            }
        }

        return current;
    }

    // --- Assertion helpers 

    private static void AssertPositive( long value, string configPath, string fieldPath ) {
        if (value <= 0) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be > 0 but was {value}.");
        }
    }

    private static void AssertPositive( int value, string configPath, string fieldPath ) {
        if (value <= 0) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be > 0 but was {value}.");
        }
    }

    private static void AssertPositive( double value, string configPath, string fieldPath ) {
        if (value <= 0.0) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be > 0 but was {value}.");
        }
    }

    private static void AssertInRange( double value, double min, double max, string configPath, string fieldPath ) {
        if (value < min || value > max) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be between {min} and {max} but was {value}.");
        }
    }

    private static void AssertInRange( int value, int min, int max, string configPath, string fieldPath ) {
        if (value < min || value > max) {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be between {min} and {max} but was {value}.");
        }
    }
}