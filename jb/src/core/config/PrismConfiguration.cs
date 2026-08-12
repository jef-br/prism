using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Typed PRISM configuration loaded from Prism_Config.json at startup.
/// Owns all threshold, limit, and queue values used by the pipeline.
/// </summary>
public sealed class PrismConfiguration {

    /// <summary>Config file name; resolve its path with <see cref="ConfigLoader.RequireFile"/>.</summary>
    public const string FileName = "Prism_Config.json";

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

    /// <summary>
    /// Optional per-feature influential-threshold overrides (Classification.Confidence_Thresholds).
    /// A single global threshold penalizes high-cardinality feature groups — a 5-way softmax winner
    /// rarely reaches the confidence a 2-way winner does — so groups can declare their own bar here.
    /// </summary>
    public IReadOnlyDictionary<string, double> InfluentialThresholdsByFeature { get; private set; } =
        new Dictionary<string, double>();

    /// <summary>The influential threshold for <paramref name="feature"/>: its override, or the global default.</summary>
    public double InfluentialThresholdFor(string feature) =>
        this.InfluentialThresholdsByFeature.TryGetValue(feature, out double threshold)
            ? threshold
            : this.ThresholdForInfluentialTags;
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
    public int MinInputSizeInPixels { get; private set; }
    public int MinOutputWidth { get; private set; }
    public int MinOutputHeight { get; private set; }
    public int MaxOutputWidth { get; private set; }
    public int MaxOutputHeight { get; private set; }
    public double MaxUpScaleFactor { get; private set; }

    /// <summary>
    /// Upscale ceiling when the job did not opt into ESRGAN (<c>AllowEsrganUpscale</c> false, the
    /// default): plain Lanczos may enlarge this much before the image is KO'd instead. Lower than
    /// <see cref="MaxUpScaleFactor"/> because interpolation invents no detail — past this the result
    /// is soft rather than merely enlarged.
    /// </summary>
    // `required` + `init` rather than the private-set convention of its siblings: this property is new
    // code, so the no-shadow-defaults rule applies to it even though the rest of this class is legacy
    // debt awaiting a retrofit. `private set` cannot carry `required` (CS9032 — the setter would be less
    // visible than the type), hence `init`.
    public required double MaxLanczosOnlyUpScaleFactor { get; init; }
    public double MaxDownScaleFactor { get; private set; }
    public int MinGeneratedImgWidth { get; private set; }
    public int MinGeneratedImgWidthHeight { get; private set; }

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
    public IReadOnlyList<string> AcceptedMediaTypes => [.. this.AcceptedImageExtensions, .. this.AcceptedExcelExtensions, .. this.AcceptedZipExtensions];

    // --- Model assets (paths relative to the core root; resolved by ModelAssetLocator.Find)
    public string ClipModelDir { get; private set; } = "";
    public string ClipModelFile { get; private set; } = "";
    public string ClipVocabFile { get; private set; } = "";
    public string ClipMergesFile { get; private set; } = "";
    public string UpscaleModelPath { get; private set; } = "";
    public string YoloModelPath { get; private set; } = "";

    // --- Per-model AI toggles (Models.<section>.UseIt)
    /// <summary>
    /// One boolean per ONNX model, sourced from that model's own config section. False means the model is
    /// never loaded and every feature it would have measured keeps its "I don't know" default — analyzers
    /// still run, only the write is gated. `required` + `init` rather than the private-set convention of
    /// the properties above: these are new, so the no-shadow-defaults rule applies — a config missing any
    /// UseIt key throws at load rather than silently picking a side.
    /// </summary>
    public required bool AiClassificationEnabled { get; init; }

    /// <summary>Models.Detection.UseIt — the YOLO26 detector. See <see cref="AiClassificationEnabled"/>.</summary>
    public required bool AiDetectionEnabled { get; init; }

    /// <summary>Models.Upscaling.UseIt — Real-ESRGAN. Off forces the existing Lanczos path.</summary>
    public required bool AiUpscalingEnabled { get; init; }

    /// <summary>Models.Generation.UseIt — the not-yet-built generation backend. Shipped false.</summary>
    public required bool AiGenerationEnabled { get; init; }

    // --- Output det-order policy
    /// <summary>
    /// Output.DET-ORDER-GAPS-ALLOWED. When false (default), each family's det indices are compacted to a
    /// contiguous 0..n-1 range at export time (gaps closed, relative order preserved). When true, det
    /// indices are left exactly as the Order stage assigned them. See PRISM-order-rename.md.
    /// </summary>
    public bool DetOrderGapsAllowed { get; private set; }

