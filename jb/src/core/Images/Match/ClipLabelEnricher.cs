namespace Prism.Core;

/// <summary>
/// Provides CLIP label evidence for images. Evidence-only: never creates or overrides FamilyID assignments.
/// Used by ImageMatcher to enrich already-matched records and by SemanticMatcher as a hard filter.
/// </summary>
internal sealed class ClipLabelEnricher {
    /// <summary>
    /// Builds CLIP label evidence by matching influential tags against MatchingConfig label rules.
    /// </summary>
    /// <param name="record">Lambda record whose Tags.Influential tokens are evaluated.</param>
    /// <param name="families">FamilyRecords to match CLIP labels against.</param>
    /// <param name="labelRules">Label rules from MatchingConfig (ProductColor, ProductType, etc.).</param>
    /// <returns>Evidence items for each CLIP label-to-family match found.</returns>
    internal IReadOnlyList<LabelEvidenceItem> BuildEvidence(ImageRecord_LAMBDA record, IReadOnlyList<FamilyIDRecord> families, IReadOnlyList<MatchingRule> labelRules) {
        ClassificationToken[] influentialTags = record.Tags.Influential;
        if (influentialTags.Length == 0 || labelRules.Count == 0) return [];

        List<LabelEvidenceItem> evidence = [];

        foreach (ClassificationToken tag in influentialTags) {
            string normalizedLabel = tag.Label.ToLowerInvariant().Trim();

            foreach (MatchingRule rule in labelRules) {
                bool isAllLabels = rule.ExcelField.Equals("ALL", StringComparison.OrdinalIgnoreCase);

                foreach (FamilyIDRecord family in families) {
                    if (isAllLabels) {
                        if (HasTokenOverlapInAnyStringColumn(normalizedLabel, family)) {
                            evidence.Add(new LabelEvidenceItem(tag.Label, "ALL", family.FamilyID, rule.Weight, tag.Confidence));
                        }
                    }
                    else if (family.NormalizedTokens.TryGetValue(rule.ExcelField, out IReadOnlyList<string>? fieldTokens)) {
                        bool hasMatch = fieldTokens.Any(ft => ft.Equals(normalizedLabel, StringComparison.OrdinalIgnoreCase));

                        if (hasMatch) {
                            evidence.Add(new LabelEvidenceItem(tag.Label, rule.ExcelField, family.FamilyID, rule.Weight, tag.Confidence));
                        }
                    }
                }
            }
        }

        return evidence;
    }

    // --- Helpers

    /// <summary>
    /// Returns true when the normalized CLIP label appears as a token in any non-numeric column of the family.
    /// </summary>
    private static bool HasTokenOverlapInAnyStringColumn(string normalizedLabel, FamilyIDRecord family) {
        foreach (KeyValuePair<string, IReadOnlyList<string>> property in family.NormalizedTokens) {
            ExcelColumnClassification classification = family.ColumnClassifications.TryGetValue(property.Key, out ExcelColumnClassification cls) ? cls : ExcelColumnClassification.Descriptive;
            if (classification is ExcelColumnClassification.Numerical or ExcelColumnClassification.FamilyID) continue;
            if (property.Value.Any(token => token.Equals(normalizedLabel, StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }
}
