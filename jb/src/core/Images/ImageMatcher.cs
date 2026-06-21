namespace Prism.Core;

/// <summary>
/// Waterfall matching orchestrator for the Matched pipeline stage.
/// Runs numeric (brackets 1–2), string (bracket 3), and label-evidence (bracket 4) matchers in sequence.
/// Matched images are removed from subsequent brackets. Remaining images are KO'd.
/// </summary>
internal sealed class ImageMatcher
{
    private readonly MatchingConfig     matchingConfig;
    private readonly NumericMatcher     numericMatcher;
    private readonly StringMatcher      stringMatcher;
    private readonly ImageLabelingMatcher labelingMatcher;

    private ImageMatcher(MatchingConfig matchingConfig, TranslationConfig translationConfig, string familyIdColumnName)
    {
        this.matchingConfig = matchingConfig;
        numericMatcher      = new NumericMatcher(familyIdColumnName);
        stringMatcher       = new StringMatcher(translationConfig);
        labelingMatcher     = new ImageLabelingMatcher();
    }

    /// <summary>
    /// Entry point called by <see cref="ShellStage_Match"/>.
    /// Loads configs, runs the waterfall, and writes MatchEvidence to every lambda record.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    internal static void Run(PipelineContext context)
    {
        string matchingConfigPath = LoadConfigPath(
            "Images/Match/MatchingConfig.json",
            "MatchingConfig.json not found next to Prism_Config.json.");

        string translationConfigPath = LoadConfigPath(
            "Images/Match/Translate/TranslationConfig.json",
            "TranslationConfig.json not found next to Prism_Config.json.");

        string excelConfigPath = LoadConfigPath(
            "Excel/ExcelConfig.json",
            "ExcelConfig.json not found next to Prism_Config.json.");

        MatchingConfig    matchingConfig    = MatchingConfig.Load(matchingConfigPath);
        TranslationConfig translationConfig = TranslationConfig.Load(translationConfigPath);
        ExcelConfig       excelConfig       = ExcelConfig.Load(excelConfigPath);

        ImageMatcher matcher = new(matchingConfig, translationConfig, excelConfig.RecordPrimaryKey);
        matcher.RunWaterfall(context.LambdaRecords, context.FamilyRecords, context);
    }

    // ─── Waterfall ─────────────────────────────────────────────────────────────

    private void RunWaterfall(
        List<ImageRecord_LAMBDA> allRecords,
        IReadOnlyList<FamilyRecord> families,
        PipelineContext context)
    {
        IReadOnlyList<MatchingRule> numericRules = matchingConfig.NumericRules;
        IReadOnlyList<MatchingRule> labelRules   = matchingConfig.LabelRules;

        List<ImageRecord_LAMBDA> unmatched = allRecords.Where(r => !r.IsKo).ToList();

        // Bracket 1: single numeric token, TCD = 0
        unmatched = RunBracket1(unmatched, families, numericRules);

        // Bracket 2: multi-token numeric concatenation, TCD ≤ maxDistance
        unmatched = RunBracket2(unmatched, families, numericRules);

        // Bracket 3: string tokens, exactly-1-FamilyID
        unmatched = RunBracket3(unmatched, families);

        // Bracket 4: add label evidence to matched records (no new assignments)
        AddLabelEvidence(allRecords, families, labelRules);

        // Bracket 4 cleanup: KO any image still without a FamilyID assignment
        KoUnmatched(unmatched, context);

        // Bracket 5: finalize clustering (single-pass waterfall means no structural ties)
        FinalizeMatches(allRecords);
    }

    // ─── Bracket 1 ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs NumericMatcher single-token bracket. Returns images not yet matched.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket1(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in candidates)
        {
            MatchEvidence? evidence = numericMatcher.TryMatchBracket1(record, families, numericRules);

            if (evidence is not null)
                record.MatchEvidence = evidence;
            else
                stillUnmatched.Add(record);
        }