    // --- Factory
    public static PrismConfiguration LoadPrismConfig(string cfgPath) {
        if (string.IsNullOrWhiteSpace(cfgPath)) {
            throw new PrismConfigurationException($"{FileName} path must not be null or empty.");
        }

        if (!File.Exists(cfgPath)) {
            throw new PrismConfigurationException($"{FileName} was not found at: {cfgPath}");
        }

        string rawJson;
        try {
            rawJson = File.ReadAllText(cfgPath);
        }
        catch (Exception readException) {
            throw new PrismConfigurationException(
                $"{FileName} could not be read at: {cfgPath}",
                readException);
        }

        JsonDocument document;
        try {
            document = JsonDocument.Parse(rawJson);
        }
        catch (JsonException parseException) {
            throw new PrismConfigurationException(
                $"{FileName} is not valid JSON: {parseException.Message}",
                parseException);
        }

        using (document) {
            PrismConfiguration config = ParseAndValidate(document.RootElement, cfgPath);
            string coreConfigDirectory = Path.GetDirectoryName(cfgPath) ?? string.Empty;
            ImageNgpValidator.Validate(coreConfigDirectory);
            ValidateModelAssets(config);
            return config;
        }
    }

    // Fail fast on missing model assets: the refinement chain needs the YOLO26 detector and Transform
    // needs the Real-ESRGAN upscaler, and a per-image degradation would be silent (T-4110: no fallback
    // upscaler exists). Same resolution order as the CLIP model assets. A model whose UseIt toggle is off
    // is never loaded, so its asset is not required to be present — checking it anyway would make
    // "disable the model" impossible on exactly the hosts that need it (asset missing or known-bad).
    private static void ValidateModelAssets(PrismConfiguration config) {
        if (config.AiDetectionEnabled && ModelAssetLocator.Find(config.YoloModelPath) is null)
            throw new PrismConfigurationException(
                $"YOLO26 ONNX model not found at '{config.YoloModelPath}'. Deploy it next to " +
                "Prism_Config.json, set PRISM_ONNX_MODEL_DIR, or keep the source-tree copy under jb/src/core/.");
        if (config.AiUpscalingEnabled && ModelAssetLocator.Find(config.UpscaleModelPath) is null)
            throw new PrismConfigurationException(
                $"Real-ESRGAN ONNX model not found at '{config.UpscaleModelPath}'. Deploy it next to " +
                "Prism_Config.json, set PRISM_ONNX_MODEL_DIR, or keep the source-tree copy under jb/src/core/.");
    }

