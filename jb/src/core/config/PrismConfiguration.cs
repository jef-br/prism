using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Typed PRISM configuration loaded from Prism_Config.json at startup.
/// Owns all threshold, limit, and queue values used by the pipeline.
/// </summary>
public sealed class PrismConfiguration {
    
    private static string pcjson = "Prism_Config.json";

    
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
    public static PrismConfiguration LoadPrismConfig( string cfgPath ) {
        if (string.IsNullOrWhiteSpace(cfgPath)) {
            throw new PrismConfigurationException($"{pcjson} path must not be null or empty.");
        }

        if (!File.Exists(cfgPath)) {
            throw new PrismConfigurationException($"{pcjson} was not found at: {cfgPath}");
        }

        string rawJson;
        try {
            rawJson = File.ReadAllText(cfgPath);
        } catch (Exception readException) {
            throw new PrismConfigurationException(
                $"{pcjson} could not be read at: {cfgPath}",
                readException);
        }

        JsonDocument document;
        try {
            document = JsonDocument.Parse(rawJson);
        } catch (JsonException parseException) {
            throw new PrismConfigurationException(
                $"{pcjson} is not valid JSON: {parseException.Message}",
                parseException);
        }

        using (document) {
            PrismConfiguration config = ParseAndValidate(document.RootElement, cfgPath);
            string coreConfigDirectory = Path.GetDirectoryName(cfgPath) ?? string.Empty;
            ImageNgpValidator.Validate(coreConfigDirectory);
            return config;
        }
    }

    // --- Parsing helpers 
    private static PrismConfiguration ParseAndValidate( JsonElement root, string cfgPath ) {
        PrismConfiguration config = new() {
            MaximumRequestBytes = RequireInt64(root, cfgPath, "Input", "MAXIMUM_REQUEST_SIZE"),
            MinimumImageCountPerJob = RequireInt32(root, cfgPath, "Input", "Images", "amount", "min"),
            MaximumImageCountPerJob = RequireInt32(root, cfgPath, "Input", "Images", "amount", "max"),
            MinBytesPerImg = RequireInt64(root, cfgPath, "Input", "Images", "filesize", "min"),
            MaxBytesPerImg = RequireInt64(root, cfgPath, "Input", "Images", "filesize", "max"),
            MinXLSCount = RequireInt32(root, cfgPath, "Input", "EXCEL", "amount", "min"),
            MaxXLSCount = RequireInt32(root, cfgPath, "Input", "EXCEL", "amount", "max"),
            MinXLSBytes = RequireInt64(root, cfgPath, "Input", "EXCEL", "filesize", "min"),
            MaxXLSBytes = RequireInt64(root, cfgPath, "Input", "EXCEL", "filesize", "max"),
            MaxZipCount = RequireInt32(root, cfgPath, "Input", "ZIP", "amount", "max"),
            MaxZipBytes = RequireInt64(root, cfgPath, "Input", "ZIP", "filesize", "max"),
            MaxNestDepthZip = RequireInt32(root, cfgPath, "Input", "ZIP", "NestDepth"),

            ThresholdForInfluentialTags = RequireDouble(root, cfgPath, "Classification", "Confidence_Threshold"),
            ThresholdForDiscardingClassificationTags = RequireDouble(root, cfgPath, "Classification", "Cutoff_Threshold"),

            // Deduplication is optional with safe defaults so existing configs keep working.
            ShouldDeduplicate = OptionalBool(root, "Classification", "Deduplication", "Enabled") ?? true,
            MaxHammingDistance = OptionalInt32(root, "Classification", "Deduplication", "HammingThreshold") ?? 6,
            DeduplicationExemptPhenotypes = OptionalStringArray(root, "Classification", "Deduplication", "ExemptPhenotypes") ?? [],

            Weight_NumTokens = RequireDouble(root, cfgPath, "Classification", "Weights", "NumericToken_Weight"),
            Weight_StringTokens = RequireDouble(root, cfgPath, "Classification", "Weights", "StringToken_Weight"),
            Weight_ClassifyingTags = RequireDouble(root, cfgPath, "Classification", "Weights", "Classification_Weight"),
            Weight_SemanticRelevance = RequireDouble(root, cfgPath, "Classification", "Weights", "SemanticalRelevanceWeight"),
            Weight_MatchingSignalsConverging = RequireDouble(root, cfgPath, "Classification", "Weights", "CONVERGENCE_WEIGHT"),

            MinOutputWidth = RequireInt32(root, cfgPath, "Output", "Images", "Processed", "MINIMUM_SIZE_IN_PIXELS", "width"),
            MinOutputHeight = RequireInt32(root, cfgPath, "Output", "Images", "Processed", "MINIMUM_SIZE_IN_PIXELS", "height"),
            MaxOutputWidth = RequireInt32(root, cfgPath, "Output", "Images", "Processed", "MAXIMUM_SIZE_IN_PIXELS", "width"),
            MaxOutputHeight = RequireInt32(root, cfgPath, "Output", "Images", "Processed", "MAXIMUM_SIZE_IN_PIXELS", "height"),
            MaxUpScaleFactor = RequireDouble(root, cfgPath, "Output", "Images", "Resize", "MAXIMUM_UpScale"),
            MaxDownScaleFactor = RequireDouble(root, cfgPath, "Output", "Images", "Resize", "MAXIMUM_DownScale"),

            MinGeneratedImgWidth = RequireInt32(root, cfgPath, "Generation", "InputImages", "MINIMUM_SIZE_IN_PIXELS", "width"),
            MinGeneratedImgWidthHeight = RequireInt32(root, cfgPath, "Generation", "InputImages", "MINIMUM_SIZE_IN_PIXELS", "height"),

            ShouldCenterProducts = RequireBool(root, cfgPath, "Transformation", "Positioning", "Center"),
            WhiteSpaceMargin = RequireDouble(root, cfgPath, "Transformation", "Positioning", "Margin"),

            JobRetries = RequireInt32(root, cfgPath, "Pipeline", "JobRetries"),
            JobRetentionPeriodInHours = RequireInt32(root, cfgPath, "Jobs", "JobRetentionPeriodInHours"),
            MaxQueuedJobs = RequireInt32(root, cfgPath, "Jobs", "MaxQueuedJobs"),
            MaxConcurrentJobs = RequireInt32(root, cfgPath, "Jobs", "MaxConcurrentJobs"),

            AcceptedImageExtensions = RequireStringArray(root, cfgPath, "Input", "Images", "extensions"),
            AcceptedExcelExtensions = RequireStringArray(root, cfgPath, "Input", "EXCEL", "extensions"),
            AcceptedZipExtensions = RequireStringArray(root, cfgPath, "Input", "ZIP", "extensions")
        };

        config.Validate(cfgPath);
        return config;
    }

