/// <summary>
/// Orders images within each matched FamilyID group using ImageNGP phenotype qualification,
/// per-product-type DetOrderRules, filename hints, and deterministic tie-breaking.
/// Writes <see cref="ImageRecord_Base.Family"/>, <see cref="ImageRecord_Base.DetOrder"/>,
/// and <see cref="OrderEvidence"/> to each processed record.
/// </summary>
internal static class ImageOrderer
{
    /// <summary>
    /// Runs the Ordered stage for the given job context.
    /// Groups non-KO matched records by FinalFamilyId, assigns det slots per product type rules,
    /// and records ordering evidence on each record.
    /// </summary>
    internal static void Run(PipelineContext context)
    {
        DetOrderConfig config = LoadConfig();

        List<IGrouping<string, ImageRecord_LAMBDA>> familyGroups = context.LambdaRecords
            .Where(r => !r.IsKo && r.MatchEvidence?.FinalFamilyId is not null)
            .GroupBy(r => r.MatchEvidence!.FinalFamilyId!)
            .ToList();

        Dictionary<string, FamilyRecord> familyLookup = context.FamilyRecords
            .ToDictionary(f => f.FamilyID, f => f, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, ImageRecord_LAMBDA> group in familyGroups)
        {
            familyLookup.TryGetValue(group.Key, out FamilyRecord? familyRecord);
            ProcessFamily(group.Key, group.ToList(), familyRecord, config);
        }
    }

    // ─── Family processing ────────────────────────────────────────────────────

    /// <summary>
    /// Assigns det slots to all images within one FamilyID group.
    /// Uses phenotype qualification, slot preference lists, and deterministic tie-breaking.
    /// Overflow images (no qualifying phenotype) are assigned slots after the last configured slot.
    /// </summary>
    private static void ProcessFamily(
        string familyId,
        List<ImageRecord_LAMBDA> images,
        FamilyRecord? familyRecord,
        DetOrderConfig config)
    {
        string productTypeId = ResolveProductType(familyRecord, config);
        IReadOnlyList<DetSlotRule> slots = config.GetSlots(productTypeId);
        int lastConfiguredSlot = slots.Count > 0 ? slots[^1].SlotIndex : -1;

        List<Candidate> candidates = BuildCandidates(images, slots, config);
        candidates.Sort(CompareCandidates);

        bool[] imageAssigned = new bool[images.Count];
        HashSet<int> slotClaimed = [];
        Dictionary<int, AssignmentRecord> assignments = [];

        // Assign each candidate in sorted order (best slot/phenotype first).
        foreach (Candidate c in candidates)
        {
            if (imageAssigned[c.ImageIndex]) continue;
            if (slotClaimed.Contains(c.DetSlot)) continue;

            string tieBreakerWon = DetermineTieBreaker(c, candidates);

            imageAssigned[c.ImageIndex] = true;
            slotClaimed.Add(c.DetSlot);
            assignments[c.ImageIndex] = new AssignmentRecord(
                c.DetSlot, c.Phenotype, c.PhenotypeRank,
                c.NgpConfidence, tieBreakerWon, IsOverflow: false);
        }

        // Images with no qualifying phenotype become overflow after the last configured slot.
        int overflowSlot = lastConfiguredSlot + 1;
        foreach ((ImageRecord_LAMBDA img, int idx) in images
            .Select((img, idx) => (img, idx))
            .Where(x => !imageAssigned[x.idx])
            .OrderBy(x => x.idx))
        {
            int ngpConfidence = img.Features.All.Count(kv => !kv.Value.IsUnknown);
            assignments[idx] = new AssignmentRecord(
                overflowSlot++, WinningPhenotype: null, PhenotypeRank: -1,
                ngpConfidence, TieBreakerWon: "none", IsOverflow: true);
        }

        // Write results back to records.
        foreach ((int imageIndex, AssignmentRecord record) in assignments)
        {
            ImageRecord_LAMBDA lambda = images[imageIndex];
            lambda.Family   = familyId;
            lambda.DetOrder = record.DetSlot;
            lambda.OrderEvidence = new OrderEvidence
            {
                AssignedDetSlot     = record.DetSlot,
                WinningPhenotype    = record.WinningPhenotype,
                PhenotypeRankInSlot = record.PhenotypeRank,
                NgpConfidenceCount  = record.NgpConfidence,
                TieBreakerWon       = record.TieBreakerWon,
                IsOverflow          = record.IsOverflow
            };
        }
    }

    // ─── Candidate building ───────────────────────────────────────────────────