    // --- Parsing helpers 
    private static PrismConfiguration ParseAndValidate(JsonElement root, string cfgPath) {
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
            InfluentialThresholdsByFeature = OptionalDoubleMap(root, "Classification", "Confidence_Thresholds")
                ?? new Dictionary<string, double>(),

            // Deduplication is optional with safe defaults so existing configs keep working.
            ShouldDeduplicate = OptionalBool(root, "Classification", "Deduplication", "Enabled") ?? true,
            MaxHammingDistance = OptionalInt32(root, "Classification", "Deduplication", "HammingThreshold") ?? 6,
            DeduplicationExemptPhenotypes = OptionalStringArray(root, "Classification", "Deduplication", "ExemptPhenotypes") ?? [],

            Weight_NumTokens = RequireDouble(root, cfgPath, "Classification", "Weights", "NumericToken_Weight"),
            Weight_StringTokens = RequireDouble(root, cfgPath, "Classification", "Weights", "StringToken_Weight"),
            Weight_ClassifyingTags = RequireDouble(root, cfgPath, "Classification", "Weights", "Classification_Weight"),
            Weight_SemanticRelevance = RequireDouble(root, cfgPath, "Classification", "Weights", "SemanticalRelevanceWeight"),
            Weight_MatchingSignalsConverging = RequireDouble(root, cfgPath, "Classification", "Weights", "CONVERGENCE_WEIGHT"),

            MinInputSizeInPixels = RequireInt32(root, cfgPath, "Input", "Images", "MINIMUM_SIZE_IN_PIXELS", "width"),
            MinOutputWidth = RequireInt32(root, cfgPath, "Output", "Images", "Processed", "MINIMUM_SIZE_IN_PIXELS", "width"),
            MinOutputHeight = RequireInt32(root, cfgPath, "Output", "Images", "Processed", "MINIMUM_SIZE_IN_PIXELS", "height"),
            MaxOutputWidth = RequireInt32(root, cfgPath, "Output", "Images", "Processed", "MAXIMUM_SIZE_IN_PIXELS", "width"),
            MaxOutputHeight = RequireInt32(root, cfgPath, "Output", "Images", "Processed", "MAXIMUM_SIZE_IN_PIXELS", "height"),
            MaxUpScaleFactor = RequireDouble(root, cfgPath, "Output", "Images", "Resize", "MAXIMUM_UpScale"),
            MaxLanczosOnlyUpScaleFactor = RequireDouble(root, cfgPath, "Output", "Images", "Resize", "MAXIMUM_UpScale_LanczosOnly"),
            MaxDownScaleFactor = RequireDouble(root, cfgPath, "Output", "Images", "Resize", "MAXIMUM_DownScale"),

            MinGeneratedImgWidth = RequireInt32(root, cfgPath, "Generation", "InputImages", "MINIMUM_SIZE_IN_PIXELS", "width"),
            MinGeneratedImgWidthHeight = RequireInt32(root, cfgPath, "Generation", "InputImages", "MINIMUM_SIZE_IN_PIXELS", "height"),

            JobRetries = RequireInt32(root, cfgPath, "Pipeline", "JobRetries"),
            JobRetentionPeriodInHours = RequireInt32(root, cfgPath, "Jobs", "JobRetentionPeriodInHours"),
            MaxQueuedJobs = RequireInt32(root, cfgPath, "Jobs", "MaxQueuedJobs"),
            MaxConcurrentJobs = RequireInt32(root, cfgPath, "Jobs", "MaxConcurrentJobs"),

            AcceptedImageExtensions = RequireStringArray(root, cfgPath, "Input", "Images", "extensions"),
            AcceptedExcelExtensions = RequireStringArray(root, cfgPath, "Input", "EXCEL", "extensions"),
            AcceptedZipExtensions = RequireStringArray(root, cfgPath, "Input", "ZIP", "extensions"),

            ClipModelDir = RequireString(root, cfgPath, "Models", "classification", "Dir"),
            ClipModelFile = RequireString(root, cfgPath, "Models", "classification", "Model"),
            ClipVocabFile = RequireString(root, cfgPath, "Models", "classification", "Vocab"),
            ClipMergesFile = RequireString(root, cfgPath, "Models", "classification", "Merges"),
            UpscaleModelPath = RequireString(root, cfgPath, "Models", "Upscaling", "Path"),
            YoloModelPath = RequireString(root, cfgPath, "Models", "Detection", "Path"),

            AiClassificationEnabled = RequireBool(root, cfgPath, "Models", "classification", "UseIt"),
            AiDetectionEnabled = RequireBool(root, cfgPath, "Models", "Detection", "UseIt"),
            AiUpscalingEnabled = RequireBool(root, cfgPath, "Models", "Upscaling", "UseIt"),
            AiGenerationEnabled = RequireBool(root, cfgPath, "Models", "Generation", "UseIt"),

            // Optional with a safe default (false = compact) so existing configs keep working.
            DetOrderGapsAllowed = OptionalBool(root, "Output", "DET-ORDER-GAPS-ALLOWED") ?? false
        };

