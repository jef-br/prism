using System.Text.Json;

/// <summary>
/// Typed PRISM configuration loaded from Prism_Config.json at startup.
/// Owns all threshold, limit, and queue values used by the pipeline.
/// </summary>
public sealed class PrismConfiguration
{
    // -------------------------------------------------------------------------
    // Input limits
    // -------------------------------------------------------------------------

    /// <summary>Maximum accepted request size in bytes.</summary>
    public long MaximumRequestBytes { get; private set; }

    /// <summary>Minimum accepted image count per job.</summary>
    public int MinimumImageCount { get; private set; }

    /// <summary>Maximum accepted image count per job.</summary>
    public int MaximumImageCount { get; private set; }

    /// <summary>Minimum accepted image file size in bytes.</summary>
    public long MinimumImageBytes { get; private set; }

    /// <summary>Maximum accepted image file size in bytes.</summary>
    public long MaximumImageBytes { get; private set; }

    /// <summary>Minimum accepted Excel file count per job.</summary>
    public int MinimumExcelCount { get; private set; }

    /// <summary>Maximum accepted Excel file count per job.</summary>
    public int MaximumExcelCount { get; private set; }

    /// <summary>Minimum accepted Excel file size in bytes.</summary>
    public long MinimumExcelBytes { get; private set; }

    /// <summary>Maximum accepted Excel file size in bytes.</summary>
    public long MaximumExcelBytes { get; private set; }

    /// <summary>Maximum accepted zip file count per job.</summary>
    public int MaximumZipCount { get; private set; }

    /// <summary>Maximum accepted zip file size in bytes.</summary>
    public long MaximumZipBytes { get; private set; }

    /// <summary>Maximum nesting depth inside a zip archive.</summary>
    public int ZipMaxNestDepth { get; private set; }

    // -------------------------------------------------------------------------
    // Classification thresholds
    // -------------------------------------------------------------------------

    /// <summary>Confidence threshold above which a classification tag is influential.</summary>
    public double ClassificationConfidenceThreshold { get; private set; }

    /// <summary>Minimum confidence below which a classification tag is discarded.</summary>
    public double ClassificationCutoffThreshold { get; private set; }

    // -------------------------------------------------------------------------
    // Matching weights
    // -------------------------------------------------------------------------

    /// <summary>Weight applied to numeric token matcher score (0–1).</summary>
    public double NumericTokenWeight { get; private set; }

    /// <summary>Weight applied to string token matcher score (0–1).</summary>
    public double StringTokenWeight { get; private set; }

    /// <summary>Weight applied to classification label matcher score (0–1).</summary>
    public double ClassificationWeight { get; private set; }

    /// <summary>Weight applied to semantic relevance matcher score (0–1).</summary>
    public double SemanticRelevanceWeight { get; private set; }

    /// <summary>Convergence weight used when all matcher signals agree.</summary>
    public double ConvergenceWeight { get; private set; }

    // -------------------------------------------------------------------------
    // Output pixel limits
    // -------------------------------------------------------------------------

    /// <summary>Minimum output image width in pixels.</summary>
    public int MinimumOutputWidth { get; private set; }

    /// <summary>Minimum output image height in pixels.</summary>
    public int MinimumOutputHeight { get; private set; }

    /// <summary>Maximum output image width in pixels.</summary>
    public int MaximumOutputWidth { get; private set; }

    /// <summary>Maximum output image height in pixels.</summary>
    public int MaximumOutputHeight { get; private set; }

    /// <summary>Maximum allowed upscale factor during resize.</summary>
    public double MaximumUpScaleFactor { get; private set; }

    /// <summary>Maximum allowed downscale factor during resize.</summary>
    public double MaximumDownScaleFactor { get; private set; }

    // -------------------------------------------------------------------------
    // Generation limits
    // -------------------------------------------------------------------------

    /// <summary>Minimum input image width required for generation.</summary>
    public int MinimumGenerationInputWidth { get; private set; }

    /// <summary>Minimum input image height required for generation.</summary>
    public int MinimumGenerationInputHeight { get; private set; }

    // -------------------------------------------------------------------------
    // Transformation settings
    // -------------------------------------------------------------------------

    /// <summary>Whether processed images should be centered on canvas.</summary>
    public bool TransformCenter { get; private set; }

    /// <summary>Proportional margin applied on each axis during transformation.</summary>
    public double TransformMargin { get; private set; }

    /// <summary>Whether margin is applied on both axes.</summary>
    public bool TransformBothAxis { get; private set; }