        return stillUnmatched;
    }

    // ─── Bracket 2 ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs NumericMatcher multi-token bracket. Returns images not yet matched.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket2(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyRecord> families,
        IReadOnlyList<MatchingRule> numericRules)
    {
        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in candidates)
        {
            MatchEvidence? evidence = numericMatcher.TryMatchBracket2(record, families, numericRules);

            if (evidence is not null)
                record.MatchEvidence = evidence;
            else
                stillUnmatched.Add(record);
        }

        return stillUnmatched;
    }

    // ─── Bracket 3 ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs StringMatcher exactly-1-FamilyID bracket. Returns images not yet matched.
    /// </summary>
    private List<ImageRecord_LAMBDA> RunBracket3(
        List<ImageRecord_LAMBDA> candidates,
        IReadOnlyList<FamilyRecord> families)
    {
        List<ImageRecord_LAMBDA> stillUnmatched = [];

        foreach (ImageRecord_LAMBDA record in candidates)
        {
            MatchEvidence? evidence = stringMatcher.TryMatch(record, families);

            if (evidence is not null)
                record.MatchEvidence = evidence;
            else
                stillUnmatched.Add(record);
        }

        return stillUnmatched;
    }

    // ─── Bracket 4: label evidence ────────────────────────────────────────────

    /// <summary>
    /// Appends CLIP label evidence to the MatchEvidence of already-matched records.
    /// Never creates or overrides FamilyID assignments.
    /// </summary>
    private void AddLabelEvidence(
        List<ImageRecord_LAMBDA> allRecords,
        IReadOnlyList<FamilyRecord> families,
        IReadOnlyList<MatchingRule> labelRules)
    {
        if (labelRules.Count == 0)
            return;

        foreach (ImageRecord_LAMBDA record in allRecords)
        {
            if (record.IsKo || record.MatchEvidence?.FinalFamilyId is null)
                continue;

            IReadOnlyList<LabelEvidenceItem> labelEvidence =
                labelingMatcher.BuildEvidence(record, families, labelRules);

            if (labelEvidence.Count == 0)
                continue;

            record.MatchEvidence = record.MatchEvidence with
            {
                ClassificationLabelEvidence =
                [
                    ..record.MatchEvidence.ClassificationLabelEvidence,
                    ..labelEvidence
                ]
            };
        }
    }

    // ─── Bracket 4 cleanup ────────────────────────────────────────────────────

    /// <summary>
    /// KOs any image that was not matched by brackets 1–3.
    /// </summary>
    private static void KoUnmatched(
        List<ImageRecord_LAMBDA> unmatched,
        PipelineContext context)
    {
        foreach (ImageRecord_LAMBDA record in unmatched)
        {
            string sourceFilename = record.InitialFullName ?? string.Empty;
            string imageId        = Path.GetFileNameWithoutExtension(sourceFilename);

            record.IsKo          = true;
            record.KoReasonCode  = "MATCH_NOT_FOUND";
            record.KoSafeMessage = $"No FamilyID match found for '{imageId}'.";
            record.MatchEvidence = new MatchEvidence
            {
                ImageId         = imageId,
                SourceFilename  = sourceFilename,
                IsKo            = true,
                KoReason        = "MATCH_NOT_FOUND",
                SafeExplanation = $"'{imageId}' was not matched to any FamilyID after all matching brackets."
            };

            context.KoRecordCount++;
        }
    }

    // ─── Bracket 5: finalize ─────────────────────────────────────────────────

    /// <summary>
    /// Finalizes FamilyID clusters. Single-pass waterfall produces no structural ties; this step
    /// is a hook for T-700 ordering to consume the clustered results.
    /// </summary>
    private static void FinalizeMatches(List<ImageRecord_LAMBDA> allRecords)
    {
        // No additional action required for T-600.
        // T-700 reads record.MatchEvidence.FinalFamilyId to build det-order clusters.
        _ = allRecords;
    }

    // ─── Config loading ───────────────────────────────────────────────────────

    private static string LoadConfigPath(string relativePath, string missingMessage)
    {
        string? path = PrismConfigLocator.FindFolderLocalConfig(relativePath);
        if (path is null)
            throw new PrismConfigurationException(missingMessage);

        return path;
    }
}
