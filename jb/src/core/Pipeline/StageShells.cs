/// <summary>
/// Shell delegate for the Imported stage.
/// Receives raw input records; normalizes images, unpacks zips, and parses Excel into the IEM.
/// Real implementation lives in <c>Importer.cs</c> and <c>ZipHandler.cs</c>.
/// </summary>
internal static class ImportStageShell
{
    /// <summary>
    /// Runs the Imported stage for a job context.
    /// Delegates all normalization, zip extraction, and IEM construction to <see cref="Importer"/>.
    /// KO records are stored in the context and do not stop the batch.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        string excelConfigPath = LocateExcelConfig();
        ModelBuilder modelBuilder = ModelBuilder.FromConfigFile(excelConfigPath);
        Importer importer = new(configuration, modelBuilder);

        string jobTempRoot = Path.Combine(Path.GetTempPath(), "PRISM");
        ImportStageResult importResult = importer.Run(
            context.JobID,
            context.ImageRecords,
            context.ExcelRecords,
            context.ZipFileRecords,
            jobTempRoot);

        context.ImportResult = importResult;
        context.KoRecordCount += importResult.ImageKoRecords.Count + importResult.ZipKoRecords.Count;

        foreach (ExcelProcessingDiagnostic diagnostic in importResult.ExcelDiagnostics.Where(IsExcelKo))
        {
            context.AddWarning($"Excel KO: {diagnostic.ReasonCode} — {diagnostic.Message}");
        }

        context.MarkStageCompleted(PipelineStageNames.Imported);
    }

    /// <summary>
    /// Locates ExcelConfig.json next to the running assembly using PrismConfigLocator conventions.
    /// </summary>
    private static string LocateExcelConfig()
    {
        string? prismConfigPath = PrismConfigLocator.FindPrismConfigPath();
        if (prismConfigPath is null)
        {
            throw new PrismConfigurationException(
                "Prism_Config.json was not found; cannot locate ExcelConfig.json for the Imported stage.");
        }

        string coreDirectory = Path.GetDirectoryName(prismConfigPath)
            ?? throw new PrismConfigurationException("Could not determine core configuration directory.");

        string excelConfigPath = Path.Combine(coreDirectory, "Excel", "ExcelConfig.json");
        if (!File.Exists(excelConfigPath))
        {
            throw new PrismConfigurationException(
                $"ExcelConfig.json was not found at expected location: {excelConfigPath}");
        }

        return excelConfigPath;
    }

    /// <summary>
    /// Determines whether an Excel diagnostic represents a KO item.
    /// </summary>
    private static bool IsExcelKo(ExcelProcessingDiagnostic diagnostic)
    {
        return diagnostic.Severity == ExcelDiagnosticSeverity.Error;
    }
}

/// <summary>
/// Shell delegate for the Classified stage.
/// Deduplicates images by visual hash, extracts ImageFeatures via
/// <see cref="ImageFeatureAnalyzer"/> and optional CLIP, then assigns phenotypes
/// from <c>ImageRoles.json</c> via <see cref="PhenotypeRuleSet"/>.
/// </summary>
internal static class ClassifyStageShell
{
    /// <summary>
    /// Runs the Classified stage for a job context.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        IReadOnlyList<ImageRecord_INPUT> okImages = context.NormalizedImages
            .Where(r => r.ImportStatus == ImportStatus.Ok && r.NormalizedJpgPath is not null)
            .ToList();

        PhenotypeRuleSet ruleSet = LoadRuleSet();

        using ImageClassifier classifier = new();
        InitializeClassifier(classifier);

        VisualHasher hasher = new();
        IReadOnlyList<DedupGroup> groups = hasher.FindDuplicates(okImages);
        context.DuplicatesRemoved += groups.Sum(g => g.Duplicates.Count);