    /// <summary>Target coverage ratio when cropping.</summary>
    public double CropCoverage { get; private set; }

    // -------------------------------------------------------------------------
    // Pipeline / job settings
    // -------------------------------------------------------------------------

    /// <summary>Number of pipeline retry attempts for a failed job step.</summary>
    public int JobRetries { get; private set; }

    /// <summary>How long completed jobs are retained in-process before expiry (hours).</summary>
    public int JobRetentionPeriodInHours { get; private set; }

    /// <summary>Maximum number of jobs that may sit in the queue at once.</summary>
    public int MaxQueuedJobs { get; private set; }

    /// <summary>Maximum number of jobs processed concurrently.</summary>
    public int MaxConcurrentJobs { get; private set; }

    // -------------------------------------------------------------------------
    // Accepted media types
    // -------------------------------------------------------------------------

    /// <summary>File extensions accepted as input (images, Excel, zip).</summary>
    public IReadOnlyList<string> AcceptedMediaTypes { get; private set; } = [];

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads PRISM configuration from Prism_Config.json.
    /// Fails fast with <see cref="PrismConfigurationException"/> if the file is missing, unreadable, or invalid.
    /// </summary>
    /// <param name="configPath">Absolute path to Prism_Config.json.</param>
    /// <returns>A validated <see cref="PrismConfiguration"/> instance.</returns>
    public static PrismConfiguration Load(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new PrismConfigurationException("Prism_Config.json path must not be null or empty.");
        }

        if (!File.Exists(configPath))
        {
            throw new PrismConfigurationException($"Prism_Config.json was not found at: {configPath}");
        }