    /// <summary>
    /// Builds all (image, slot) candidates where the image's selected phenotype qualifies for the slot.
    /// Each qualifying combination becomes one candidate entry for the assignment sort.
    /// </summary>
    private static List<Candidate> BuildCandidates(
        List<ImageRecord_LAMBDA> images,
        IReadOnlyList<DetSlotRule> slots,
        DetOrderConfig config)
    {
        List<Candidate> result = [];

        for (int i = 0; i < images.Count; i++)
        {
            ImageRecord_LAMBDA img = images[i];
            if (img.SelectedPhenotype is null) continue;

            int ngpConfidence = img.Features.All.Count(kv => !kv.Value.IsUnknown);

            foreach (DetSlotRule slot in slots)
            {
                int phenotypeRank = ((IList<string>)slot.Phenotypes).IndexOf(img.SelectedPhenotype);
                if (phenotypeRank < 0) continue;

                int hintScore = config.FilenameMatchesSlotKeyword(img.InitialFullName, slot.Keyword) ? 1 : 0;
                result.Add(new Candidate(i, slot.SlotIndex, phenotypeRank, ngpConfidence, hintScore, i, img.SelectedPhenotype));
            }
        }

        return result;
    }

    // ─── Candidate comparison ─────────────────────────────────────────────────

    /// <summary>
    /// Sorts candidates so the best assignment comes first.
    /// Priority: earlier det slot → lower phenotype rank → higher NGP confidence → filename hint → lower source index.
    /// </summary>
    private static int CompareCandidates(Candidate a, Candidate b)
    {
        int cmp = a.DetSlot.CompareTo(b.DetSlot);             if (cmp != 0) return cmp;
        cmp = a.PhenotypeRank.CompareTo(b.PhenotypeRank);     if (cmp != 0) return cmp;
        cmp = b.NgpConfidence.CompareTo(a.NgpConfidence);     if (cmp != 0) return cmp;
        cmp = b.HintScore.CompareTo(a.HintScore);             if (cmp != 0) return cmp;
        return a.SourceIndex.CompareTo(b.SourceIndex);
    }

    // ─── Tie-breaker labelling ────────────────────────────────────────────────

    /// <summary>
    /// Returns which tie-breaker determined the winner over competitors at the same slot and phenotype rank.
    /// Returns "none" when there were no competitors.
    /// </summary>
    private static string DetermineTieBreaker(Candidate winner, List<Candidate> all)
    {
        List<Candidate> competitors = all.Where(c =>
            !ReferenceEquals(c, winner) &&
            c.DetSlot == winner.DetSlot &&
            c.PhenotypeRank == winner.PhenotypeRank).ToList();

        if (competitors.Count == 0) return "none";
        if (competitors.Any(c => c.NgpConfidence != winner.NgpConfidence)) return "ngp-confidence";
        if (competitors.Any(c => c.HintScore     != winner.HintScore))     return "filename-hint";
        return "source-index";
    }

    // ─── Product type resolution ──────────────────────────────────────────────

    /// <summary>
    /// Resolves the product type id from the FamilyRecord's canonical properties.
    /// Normalises each value to kebab-case and checks against known product type ids.
    /// Returns "default" when no match is found.
    /// </summary>
    private static string ResolveProductType(FamilyRecord? familyRecord, DetOrderConfig config)
    {
        if (familyRecord is null) return "default";

        foreach (string value in familyRecord.CanonicalProperties.Values)
        {
            string normalized = value.ToLowerInvariant()
                .Replace(' ', '-')
                .Replace('_', '-');

            if (config.HasProductType(normalized)) return normalized;
        }

        return "default";
    }

    // ─── Config loader ────────────────────────────────────────────────────────

    /// <summary>
    /// Locates and loads both order config files using PrismConfigLocator conventions.
    /// Throws <see cref="PrismConfigurationException"/> when either file is not found.
    /// </summary>
    private static DetOrderConfig LoadConfig()
    {
        string? rulesPath = PrismConfigLocator.FindFolderLocalConfig("Images/Order/DetOrderRules.json");
        string? stemsPath = PrismConfigLocator.FindFolderLocalConfig("Images/Order/DetOrderKeywordStems.json");

        if (rulesPath is null)
            throw new PrismConfigurationException(
                "DetOrderRules.json not found. Ensure Images/Order/DetOrderRules.json is present next to Prism_Config.json.");
        if (stemsPath is null)
            throw new PrismConfigurationException(
                "DetOrderKeywordStems.json not found. Ensure Images/Order/DetOrderKeywordStems.json is present next to Prism_Config.json.");

        return DetOrderConfig.Load(rulesPath, stemsPath);
    }

}
