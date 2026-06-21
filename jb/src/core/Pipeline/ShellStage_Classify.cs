/// <summary>
/// Shell delegate for the Classified stage.
/// Deduplicates images by visual hash, extracts ImageFeatures via
/// <see cref="ImageFeatureAnalyzer"/> and optional CLIP, then assigns phenotypes
/// from <c>ImageRoles.json</c> via <see cref="PhenotypeRuleSet"/>.
/// </summary>
internal static class ShellStage_Classify {
    /// <summary>
    /// Runs the Classified stage for a job context.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run( PipelineContext context, PrismConfiguration configuration ) {
        IReadOnlyList<ImageRecord_INPUT> okImages = context.NormalizedImages
            .Where(r => r.ImportStatus == ImportStatus.Ok && r.NormalizedJpgPath is not null)
            .ToList();

        PhenotypeRuleSet ruleSet = LoadRuleSet();
        ClipPromptCatalog promptCatalog = LoadPromptCatalog();

        using ImageClassifier classifier = new();
        InitializeClassifier(classifier);

        // Classify every image first so deduplication, which runs afterwards, can use the assigned
        // phenotype to exempt illustrations, technical drawings, and labels.
        Dictionary<ImageRecord_INPUT, ImageRecord_LAMBDA> lambdaByImage = new();
        foreach (ImageRecord_INPUT image in okImages) {
            ImageRecord_LAMBDA IRLambda = ClassifyImage(image, ruleSet, classifier, promptCatalog, context,
                configuration.ThresholdForInfluentialTags,
                configuration.ThresholdForDiscardingClassificationTags);
            lambdaByImage[image] = IRLambda;
            context.LambdaRecords.Add(IRLambda);
        }

        // Visual deduplication is a configurable post-classification option (Classification.Deduplication).
        if (configuration.ShouldDeduplicate)
            DeduplicateAfterClassification(okImages, lambdaByImage, configuration, context);

