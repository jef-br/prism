namespace Prism.Services.Matching;

/// <summary>
/// Orders images within each matched FamilyID group using ImageNGP phenotype qualification,
/// per-product-type DetOrderRules, filename hints, and deterministic tie-breaking.
/// Writes <see cref="ImageRecord_Base.Family"/>, <see cref="ImageRecord_Base.DetOrder"/>,
/// and <see cref="OrderEvidence"/> to each processed record.
/// </summary>
internal static class ImageOrderer
{
    /// <summary>
    /// Runs the Ordered stage over a matched LAMBDA collection.
    /// Groups non-KO matched records by FinalFamilyId, assigns det slots per product type rules,
    /// and records ordering evidence on each record.
    /// </summary>
    /// <param name="records">Matched LAMBDA records.</param>
    /// <param name="families">Family records resolved from the Internal Excel Model.</param>
    internal static void Run(List<ImageRecord_LAMBDA> records, IReadOnlyList<FamilyIDRecord> families)
    {
        DetOrderConfig config = LoadConfig();

        List<IGrouping<string, ImageRecord_LAMBDA>> familyGroups = records
            .Where(r => !r.IsKo && r.MatchEvidence?.FinalFamilyId is not null)
            .GroupBy(r => r.MatchEvidence!.FinalFamilyId!)
            .ToList();

        Dictionary<string, FamilyIDRecord> familyLookup = families
            .ToDictionary(f => f.FamilyID, f => f, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, ImageRecord_LAMBDA> group in familyGroups)
        {
            familyLookup.TryGetValue(group.Key, out FamilyIDRecord? familyIDRecord);
            ProcessFamily(group.Key, group.ToList(), familyIDRecord, config);
        }
    }

    //  Det compaction (gap policy)

    /// <summary>
    /// Compacts each family's det indices to a contiguous 0..n-1 range: gaps are closed and the relative
    /// order the Order stage assigned is preserved exactly (renumber only, never reorder). Applied when
    /// Output.DET-ORDER-GAPS-ALLOWED is false. Operates on non-KO matched records grouped by Family.
    /// </summary>
    internal static void CompactDetOrder(IReadOnlyList<ImageRecord_LAMBDA> records)
    {
        IEnumerable<IGrouping<string, ImageRecord_LAMBDA>> familyGroups = records
            .Where(r => !r.IsKo && !string.IsNullOrEmpty(r.Family))
            .GroupBy(r => r.Family!);

        foreach (IGrouping<string, ImageRecord_LAMBDA> group in familyGroups)
        {
            int det = 0;
            foreach (ImageRecord_LAMBDA lambda in group.OrderBy(r => r.DetOrder))
                lambda.DetOrder = det++;
        }
    }

    //  Family processing

