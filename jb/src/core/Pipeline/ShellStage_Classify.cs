/// <summary>
/// Shell delegate for the Classified stage.
/// Deduplicates images by visual hash, extracts ImageFeatures via
/// <see cref="ImageFeatureAnalyzer"/> and optional CLIP, then assigns phenotypes
/// from <c>ImageRoles.json</c> via <see cref="PhenotypeRuleSet"/>.
/// </summary>
internal static class ShellStage_Classify
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