        context.MarkStageCompleted(PipelineStageNames.Classified);
    }

    //--- Per-image classification

    private static ImageRecord_LAMBDA ClassifyImage(
        ImageRecord_INPUT source,
        PhenotypeRuleSet ruleSet,
        ImageClassifier classifier,
        ClipPromptCatalog promptCatalog,
        PipelineContext context,
        double influentialThreshold,
        double cutoffThreshold ) {
        ImageRecord_LAMBDA IRLambda = CreateLambdaRecord(source);

        if (source.NormalizedJpgPath is not null) {
            try {
                ImageFeatureAnalyzer.Analyze(source.NormalizedJpgPath, IRLambda.Features);

                if (classifier.IsReady)
                    ApplyClipTags(source.NormalizedJpgPath, classifier, promptCatalog, IRLambda, influentialThreshold, cutoffThreshold);
            } catch (Exception ex) {
                IRLambda.IsKo = true;
                IRLambda.KoReasonCode = "CLASSIFY_ERROR";
                IRLambda.KoSafeMessage = $"Feature extraction failed: {ex.Message}";
                context.KoRecordCount++;
                return IRLambda;
            }
        }

        string[] candidates = ruleSet.EvaluateCandidates(IRLambda.Features);
        IRLambda.CandidatePhenotypes = candidates;
        IRLambda.SelectedPhenotype = candidates.Length > 0 ? candidates[0] : null;

        if (IRLambda.SelectedPhenotype is not null)
            context.PhenotypeAssignedCount++;

        return IRLambda;
    }

    //--- Post-classification deduplication

    private static void DeduplicateAfterClassification(
        IReadOnlyList<ImageRecord_INPUT> okImages,
        Dictionary<ImageRecord_INPUT, ImageRecord_LAMBDA> lambdaByImage,
        PrismConfiguration configuration,
        PipelineContext context ) {
        HashSet<string> exempt = new(configuration.DeduplicationExemptPhenotypes, StringComparer.OrdinalIgnoreCase);
        VisualHasher hasher = new(configuration.MaxHammingDistance);
        IReadOnlyList<DedupGroup> groups = hasher.FindDuplicates(okImages);

        foreach (DedupGroup group in groups) {
            foreach (ImageRecord_INPUT duplicate in group.Duplicates) {
                ImageRecord_LAMBDA IRLambda = lambdaByImage[duplicate];

                // Already rejected (e.g. CLASSIFY_ERROR) — keep its original reason.
                if (IRLambda.IsKo) continue;

                // Illustrations, technical drawings, and labels are exempt so EU energy labels and
                // tech drawings pass; packshots, closeups, and zooms are removed as duplicates.
                if (IRLambda.SelectedPhenotype is not null && exempt.Contains(IRLambda.SelectedPhenotype)) continue;

                IRLambda.IsKo = true;
                IRLambda.KoReasonCode = "VISUAL_DUPLICATE";
                IRLambda.KoSafeMessage = $"Visual duplicate of {Path.GetFileName(group.Canonical.InitialFullName)}";
                context.KoRecordCount++;
                context.DuplicatesRemoved++;
            }
        }
    }

    //--- CLIP tag application

    private static void ApplyClipTags( string imgPath, ImageClassifier imgClsfr, ClipPromptCatalog clipPromptCtlg, ImageRecord_LAMBDA IRLambda, double threshInfluential, double threshCutOff ) {
        ClassificationToken[] allTokens = imgClsfr.ClassifyImage(imgPath, clipPromptCtlg.BuildPrompts());
        if (allTokens.Length == 0) return;

        IRLambda.Tags = new TagCollection {
            Influential = allTokens.Where(t => t.Confidence >= threshInfluential).ToArray(),
            Trivial = allTokens.Where(t => t.Confidence >= threshCutOff && t.Confidence < threshInfluential).ToArray()
        };

        // Write top-scoring tokens into the feature snapshot for phenotype matching.
        foreach (ClassificationToken token in IRLambda.Tags.Influential) {
            if (clipPromptCtlg.TryResolve(token.Label, out string featureId, out string featureValue)) {
                IRLambda.Features.Set(featureId, featureValue, token.Confidence, "clip");
            }
        }
    }

    //--- Config loaders

    private static PhenotypeRuleSet LoadRuleSet() {
        string? imageRolesPath = PrismConfigLocator.FindFolderLocalConfig("ImageNGP/ImageRoles.json");
        if (imageRolesPath is null)
            throw new PrismConfigurationException(
                "ImageRoles.json not found. Ensure ImageNGP/ImageRoles.json is present next to Prism_Config.json.");

        return PhenotypeRuleSet.Load(imageRolesPath);
    }

    private static ClipPromptCatalog LoadPromptCatalog() {
        string? clipPromptsPath = PrismConfigLocator.FindFolderLocalConfig("Images/Classify/ClipPrompts.json");
        if (clipPromptsPath is null)
            throw new PrismConfigurationException(
                "ClipPrompts.json not found. Ensure Images/Classify/ClipPrompts.json is present next to Prism_Config.json.");

        return ClipPromptCatalog.Load(clipPromptsPath);
    }

    private static void InitializeClassifier( ImageClassifier classifier ) {
        string pathRoot = "Images/Classify/ONNX/clip-vit-b32-uint8";

        string? modelPath = PrismConfigLocator.FindFolderLocalConfig($"{pathRoot}/model_uint8.onnx");
        string? vocabPath = PrismConfigLocator.FindFolderLocalConfig($"{pathRoot}/vocab.json");
        string? mergesPath = PrismConfigLocator.FindFolderLocalConfig($"{pathRoot}/merges.txt");

        if (modelPath is null || vocabPath is null || mergesPath is null) throw new PrismConfigurationException(
                "Make sure Images/Classify/ONNX/ and required files are present next to Prism_Config.json.");

         classifier.Initialize(modelPath, vocabPath, mergesPath);
    }

    //--- Record construction

    private static ImageRecord_LAMBDA CreateLambdaRecord( ImageRecord_INPUT source ) {
        return new ImageRecord_LAMBDA {
            InitialFullName = source.InitialFullName,
            Width = source.NormalizedWidth > 0 ? source.NormalizedWidth : source.Width,
            Height = source.NormalizedHeight > 0 ? source.NormalizedHeight : source.Height
        };
    }
}