    /// <summary>
    /// Validates that all loaded values are within acceptable ranges.
    /// Throws <see cref="PrismConfigurationException"/> on any invalid value.
    /// </summary>
    /// <param name="cfgPath">Source path used in error messages.</param>
    private void Validate( string cfgPath ) {
        AssertPositive(MaximumRequestBytes, cfgPath, "Input.MAXIMUM_REQUEST_SIZE");
        AssertPositive(MinimumImageCountPerJob, cfgPath, "Input.Images.amount.min");
        AssertPositive(MaximumImageCountPerJob, cfgPath, "Input.Images.amount.max");
        AssertInRange(MinimumImageCountPerJob, 1, MaximumImageCountPerJob, cfgPath, "Input.Images.amount.min");
        AssertPositive(MinBytesPerImg, cfgPath, "Input.Images.filesize.min");
        AssertPositive(MaxBytesPerImg, cfgPath, "Input.Images.filesize.max");
        AssertPositive(MinXLSCount, cfgPath, "Input.EXCEL.amount.min");
        AssertPositive(MaxXLSCount, cfgPath, "Input.EXCEL.amount.max");
        AssertPositive(MaxNestDepthZip, cfgPath, "Input.ZIP.NestDepth");

        AssertInRange(ThresholdForInfluentialTags, 0.0, 1.0, cfgPath, "Classification.Confidence_Threshold");
        AssertInRange(ThresholdForDiscardingClassificationTags, 0.0, 1.0, cfgPath, "Classification.Cutoff_Threshold");

        if (ThresholdForDiscardingClassificationTags > ThresholdForInfluentialTags) {throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': Classification.Cutoff_Threshold ({ThresholdForDiscardingClassificationTags}) must be <= Classification.Confidence_Threshold ({ThresholdForInfluentialTags}).");}
        if (MaxHammingDistance < 0) {throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': Classification.Deduplication.HammingThreshold must be >= 0 but was {MaxHammingDistance}.");}

        AssertInRange(Weight_NumTokens, 0.0, 1.0, cfgPath, "Classification.Weights.NumericToken_Weight");
        AssertInRange(Weight_StringTokens, 0.0, 1.0, cfgPath, "Classification.Weights.StringToken_Weight");
        AssertInRange(Weight_ClassifyingTags, 0.0, 1.0, cfgPath, "Classification.Weights.Classification_Weight");
        AssertInRange(Weight_SemanticRelevance, 0.0, 1.0, cfgPath, "Classification.Weights.SemanticalRelevanceWeight");
        AssertInRange(Weight_MatchingSignalsConverging, 0.0, 1.0, cfgPath, "Classification.Weights.CONVERGENCE_WEIGHT");

        AssertPositive(MinOutputWidth, cfgPath, "Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(MinOutputHeight, cfgPath, "Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS.height");
        AssertPositive(MaxOutputWidth, cfgPath, "Output.Images.Processed.MAXIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(MaxOutputHeight, cfgPath, "Output.Images.Processed.MAXIMUM_SIZE_IN_PIXELS.height");
        AssertPositive(MaxUpScaleFactor, cfgPath, "Output.Images.Resize.MAXIMUM_UpScale");
        AssertPositive(MaxDownScaleFactor, cfgPath, "Output.Images.Resize.MAXIMUM_DownScale");
        AssertPositive(MinGeneratedImgWidth, cfgPath, "Generation.InputImages.MINIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(MinGeneratedImgWidthHeight, cfgPath, "Generation.InputImages.MINIMUM_SIZE_IN_PIXELS.height");
        AssertInRange(WhiteSpaceMargin, 0.0, 1.0, cfgPath, "Transformation.Positioning.Margin");

        if (JobRetries < 0) {throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': Pipeline.JobRetries must be >= 0 but was {JobRetries}.");}
        if (JobRetentionPeriodInHours <= 0) throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': Jobs.JobRetentionPeriodInHours must be > 0 but was {JobRetentionPeriodInHours}.");

        AssertPositive(MaxQueuedJobs, cfgPath, "Jobs.MaxQueuedJobs");
        AssertPositive(MaxConcurrentJobs, cfgPath, "Jobs.MaxConcurrentJobs");

        if (AcceptedImageExtensions.Count == 0) throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': Input.Images.extensions must contain at least one entry.");
        if (AcceptedExcelExtensions.Count == 0) throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': Input.EXCEL.extensions must contain at least one entry.");
        if (AcceptedZipExtensions.Count == 0) throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': Input.ZIP.extensions must contain at least one entry.");
        
    }

    // --- JSON navigation helpers 
    private static int RequireInt32( JsonElement root, string cfgPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt32(out int val)) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': required integer field '{string.Join(".", path)}' is missing or not a valid integer.");
        }

        return val;
    }

    private static long RequireInt64( JsonElement root, string cfgPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt64(out long val)) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': required integer field '{string.Join(".", path)}' is missing or not a valid integer.");
        }

        return val;
    }

    private static double RequireDouble( JsonElement root, string cfgPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetDouble(out double val)) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': required numeric field '{string.Join(".", path)}' is missing or not a valid number.");
        }

        return val;
    }