        foreach (DedupGroup group in groups)
        {
            ProcessCanonical(group.Canonical, ruleSet, classifier, context,
                configuration.ClassificationConfidenceThreshold,
                configuration.ClassificationCutoffThreshold);

            foreach (ImageRecord_INPUT duplicate in group.Duplicates)
                KoDuplicate(duplicate, group.Canonical, context);
        }

        context.MarkStageCompleted(PipelineStageNames.Classified);
    }

    // ─── Per-image processing ────────────────────────────────────────────────

    private static void ProcessCanonical(
        ImageRecord_INPUT source,
        PhenotypeRuleSet ruleSet,
        ImageClassifier classifier,
        PipelineContext context,
        double influentialThreshold,
        double cutoffThreshold)
    {
        ImageRecord_LAMBDA lambda = CreateLambdaRecord(source);

        if (source.NormalizedJpgPath is not null)
        {
            try
            {
                ImageFeatureAnalyzer.Analyze(source.NormalizedJpgPath, lambda.Features);

                if (classifier.IsReady)
                    ApplyClipTags(source.NormalizedJpgPath, classifier, lambda, influentialThreshold, cutoffThreshold);
            }
            catch (Exception ex)
            {
                lambda.IsKo         = true;
                lambda.KoReasonCode = "CLASSIFY_ERROR";
                lambda.KoSafeMessage = $"Feature extraction failed: {ex.Message}";
                context.LambdaRecords.Add(lambda);
                context.KoRecordCount++;
                return;
            }
        }

        string[] candidates = ruleSet.EvaluateCandidates(lambda.Features);
        lambda.CandidatePhenotypes = candidates;
        lambda.SelectedPhenotype   = candidates.Length > 0 ? candidates[0] : null;

        if (lambda.SelectedPhenotype is not null)
            context.PhenotypeAssignedCount++;

        context.LambdaRecords.Add(lambda);
    }

    private static void KoDuplicate(
        ImageRecord_INPUT duplicate,
        ImageRecord_INPUT canonical,
        PipelineContext context)
    {
        ImageRecord_LAMBDA ko = CreateLambdaRecord(duplicate);
        ko.IsKo          = true;
        ko.KoReasonCode  = "VISUAL_DUPLICATE";
        ko.KoSafeMessage = $"Visual duplicate of {Path.GetFileName(canonical.InitialFullName)}";
        context.LambdaRecords.Add(ko);
        context.KoRecordCount++;
    }

    // ─── CLIP tag application ─────────────────────────────────────────────────

    private static void ApplyClipTags(
        string imagePath,
        ImageClassifier classifier,
        ImageRecord_LAMBDA lambda,
        double influentialThreshold,
        double cutoffThreshold)
    {
        ClassificationToken[] allTokens = classifier.ClassifyImage(imagePath, BuildDefaultPrompts());
        if (allTokens.Length == 0) return;

        lambda.Tags = new TagCollection
        {
            Influential = allTokens.Where(t => t.Confidence >= influentialThreshold).ToArray(),
            Trivial     = allTokens.Where(t => t.Confidence >= cutoffThreshold && t.Confidence < influentialThreshold).ToArray()
        };

        // Write top-scoring tokens into the feature snapshot for phenotype matching.
        foreach (ClassificationToken token in lambda.Tags.Influential)
        {
            if (TryParseFeatureToken(token.Label, out string featureId, out string featureValue))
            {
                lambda.Features.Set(featureId, featureValue, token.Confidence, "clip");
            }
        }
    }

    // ─── Config loaders ──────────────────────────────────────────────────────

    private static PhenotypeRuleSet LoadRuleSet()
    {
        string? imageRolesPath = PrismConfigLocator.FindFolderLocalConfig("ImageNGP/ImageRoles.json");
        if (imageRolesPath is null)
            throw new PrismConfigurationException(
                "ImageRoles.json not found. Ensure ImageNGP/ImageRoles.json is present next to Prism_Config.json.");

        return PhenotypeRuleSet.Load(imageRolesPath);
    }

    private static void InitializeClassifier(ImageClassifier classifier)
    {
        string? modelPath  = PrismConfigLocator.FindFolderLocalConfig(
            "Images/Classify/ONNX/clip-vit-b32-uint8/model_uint8.onnx");
        string? vocabPath  = PrismConfigLocator.FindFolderLocalConfig(
            "Images/Classify/ONNX/clip-vit-b32-uint8/vocab.json");
        string? mergesPath = PrismConfigLocator.FindFolderLocalConfig(
            "Images/Classify/ONNX/clip-vit-b32-uint8/merges.txt");

        if (modelPath is not null && vocabPath is not null && mergesPath is not null)
            classifier.Initialize(modelPath, vocabPath, mergesPath);
        // Absent model → classifier.IsReady = false → CLIP skipped gracefully.
    }

    // ─── Record construction ──────────────────────────────────────────────────

    private static ImageRecord_LAMBDA CreateLambdaRecord(ImageRecord_INPUT source)
    {
        return new ImageRecord_LAMBDA
        {
            InitialFullName = source.InitialFullName,
            Width           = source.NormalizedWidth  > 0 ? source.NormalizedWidth  : source.Width,
            Height          = source.NormalizedHeight > 0 ? source.NormalizedHeight : source.Height
        };
    }

    // ─── CLIP prompt catalogue ────────────────────────────────────────────────

    /// <summary>
    /// Maps each natural-language CLIP prompt to the feature ID and value it represents.
    /// Single source of truth: adding a prompt here automatically includes it in
    /// <see cref="BuildDefaultPrompts"/> and makes it parseable by <see cref="TryParseFeatureToken"/>.
    /// </summary>
    private static readonly Dictionary<string, (string FeatureId, string FeatureValue)> PromptFeatureMap = new()
    {
        ["a photo of a person wearing clothing"]                         = ("hero-is-human",    "TRUE"),
        ["a product photo with no person, ghost mannequin or flat lay"]  = ("hero-is-human",    "FALSE"),
        ["a front view of the product"]                                  = ("hero-orientation",  "FRONT"),
        ["a back view of the product"]                                   = ("hero-orientation",  "BACK"),
        ["a side view of the product"]                                   = ("hero-orientation",  "SIDEON"),
        ["a top down view of the product"]                               = ("hero-orientation",  "TOP"),
        ["a three-quarter or diagonal view of the product"]              = ("hero-orientation",  "DIAGONAL"),
        ["a photo showing the full face of the model"]                   = ("head-visible",      "FULL"),
        ["a photo showing a partially visible face"]                     = ("head-visible",      "PARTIAL"),
        ["a photo with no visible face or head"]                         = ("head-visible",      "NONE"),
        ["a photo showing the full body of the model"]                   = ("body-visible",      "full"),
        ["a photo showing three quarters of the body"]                   = ("body-visible",      "three-quarter"),
        ["a photo showing only the upper half of the body"]              = ("body-visible",      "half"),
        ["a photo showing only the bust or chest of the model"]          = ("body-visible",      "bust"),
    };

    /// <summary>
    /// Returns all natural-language CLIP zero-shot prompts derived from <see cref="PromptFeatureMap"/>.
    /// </summary>
    private static string[] BuildDefaultPrompts() => [.. PromptFeatureMap.Keys];

    /// <summary>
    /// Maps a CLIP result label back to its feature ID and value using <see cref="PromptFeatureMap"/>.
    /// </summary>
    private static bool TryParseFeatureToken(
        string label,
        out string featureId,
        out string featureValue)
    {
        if (PromptFeatureMap.TryGetValue(label, out (string FeatureId, string FeatureValue) mapping))
        {
            featureId    = mapping.FeatureId;
            featureValue = mapping.FeatureValue;
            return true;
        }

        featureId    = string.Empty;
        featureValue = string.Empty;
        return false;
    }
}