    /// <summary>
    /// Assigns det slots to all images within one FamilyID group.
    /// Uses phenotype qualification, slot preference lists, and deterministic tie-breaking.
    /// Overflow images (no qualifying phenotype) are assigned slots after the last configured slot.
    /// </summary>
    private static void ProcessFamily(
        string familyId,
        List<ImageRecord_LAMBDA> images,
        FamilyIDRecord? familyIDRecord,
        DetOrderConfig config)
    {
        string productTypeId = ResolveProductType(images, familyIDRecord, config);
        IReadOnlyList<DetSlotRule> slots = config.GetSlots(productTypeId);
        int lastConfiguredSlot = slots.Count > 0 ? slots[^1].SlotIndex : -1;

        List<CandidateDetOrder> candidates = BuildCandidates(images, slots, config);
        candidates.Sort(CompareCandidates);

        bool[] imageAssigned = new bool[images.Count];
        HashSet<int> slotClaimed = [];
        Dictionary<int, AssignmentRecord> assignments = [];

        // Assign each candidate in sorted order (best slot/phenotype first).
        for (int i = 0; i < candidates.Count; i++) {
            CandidateDetOrder c = candidates[i];
            if (imageAssigned[c.ImageIndex]) continue;
            if (slotClaimed.Contains(c.DetSlot)) continue;

            string tieBreakerWon = DetermineTieBreaker(candidates, i, imageAssigned);

            imageAssigned[c.ImageIndex] = true;
            slotClaimed.Add(c.DetSlot);
            assignments[c.ImageIndex] = new AssignmentRecord(
                c.DetSlot, c.Phenotype, c.PhenotypeRank,
                c.NgpConfidence, tieBreakerWon, IsOverflow: false);
        }

        // Images with no qualifying phenotype become overflow after the last configured slot.
        // Overflow order uses real signal instead of raw list position: filename-hinted images
        // anchor at their hinted slot position, unhinted images anchor at the configured position
        // between the main-view slots and the detail/label/material slots — so a 'detail'-hinted
        // file can never jump ahead of the family's main shots. Within the same anchor, on-model
        // images (hero-is-human TRUE) outrank packshots, then numeric-aware natural filename
        // order — so 'Pareo_F1' precedes 'Pareo_F2' and 'img_2' precedes 'img_10'.
        int overflowSlot = lastConfiguredSlot + 1;
        foreach ((ImageRecord_LAMBDA img, int idx, int hintSlot) in images
            .Select((img, idx) => (img, idx, HintSlot: ResolveHintSlot(img.InitialFullName, slots, config)))
            .Where(x => !imageAssigned[x.idx])
            .OrderBy(x => x.HintSlot == int.MaxValue ? config.OverflowUnhintedAnchor : x.HintSlot)
            .ThenBy(x => OnModelRank(x.img, config))
            .ThenBy(x => x.img.InitialFullName, NaturalFilenameComparer)
            .ThenBy(x => x.idx))
        {
            int ngpConfidence = img.Features.All.Count(kv => !kv.Value.IsUnknown);
            assignments[idx] = new AssignmentRecord(
                overflowSlot++, WinningPhenotype: null, PhenotypeRank: -1,
                ngpConfidence,
                TieBreakerWon: hintSlot != int.MaxValue ? "overflow-filename-hint" : "overflow-natural-order",
                IsOverflow: true);
        }

        // Write results back to records.
        foreach ((int imageIndex, AssignmentRecord record) in assignments)
        {
            ImageRecord_LAMBDA lambda = images[imageIndex];
            lambda.Family        = familyId;
            lambda.DetOrder      = record.DetSlot;
            lambda.ProductTypeId = productTypeId;
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

    //  Overflow ordering signal

    /// <summary>
    /// The earliest configured slot whose keyword stems match the filename, or int.MaxValue when no
    /// keyword matches — used to order overflow images by intent (front before back before side).
    /// </summary>
    private static int ResolveHintSlot(string filename, IReadOnlyList<DetSlotRule> slots, DetOrderConfig config)
    {
        foreach (DetSlotRule slot in slots)
        {
            if (config.FilenameMatchesSlotKeyword(filename, slot.Keyword))
                return slot.SlotIndex;
        }

        return int.MaxValue;
    }

    /// <summary>
    /// Overflow rank for the on-model-before-packshot rule: a product shown on a human model
    /// (hero-is-human TRUE) is more valuable than the same product as a packshot, so it sorts
    /// first (0). Everything else — FALSE or UNKNOWN — ranks equal (1); UNKNOWN must not outrank
    /// a known packshot. Disabled via DetOrderRules.json overflowPolicy.onModelFirst.
    /// </summary>
    private static int OnModelRank(ImageRecord_LAMBDA img, DetOrderConfig config)
    {
        if (!config.OverflowOnModelFirst) return 0;
        return string.Equals(img.Features.GetValue("hero-is-human"), "TRUE", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    /// <summary>Numeric-aware ordinal filename comparer: digit runs compare as numbers, text ordinally.</summary>
    private static readonly Comparer<string> NaturalFilenameComparer = Comparer<string>.Create(CompareNatural);

    private static int CompareNatural(string a, string b)
    {
        int i = 0, j = 0;

        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int startA = i, startB = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;

                // Compare digit runs numerically: longer run of significant digits wins.
                ReadOnlySpan<char> runA = a.AsSpan(startA, i - startA).TrimStart('0');
                ReadOnlySpan<char> runB = b.AsSpan(startB, j - startB).TrimStart('0');
                if (runA.Length != runB.Length) return runA.Length - runB.Length;
                int cmp = runA.CompareTo(runB, StringComparison.Ordinal);
                if (cmp != 0) return cmp;
            }
            else
            {
                int cmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                if (cmp != 0) return cmp;
                i++; j++;
            }
        }

        int lengthCmp = (a.Length - i) - (b.Length - j);
        if (lengthCmp != 0) return lengthCmp;

        // Case-insensitive equal — fall back to ordinal so the order is still total and deterministic.
        return string.CompareOrdinal(a, b);
    }

    //  Candidate building

    /// <summary>
    /// Builds all (image, slot) candidates where the image's selected phenotype qualifies for the slot.
    /// Each qualifying combination becomes one candidate entry for the assignment sort.
    /// </summary>
    private static List<CandidateDetOrder> BuildCandidates(
        List<ImageRecord_LAMBDA> images,
        IReadOnlyList<DetSlotRule> slots,
        DetOrderConfig config)
    {
        List<CandidateDetOrder> result = [];

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
                result.Add(new CandidateDetOrder(i, slot.SlotIndex, phenotypeRank, ngpConfidence, hintScore, i, img.SelectedPhenotype, img.InitialFullName));
            }
        }

        return result;
    }

