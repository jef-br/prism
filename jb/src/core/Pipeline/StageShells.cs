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
    private const float ClipInfluentialThreshold = 0.28f;

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
            ProcessCanonical(group.Canonical, ruleSet, classifier, context);

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
        PipelineContext context)
    {
        ImageRecord_LAMBDA lambda = CreateLambdaRecord(source);

        if (source.NormalizedJpgPath is not null)
        {
            try
            {
                ImageFeatureAnalyzer.Analyze(source.NormalizedJpgPath, lambda.Features);

                if (classifier.IsReady)
                    ApplyClipTags(source.NormalizedJpgPath, classifier, lambda);
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
        ImageRecord_LAMBDA lambda)
    {
        ClassificationToken[] allTokens = classifier.ClassifyImage(imagePath, BuildDefaultPrompts());
        if (allTokens.Length == 0) return;

        lambda.Tags = new TagCollection
        {
            Influential = allTokens.Where(t => t.Confidence >= ClipInfluentialThreshold).ToArray(),
            Trivial     = allTokens.Where(t => t.Confidence <  ClipInfluentialThreshold).ToArray()
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
    /// Default CLIP zero-shot prompts.
    /// Each prompt encodes a feature value pair in the form "featureId=value"
    /// so <see cref="TryParseFeatureToken"/> can map it back to a feature snapshot entry.
    /// </summary>
    private static string[] BuildDefaultPrompts() =>
    [
        "hero-is-human=TRUE",
        "hero-is-human=FALSE",
        "hero-orientation=FRONT",
        "hero-orientation=BACK",
        "hero-orientation=SIDEON",
        "hero-orientation=TOP",
        "hero-orientation=DIAGONAL",
        "head-visible=FULL",
        "head-visible=PARTIAL",
        "head-visible=NONE",
        "body-visible=full",
        "body-visible=three-quarter",
        "body-visible=half",
        "body-visible=bust"
    ];

    private static bool TryParseFeatureToken(
        string label,
        out string featureId,
        out string featureValue)
    {
        int eq = label.IndexOf('=');
        if (eq > 0 && eq < label.Length - 1)
        {
            featureId    = label[..eq];
            featureValue = label[(eq + 1)..];
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
    /// T-420 will replace this body with real ImageMatcher delegation.
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
    /// T-430 will replace this body with real ImageOrderer delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-430: delegate to ImageOrderer.cs
        context.MarkStageCompleted(PipelineStageNames.Ordered);
    }
}

/// <summary>
/// Shell delegate for the Renamed stage.
/// Collapses FamilyID and det-order into the final output filename.
/// </summary>
internal static class RenameStageShell
{
    /// <summary>
    /// Runs the Renamed stage for a job context.
    /// T-440 will replace this body with real rename logic.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-440: apply FamilyID_det# rename to each accepted ImageRecord_LAMBDA
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
    /// T-450 will replace this body with real generation delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-450: delegate to generation module when Parameters.Generation is true
        context.MarkStageCompleted(PipelineStageNames.Generated);
    }
}

/// <summary>
/// Shell delegate for the Transformed stage.
/// Applies visual transformations per ImageNGP state.
/// Real implementation lives in <c>ImageTransformer.cs</c>.
/// </summary>
internal static class TransformStageShell
{
    /// <summary>
    /// Runs the Transformed stage for a job context.
    /// T-460 will replace this body with real ImageTransformer delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-460: delegate to ImageTransformer.cs when Parameters.Transform is true
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
    /// T-470 will replace this body with real Exporter delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-470: delegate to Exporter.cs
        context.MarkStageCompleted(PipelineStageNames.Exported);
    }
}