/// <summary>
/// Shell delegate for the Matched stage.
/// Tokenizes each image and resolves a FamilyID above threshold using the matcher waterfall.
/// Real implementation lives in <c>ImageMatcher.cs</c>.
/// </summary>
internal static class MatchStageShell
{
    /// <summary>
    /// Runs the Matched stage for a job context.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        ImageMatcher.Run(context);
        context.MarkStageCompleted(PipelineStageNames.Matched);
    }
}

/// <summary>
/// Shell delegate for the Ordered stage.
/// Assigns det-order indices per FamilyID using classification labels and filename tokens.
/// Real implementation lives in <c>ImageOrderer.cs</c>.
/// </summary>
internal static class OrderStageShell
{
    /// <summary>
    /// Runs the Ordered stage for a job context.
    /// Delegates to <see cref="ImageOrderer"/> to assign det-slot indices and ordering evidence per FamilyID.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        ImageOrderer.Run(context);
        context.MarkStageCompleted(PipelineStageNames.Ordered);
    }
}

/// <summary>
/// Shell delegate for the Renamed stage.
/// Validates det-slot uniqueness within each matched family and counts renamed images.
/// Real implementation lives in <c>ImageRenamer.cs</c>.
/// </summary>
internal static class RenameStageShell
{
    /// <summary>
    /// Runs the Renamed stage for a job context.
    /// Delegates collision detection and rename counting to <see cref="ImageRenamer"/>.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        ImageRenamer.Run(context);
        context.MarkStageCompleted(PipelineStageNames.Renamed);
    }
}

