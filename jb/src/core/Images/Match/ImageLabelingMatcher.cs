/// <summary>
/// Provides CLIP classification label evidence for images after numeric and string brackets complete.
/// Evidence-only: adjusts MatchEvidence confidence but never creates or overrides FamilyID assignments.
/// </summary>
internal sealed class ImageLabelingMatcher
{
    /// <summary>
    /// Builds label evidence by matching influential CLIP tags against MatchingConfig label rules.
    /// </summary>
    /// <param name="record">Lambda record whose Tags.Influential tokens are evaluated.</param>
    /// <param name="families">All FamilyRecords to match labels against.</param>
    /// <param name="labelRules">Label rules from MatchingConfig (ProductColor, ProductType, etc.).</param>
    /// <returns>Evidence items for each label-to-family match found.</returns>
    internal IReadOnlyList<LabelEvidenceItem> BuildEvidence(
        ImageRecord_LAMBDA record,
        IReadOnlyList<FamilyRecord> families,
        IReadOnlyList<MatchingRule> labelRules)
    {
        ClassificationToken[] influentialTags = record.Tags.Influential;
        if (influentialTags.Length == 0 || labelRules.Count == 0)
            return [];

        List<LabelEvidenceItem> evidence = [];

        foreach (ClassificationToken tag in influentialTags)
        {
            string normalizedLabel = tag.Label.ToLowerInvariant().Trim();

            foreach (MatchingRule rule in labelRules)
            {
                bool isAllLabels = rule.ExcelField.Equals("ALL", StringComparison.OrdinalIgnoreCase);

                foreach (FamilyRecord family in families)
                {
                    if (isAllLabels)
                    {
                        if (HasTokenOverlapInAnyStringColumn(normalizedLabel, family))
                        {
                            evidence.Add(new LabelEvidenceItem(
                                tag.Label, "ALL", family.FamilyID, rule.Weight, tag.Confidence));
                        }
                    }
                    else if (family.NormalizedTokens.TryGetValue(rule.ExcelField, out IReadOnlyList<string>? fieldTokens))
                    {
                        bool hasMatch = fieldTokens.Any(
                            ft => ft.Equals(normalizedLabel, StringComparison.OrdinalIgnoreCase));

                        if (hasMatch)
                        {
                            evidence.Add(new LabelEvidenceItem(
                                tag.Label, rule.ExcelField, family.FamilyID, rule.Weight, tag.Confidence));
                        }
                    }
                }
            }
        }

        return evidence;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the normalized label appears as a token in any non-numeric column of the family.
    /// </summary>
    private static bool HasTokenOverlapInAnyStringColumn(string normalizedLabel, FamilyRecord family)
    {
        foreach (KeyValuePair<string, IReadOnlyList<string>> property in family.NormalizedTokens)
        {
            ExcelColumnClassification classification = family.ColumnClassifications.TryGetValue(
                property.Key, out ExcelColumnClassification cls)
                    ? cls
                    : ExcelColumnClassification.Descriptive;

            if (classification is ExcelColumnClassification.Numerical or ExcelColumnClassification.FamilyID)
                continue;

            if (property.Value.Any(token => token.Equals(normalizedLabel, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }
}