    //  Candidate comparison 

    /// <summary>
    /// Sorts candidates so the best assignment comes first.
    /// Priority: earlier det slot → lower phenotype rank → higher NGP confidence → filename hint →
    /// filename ordinal → lower source index. Filename ordinal comes before source index so the
    /// outcome does not depend on list position even if upstream ordering ever changes (T-2820).
    /// </summary>
    private static int CompareCandidates(CandidateDetOrder a, CandidateDetOrder b)
    {
        int cmp = a.DetSlot.CompareTo(b.DetSlot);             if (cmp != 0) return cmp;
        cmp = a.PhenotypeRank.CompareTo(b.PhenotypeRank);     if (cmp != 0) return cmp;
        cmp = b.NgpConfidence.CompareTo(a.NgpConfidence);     if (cmp != 0) return cmp;
        cmp = b.HintScore.CompareTo(a.HintScore);             if (cmp != 0) return cmp;
        cmp = string.CompareOrdinal(a.Filename, b.Filename);  if (cmp != 0) return cmp;
        return a.SourceIndex.CompareTo(b.SourceIndex);
    }

    //  Tie-breaker labelling 

    // Names the tie-breaker that decided the winner against its *closest* rival — the next still-unassigned
    // candidate sharing its det slot and phenotype rank — rather than against any competitor anywhere in the
    // family. Only the closest rival can name the deciding level: a far-behind candidate that lost on
    // confidence must not mask the filename hint that actually settled a confidence-tied race.
    // CompareCandidates sorts by slot then phenotype rank first, so tied candidates form one contiguous
    // block and the rival is one forward scan away — no rescan of the family's full candidate list.
    // Scanning forward only is safe: the slot was still free, so every candidate *before* the winner in its
    // block can only have been skipped for holding an earlier slot already — never a rival for this one.
    // The levels below mirror CompareCandidates exactly, so the label can never contradict the sort.
    private static string DetermineTieBreaker(List<CandidateDetOrder> candidates, int winnerIndex, bool[] imageAssigned) {
        CandidateDetOrder winner = candidates[winnerIndex];

        for (int i = winnerIndex + 1; i < candidates.Count; i++) {
            CandidateDetOrder rival = candidates[i];
            if (rival.DetSlot != winner.DetSlot || rival.PhenotypeRank != winner.PhenotypeRank) break;
            if (imageAssigned[rival.ImageIndex]) continue;

            if (rival.NgpConfidence != winner.NgpConfidence) return "ngp-confidence";
            if (rival.HintScore != winner.HintScore) return "filename-hint";
            if (string.CompareOrdinal(winner.Filename, rival.Filename) != 0) return "filename-ordinal";
            return "source-index";
        }

        return "none";
    }

    //  Product type resolution 

    /// <summary>
    /// Resolves the family's product type id. The refinement chain (Analyzer_ProductType) already
    /// resolved it from the IEM producttype/ngp columns onto each image — the first validated id
    /// wins. Fallback: sniff every canonical property value for a known product type id (legacy
    /// path, to retire after real-batch validation — see the Analyzers jbtodo). Then "default".
    /// </summary>
    private static string ResolveProductType(List<ImageRecord_LAMBDA> images, FamilyIDRecord? familyIDRecord, DetOrderConfig config)
    {
        foreach (ImageRecord_LAMBDA image in images)
        {
            if (image.ProductTypeId is string resolved && config.HasProductType(resolved)) return resolved;
        }

        if (familyIDRecord is null) return "default";

        foreach (string value in familyIDRecord.CanonicalProperties.Values)
        {
            string normalized = value.ToLowerInvariant()
                .Replace(' ', '-')
                .Replace('_', '-');

            if (config.HasProductType(normalized)) return normalized;
        }

        return "default";
    }

    //  Config loader 

    /// <summary>
    /// Locates and loads both order config files. Throws <see cref="PrismConfigurationException"/>
    /// when either file is not found.
    /// </summary>
    private static DetOrderConfig LoadConfig()
    {
        return DetOrderConfig.Load(
            ConfigLoader.RequireFile("DetOrderRules.json"),
            ConfigLoader.RequireFile("DetOrderKeywordStems.json"));
    }

}