/// <summary>
/// Shell delegate for the Generated stage.
/// For families below minimum image count, copies the hero image and creates generated variants.
/// Real implementation lives in the generation module.
/// </summary>
internal static class GenerateStageShell
{
    /// <summary>
    /// Runs the Generated stage for a job context.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        ImageGenerator.Run(context);
        context.MarkStageCompleted(PipelineStageNames.Generated);
    }
}

/// <summary>
/// Shell delegate for the Transformed stage.
/// Routes each non-KO image to its appropriate <see cref="IImageTransformation"/> strategy
/// via <see cref="ImageTransformer"/>, then updates per-job counters.
/// </summary>
internal static class TransformStageShell
{
    /// <summary>
    /// Runs the Transformed stage for a job context.
    /// When <c>Parameters.Transform</c> is false, all non-KO images are marked Skipped and the stage completes immediately.
    /// Otherwise each non-KO image is routed through <see cref="ImageTransformer.TransformImage"/>.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        if (!context.Parameters.Transform)
        {
            foreach (ImageRecord_LAMBDA lambda in context.LambdaRecords)
            {
                if (lambda.IsKo) continue;
                lambda.TransformationResult = new ImageTransformationResult
                {
                    Status          = TransformationStatus.Skipped,
                    InputWidth      = lambda.Width,
                    InputHeight     = lambda.Height,
                    SafeSummaryText = "Transform disabled by job parameters."
                };
            }
            context.MarkStageCompleted(PipelineStageNames.Transformed);
            return;
        }

        foreach (ImageRecord_LAMBDA lambda in context.LambdaRecords)
        {
            if (lambda.IsKo) continue;
            ImageTransformer.TransformImage(lambda);
            context.OkTransformedCount++;
        }

        context.MarkStageCompleted(PipelineStageNames.Transformed);
    }
}

/// <summary>
/// Shell delegate for the Exported stage.
/// Packages all output images with manifest.json into the requested output format.
/// Real implementation lives in <c>Exporter.cs</c>.
/// </summary>
internal static class ExportStageShell
{
    /// <summary>
    /// Runs the Exported stage for a job context.
    /// Delegates zip/JSON packaging and manifest construction to <see cref="Exporter"/>.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        Exporter.Run(context, configuration);
        context.MarkStageCompleted(PipelineStageNames.Exported);
    }
}