    private static bool RequireBool( JsonElement root, string cfgPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || (element.Value.ValueKind != JsonValueKind.True && element.Value.ValueKind != JsonValueKind.False)) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': required boolean field '{string.Join(".", path)}' is missing or not a valid boolean.");
        }

        return element.Value.GetBoolean();
    }

    private static IReadOnlyList<string> RequireStringArray( JsonElement root, string cfgPath, params string[] path ) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Array) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': required array field '{string.Join(".", path)}' is missing or not an array.");
        }

        List<string> values = [];
        foreach (JsonElement item in element.Value.EnumerateArray()) {
            if (item.ValueKind != JsonValueKind.String) {
                throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': array field '{string.Join(".", path)}' must contain only strings.");
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
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt32(out int val))
            return null;
        return val;
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

        foreach (string segment in path) if (!current.TryGetProperty(segment, out current)) return null;

        return current;
    }

    // --- Assertion helpers 

    private static void AssertPositive( long val, string cfgPath, string fieldPath ) {
        if (val <= 0) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': field '{fieldPath}' must be > 0 but was {val}.");
        }
    }

    private static void AssertPositive( int val, string cfgPath, string fieldPath ) {
        if (val <= 0) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': field '{fieldPath}' must be > 0 but was {val}.");
        }
    }

    private static void AssertPositive( double val, string cfgPath, string fieldPath ) {
        if (val <= 0.0) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': field '{fieldPath}' must be > 0 but was {val}.");
        }
    }

    private static void AssertInRange( double val, double min, double max, string cfgPath, string fieldPath ) {
        if (val < min || val > max) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': field '{fieldPath}' must be between {min} and {max} but was {val}.");
        }
    }

    private static void AssertInRange( int val, int min, int max, string cfgPath, string fieldPath ) {
        if (val < min || val > max) {
            throw new PrismConfigurationException($"{pcjson} at '{cfgPath}': field '{fieldPath}' must be between {min} and {max} but was {val}.");
        }
    }
}