        string rawJson;
        try
        {
            rawJson = File.ReadAllText(configPath);
        }
        catch (Exception readException)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json could not be read at: {configPath}",
                readException);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawJson);
        }
        catch (JsonException parseException)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json is not valid JSON: {parseException.Message}",
                parseException);
        }

        using (document)
        {
            PrismConfiguration config = ParseAndValidate(document.RootElement, configPath);
            string coreConfigDirectory = Path.GetDirectoryName(configPath) ?? string.Empty;
            ImageNgpValidator.Validate(coreConfigDirectory);
            return config;
        }
    }

    // -------------------------------------------------------------------------
    // Parsing helpers
    // -------------------------------------------------------------------------

    private static PrismConfiguration ParseAndValidate(JsonElement root, string configPath)
    {
        PrismConfiguration config = new();

        config.MaximumRequestBytes   = RequireInt64(root, configPath, "Input", "MAXIMUM_REQUEST_SIZE");
        config.MinimumImageCount     = RequireInt32(root, configPath, "Input", "Images", "amount", "min");
        config.MaximumImageCount     = RequireInt32(root, configPath, "Input", "Images", "amount", "max");
        config.MinimumImageBytes     = RequireInt64(root, configPath, "Input", "Images", "filesize", "min");
        config.MaximumImageBytes     = RequireInt64(root, configPath, "Input", "Images", "filesize", "max");
        config.MinimumExcelCount     = RequireInt32(root, configPath, "Input", "EXCEL", "amount", "min");
        config.MaximumExcelCount     = RequireInt32(root, configPath, "Input", "EXCEL", "amount", "max");
        config.MinimumExcelBytes     = RequireInt64(root, configPath, "Input", "EXCEL", "filesize", "min");
        config.MaximumExcelBytes     = RequireInt64(root, configPath, "Input", "EXCEL", "filesize", "max");
        config.MaximumZipCount       = RequireInt32(root, configPath, "Input", "ZIP", "amount", "max");
        config.MaximumZipBytes       = RequireInt64(root, configPath, "Input", "ZIP", "filesize", "max");
        config.ZipMaxNestDepth       = RequireInt32(root, configPath, "Input", "ZIP", "NestDepth");

        config.ClassificationConfidenceThreshold = RequireDouble(root, configPath, "Classification", "Confidence_Threshold");
        config.ClassificationCutoffThreshold     = RequireDouble(root, configPath, "Classification", "Cutoff_Threshold");

        config.NumericTokenWeight      = RequireDouble(root, configPath, "Classification", "Weights", "NumericToken_Weight");
        config.StringTokenWeight       = RequireDouble(root, configPath, "Classification", "Weights", "StringToken_Weight");
        config.ClassificationWeight    = RequireDouble(root, configPath, "Classification", "Weights", "Classification_Weight");
        config.SemanticRelevanceWeight = RequireDouble(root, configPath, "Classification", "Weights", "SemanticalRelevanceWeight");
        config.ConvergenceWeight       = RequireDouble(root, configPath, "Classification", "Weights", "CONVERGENCE_WEIGHT");

        config.MinimumOutputWidth   = RequireInt32(root, configPath, "Output", "Images", "Processed", "MINIMUM_SIZE_IN_PIXELS", "width");
        config.MinimumOutputHeight  = RequireInt32(root, configPath, "Output", "Images", "Processed", "MINIMUM_SIZE_IN_PIXELS", "height");
        config.MaximumOutputWidth   = RequireInt32(root, configPath, "Output", "Images", "Processed", "MAXIMUM_SIZE_IN_PIXELS", "width");
        config.MaximumOutputHeight  = RequireInt32(root, configPath, "Output", "Images", "Processed", "MAXIMUM_SIZE_IN_PIXELS", "height");
        config.MaximumUpScaleFactor   = RequireDouble(root, configPath, "Output", "Images", "Resize", "MAXIMUM_UpScale");
        config.MaximumDownScaleFactor = RequireDouble(root, configPath, "Output", "Images", "Resize", "MAXIMUM_DownScale");

        config.MinimumGenerationInputWidth  = RequireInt32(root, configPath, "Generation", "InputImages", "MINIMUM_SIZE_IN_PIXELS", "width");
        config.MinimumGenerationInputHeight = RequireInt32(root, configPath, "Generation", "InputImages", "MINIMUM_SIZE_IN_PIXELS", "height");

        config.TransformCenter   = RequireBool(root, configPath, "Transformation", "Positioning", "Center");
        config.TransformMargin   = RequireDouble(root, configPath, "Transformation", "Positioning", "Margin");
        config.TransformBothAxis = RequireBool(root, configPath, "Transformation", "Positioning", "BothAxis");
        config.CropCoverage      = RequireDouble(root, configPath, "Transformation", "Cropping", "Coverage");

        config.JobRetries                = RequireInt32(root, configPath, "Pipeline", "JobRetries");
        config.JobRetentionPeriodInHours = RequireInt32(root, configPath, "Jobs", "JobRetentionPeriodInHours");
        config.MaxQueuedJobs             = RequireInt32(root, configPath, "Jobs", "MaxQueuedJobs");
        config.MaxConcurrentJobs         = RequireInt32(root, configPath, "Jobs", "MaxConcurrentJobs");

        config.AcceptedMediaTypes = RequireStringArray(root, configPath, "Input", "AcceptedMediaTypes");

        config.Validate(configPath);
        return config;
    }

    /// <summary>
    /// Validates that all loaded values are within acceptable ranges.
    /// Throws <see cref="PrismConfigurationException"/> on any invalid value.
    /// </summary>
    /// <param name="configPath">Source path used in error messages.</param>
    private void Validate(string configPath)
    {
        AssertPositive(MaximumRequestBytes,        configPath, "Input.MAXIMUM_REQUEST_SIZE");
        AssertPositive(MinimumImageCount,           configPath, "Input.Images.amount.min");
        AssertPositive(MaximumImageCount,           configPath, "Input.Images.amount.max");
        AssertInRange(MinimumImageCount, 1, MaximumImageCount, configPath, "Input.Images.amount.min");
        AssertPositive(MinimumImageBytes,           configPath, "Input.Images.filesize.min");
        AssertPositive(MaximumImageBytes,           configPath, "Input.Images.filesize.max");
        AssertPositive(MinimumExcelCount,           configPath, "Input.EXCEL.amount.min");
        AssertPositive(MaximumExcelCount,           configPath, "Input.EXCEL.amount.max");
        AssertPositive(ZipMaxNestDepth,             configPath, "Input.ZIP.NestDepth");

        AssertInRange(ClassificationConfidenceThreshold, 0.0, 1.0, configPath, "Classification.Confidence_Threshold");
        AssertInRange(ClassificationCutoffThreshold,     0.0, 1.0, configPath, "Classification.Cutoff_Threshold");
        AssertInRange(NumericTokenWeight,     0.0, 1.0, configPath, "Classification.Weights.NumericToken_Weight");
        AssertInRange(StringTokenWeight,      0.0, 1.0, configPath, "Classification.Weights.StringToken_Weight");
        AssertInRange(ClassificationWeight,   0.0, 1.0, configPath, "Classification.Weights.Classification_Weight");
        AssertInRange(SemanticRelevanceWeight, 0.0, 1.0, configPath, "Classification.Weights.SemanticalRelevanceWeight");

        AssertPositive(MinimumOutputWidth,          configPath, "Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(MinimumOutputHeight,         configPath, "Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS.height");
        AssertPositive(MaximumOutputWidth,          configPath, "Output.Images.Processed.MAXIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(MaximumOutputHeight,         configPath, "Output.Images.Processed.MAXIMUM_SIZE_IN_PIXELS.height");
        AssertPositive(MaximumUpScaleFactor,        configPath, "Output.Images.Resize.MAXIMUM_UpScale");
        AssertPositive(MaximumDownScaleFactor,      configPath, "Output.Images.Resize.MAXIMUM_DownScale");
        AssertPositive(MinimumGenerationInputWidth,  configPath, "Generation.InputImages.MINIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(MinimumGenerationInputHeight, configPath, "Generation.InputImages.MINIMUM_SIZE_IN_PIXELS.height");

        AssertInRange(TransformMargin, 0.0, 1.0, configPath, "Transformation.Positioning.Margin");
        AssertInRange(CropCoverage,    0.0, 1.0, configPath, "Transformation.Cropping.Coverage");

        if (JobRetries < 0)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Pipeline.JobRetries must be >= 0 but was {JobRetries}.");
        }

        if (JobRetentionPeriodInHours <= 0)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Jobs.JobRetentionPeriodInHours must be > 0 but was {JobRetentionPeriodInHours}.");
        }

        AssertPositive(MaxQueuedJobs,     configPath, "Jobs.MaxQueuedJobs");
        AssertPositive(MaxConcurrentJobs, configPath, "Jobs.MaxConcurrentJobs");

        if (AcceptedMediaTypes.Count == 0)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': Input.AcceptedMediaTypes must contain at least one entry.");
        }
    }

    // -------------------------------------------------------------------------
    // JSON navigation helpers
    // -------------------------------------------------------------------------

    private static int RequireInt32(JsonElement root, string configPath, params string[] path)
    {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt32(out int value))
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required integer field '{string.Join(".", path)}' is missing or not a valid integer.");
        }

        return value;
    }

    private static long RequireInt64(JsonElement root, string configPath, params string[] path)
    {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt64(out long value))
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required integer field '{string.Join(".", path)}' is missing or not a valid integer.");
        }

        return value;
    }

    private static double RequireDouble(JsonElement root, string configPath, params string[] path)
    {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetDouble(out double value))
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required numeric field '{string.Join(".", path)}' is missing or not a valid number.");
        }

        return value;
    }

    private static bool RequireBool(JsonElement root, string configPath, params string[] path)
    {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue
            || (element.Value.ValueKind != JsonValueKind.True && element.Value.ValueKind != JsonValueKind.False))
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required boolean field '{string.Join(".", path)}' is missing or not a valid boolean.");
        }

        return element.Value.GetBoolean();
    }

    private static IReadOnlyList<string> RequireStringArray(JsonElement root, string configPath, params string[] path)
    {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Array)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': required array field '{string.Join(".", path)}' is missing or not an array.");
        }

        List<string> values = [];
        foreach (JsonElement item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new PrismConfigurationException(
                    $"Prism_Config.json at '{configPath}': array field '{string.Join(".", path)}' must contain only strings.");
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    private static JsonElement? Navigate(JsonElement root, string[] path)
    {
        JsonElement current = root;

        foreach (string segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    // -------------------------------------------------------------------------
    // Assertion helpers
    // -------------------------------------------------------------------------

    private static void AssertPositive(long value, string configPath, string fieldPath)
    {
        if (value <= 0)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be > 0 but was {value}.");
        }
    }

    private static void AssertPositive(int value, string configPath, string fieldPath)
    {
        if (value <= 0)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be > 0 but was {value}.");
        }
    }

    private static void AssertPositive(double value, string configPath, string fieldPath)
    {
        if (value <= 0.0)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be > 0 but was {value}.");
        }
    }

    private static void AssertInRange(double value, double min, double max, string configPath, string fieldPath)
    {
        if (value < min || value > max)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be between {min} and {max} but was {value}.");
        }
    }

    private static void AssertInRange(int value, int min, int max, string configPath, string fieldPath)
    {
        if (value < min || value > max)
        {
            throw new PrismConfigurationException(
                $"Prism_Config.json at '{configPath}': field '{fieldPath}' must be between {min} and {max} but was {value}.");
        }
    }
}