        config.Validate(cfgPath);
        return config;
    }

    /// <summary>
    /// Validates that all loaded values are within acceptable ranges.
    /// Throws <see cref="PrismConfigurationException"/> on any invalid value.
    /// </summary>
    /// <param name="cfgPath">Source path used in error messages.</param>
    private void Validate(string cfgPath) {
        AssertPositive(this.MaximumRequestBytes, cfgPath, "Input.MAXIMUM_REQUEST_SIZE");
        AssertPositive(this.MinimumImageCountPerJob, cfgPath, "Input.Images.amount.min");
        AssertPositive(this.MaximumImageCountPerJob, cfgPath, "Input.Images.amount.max");
        AssertInRange(this.MinimumImageCountPerJob, 1, this.MaximumImageCountPerJob, cfgPath, "Input.Images.amount.min");
        AssertPositive(this.MinBytesPerImg, cfgPath, "Input.Images.filesize.min");
        AssertPositive(this.MaxBytesPerImg, cfgPath, "Input.Images.filesize.max");
        AssertPositive(this.MinXLSCount, cfgPath, "Input.EXCEL.amount.min");
        AssertPositive(this.MaxXLSCount, cfgPath, "Input.EXCEL.amount.max");
        AssertPositive(this.MaxNestDepthZip, cfgPath, "Input.ZIP.NestDepth");

        AssertInRange(this.ThresholdForInfluentialTags, 0.0, 1.0, cfgPath, "Classification.Confidence_Threshold");
        AssertInRange(this.ThresholdForDiscardingClassificationTags, 0.0, 1.0, cfgPath, "Classification.Cutoff_Threshold");

        if (this.ThresholdForDiscardingClassificationTags > this.ThresholdForInfluentialTags) { throw new PrismConfigurationException($"{FileName} at '{cfgPath}': Classification.Cutoff_Threshold ({this.ThresholdForDiscardingClassificationTags}) must be <= Classification.Confidence_Threshold ({this.ThresholdForInfluentialTags})."); }
        if (this.MaxHammingDistance < 0) { throw new PrismConfigurationException($"{FileName} at '{cfgPath}': Classification.Deduplication.HammingThreshold must be >= 0 but was {this.MaxHammingDistance}."); }

        AssertInRange(this.Weight_NumTokens, 0.0, 1.0, cfgPath, "Classification.Weights.NumericToken_Weight");
        AssertInRange(this.Weight_StringTokens, 0.0, 1.0, cfgPath, "Classification.Weights.StringToken_Weight");
        AssertInRange(this.Weight_ClassifyingTags, 0.0, 1.0, cfgPath, "Classification.Weights.Classification_Weight");
        AssertInRange(this.Weight_SemanticRelevance, 0.0, 1.0, cfgPath, "Classification.Weights.SemanticalRelevanceWeight");
        AssertInRange(this.Weight_MatchingSignalsConverging, 0.0, 1.0, cfgPath, "Classification.Weights.CONVERGENCE_WEIGHT");

        AssertPositive(this.MinInputSizeInPixels, cfgPath, "Input.Images.MINIMUM_SIZE_IN_PIXELS");
        AssertPositive(this.MinOutputWidth, cfgPath, "Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(this.MinOutputHeight, cfgPath, "Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS.height");
        AssertPositive(this.MaxOutputWidth, cfgPath, "Output.Images.Processed.MAXIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(this.MaxOutputHeight, cfgPath, "Output.Images.Processed.MAXIMUM_SIZE_IN_PIXELS.height");
        AssertPositive(this.MaxUpScaleFactor, cfgPath, "Output.Images.Resize.MAXIMUM_UpScale");
        AssertPositive(this.MaxLanczosOnlyUpScaleFactor, cfgPath, "Output.Images.Resize.MAXIMUM_UpScale_LanczosOnly");
        if (this.MaxLanczosOnlyUpScaleFactor > this.MaxUpScaleFactor) { throw new PrismConfigurationException($"{FileName} at '{cfgPath}': Output.Images.Resize.MAXIMUM_UpScale_LanczosOnly ({this.MaxLanczosOnlyUpScaleFactor}) must be <= MAXIMUM_UpScale ({this.MaxUpScaleFactor}) — the cheap path may not be allowed to stretch further than the quality path."); }
        AssertPositive(this.MaxDownScaleFactor, cfgPath, "Output.Images.Resize.MAXIMUM_DownScale");
        AssertPositive(this.MinGeneratedImgWidth, cfgPath, "Generation.InputImages.MINIMUM_SIZE_IN_PIXELS.width");
        AssertPositive(this.MinGeneratedImgWidthHeight, cfgPath, "Generation.InputImages.MINIMUM_SIZE_IN_PIXELS.height");

        if (this.JobRetries < 0) { throw new PrismConfigurationException($"{FileName} at '{cfgPath}': Pipeline.JobRetries must be >= 0 but was {this.JobRetries}."); }
        if (this.JobRetentionPeriodInHours <= 0) throw new PrismConfigurationException($"{FileName} at '{cfgPath}': Jobs.JobRetentionPeriodInHours must be > 0 but was {this.JobRetentionPeriodInHours}.");

        AssertPositive(this.MaxQueuedJobs, cfgPath, "Jobs.MaxQueuedJobs");
        AssertPositive(this.MaxConcurrentJobs, cfgPath, "Jobs.MaxConcurrentJobs");

        if (this.AcceptedImageExtensions.Count == 0) throw new PrismConfigurationException($"{FileName} at '{cfgPath}': Input.Images.extensions must contain at least one entry.");
        if (this.AcceptedExcelExtensions.Count == 0) throw new PrismConfigurationException($"{FileName} at '{cfgPath}': Input.EXCEL.extensions must contain at least one entry.");
        if (this.AcceptedZipExtensions.Count == 0) throw new PrismConfigurationException($"{FileName} at '{cfgPath}': Input.ZIP.extensions must contain at least one entry.");

        AssertNonEmpty(this.ClipModelDir, cfgPath, "Models.classification.Dir");
        AssertNonEmpty(this.ClipModelFile, cfgPath, "Models.classification.Model");
        AssertNonEmpty(this.ClipVocabFile, cfgPath, "Models.classification.Vocab");
        AssertNonEmpty(this.ClipMergesFile, cfgPath, "Models.classification.Merges");
        AssertNonEmpty(this.UpscaleModelPath, cfgPath, "Models.Upscaling.Path");
        AssertNonEmpty(this.YoloModelPath, cfgPath, "Models.Detection.Path");

    }

    // --- JSON navigation helpers 
    private static int RequireInt32(JsonElement root, string cfgPath, params string[] path) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt32(out int val)) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': required integer field '{string.Join(".", path)}' is missing or not a valid integer.");
        }

        return val;
    }

    private static long RequireInt64(JsonElement root, string cfgPath, params string[] path) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt64(out long val)) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': required integer field '{string.Join(".", path)}' is missing or not a valid integer.");
        }

        return val;
    }

    private static double RequireDouble(JsonElement root, string cfgPath, params string[] path) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetDouble(out double val)) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': required numeric field '{string.Join(".", path)}' is missing or not a valid number.");
        }

        return val;
    }

    private static bool RequireBool(JsonElement root, string cfgPath, params string[] path) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || (element.Value.ValueKind != JsonValueKind.True && element.Value.ValueKind != JsonValueKind.False)) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': required boolean field '{string.Join(".", path)}' is missing or not a valid boolean.");
        }

        return element.Value.GetBoolean();
    }

    private static string RequireString(JsonElement root, string cfgPath, params string[] path) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.String) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': required string field '{string.Join(".", path)}' is missing or not a string.");
        }

        return element.Value.GetString()!;
    }

    private static IReadOnlyList<string> RequireStringArray(JsonElement root, string cfgPath, params string[] path) {
        JsonElement? element = Navigate(root, path);

        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Array) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': required array field '{string.Join(".", path)}' is missing or not an array.");
        }

        List<string> values = [];
        foreach (JsonElement item in element.Value.EnumerateArray()) {
            if (item.ValueKind != JsonValueKind.String) {
                throw new PrismConfigurationException($"{FileName} at '{cfgPath}': array field '{string.Join(".", path)}' must contain only strings.");
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    private static bool? OptionalBool(JsonElement root, params string[] path) {
        JsonElement? element = Navigate(root, path);
        if (!element.HasValue || (element.Value.ValueKind != JsonValueKind.True && element.Value.ValueKind != JsonValueKind.False))
            return null;
        return element.Value.GetBoolean();
    }

    private static int? OptionalInt32(JsonElement root, params string[] path) {
        JsonElement? element = Navigate(root, path);
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Number || !element.Value.TryGetInt32(out int val))
            return null;
        return val;
    }

    private static IReadOnlyList<string>? OptionalStringArray(JsonElement root, params string[] path) {
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

    private static IReadOnlyDictionary<string, double>? OptionalDoubleMap(JsonElement root, params string[] path) {
        JsonElement? element = Navigate(root, path);
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object)
            return null;

        Dictionary<string, double> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in element.Value.EnumerateObject()) {
            if (property.Value.ValueKind == JsonValueKind.Number)
                values[property.Name] = property.Value.GetDouble();
        }
        return values;
    }

    private static JsonElement? Navigate(JsonElement root, string[] path) {
        JsonElement current = root;

        foreach (string segment in path) if (!current.TryGetProperty(segment, out current)) return null;

        return current;
    }

    // --- Assertion helpers 

    private static void AssertPositive(long val, string cfgPath, string fieldPath) {
        if (val <= 0) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': field '{fieldPath}' must be > 0 but was {val}.");
        }
    }

    private static void AssertPositive(int val, string cfgPath, string fieldPath) {
        if (val <= 0) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': field '{fieldPath}' must be > 0 but was {val}.");
        }
    }

    private static void AssertPositive(double val, string cfgPath, string fieldPath) {
        if (val <= 0.0) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': field '{fieldPath}' must be > 0 but was {val}.");
        }
    }

    private static void AssertInRange(double val, double min, double max, string cfgPath, string fieldPath) {
        if (val < min || val > max) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': field '{fieldPath}' must be between {min} and {max} but was {val}.");
        }
    }

    private static void AssertInRange(int val, int min, int max, string cfgPath, string fieldPath) {
        if (val < min || val > max) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': field '{fieldPath}' must be between {min} and {max} but was {val}.");
        }
    }

    private static void AssertNonEmpty(string val, string cfgPath, string fieldPath) {
        if (string.IsNullOrWhiteSpace(val)) {
            throw new PrismConfigurationException($"{FileName} at '{cfgPath}': field '{fieldPath}' must be a non-empty string.");
        }
    }
}
