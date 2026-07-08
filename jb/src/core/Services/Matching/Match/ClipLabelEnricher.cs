namespace Prism.Core;

/// <summary>
/// Provides CLIP label evidence for images. Evidence-only: never creates or overrides FamilyID assignments.
/// Used by ImageMatcher to enrich already-matched records and by SemanticMatcher as a hard filter.
/// Tags are matched by their feature <see cref="ClassificationToken.Value"/> (e.g. "red") — the prompt
/// sentence itself can never equal an Excel token. Rules with a ClipFeature restrict which tag features
/// they consider (e.g. the ProductColor rule only reads "product-color" tags).
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
            string matchToken = MatchableToken(tag);
            if (matchToken.Length == 0) continue;

            foreach (MatchingRule rule in labelRules) {
                if (!rule.AppliesToFeature(tag.Feature)) continue;

                bool isAllLabels = rule.ExcelField.Equals("ALL", StringComparison.OrdinalIgnoreCase);

                foreach (FamilyIDRecord family in families) {
                    if (isAllLabels) {
                        if (HasTokenOverlapInAnyStringColumn(matchToken, family)) {
                            evidence.Add(new LabelEvidenceItem(matchToken, "ALL", family.FamilyID, rule.Weight, tag.Confidence));
                        }
                    }
                    else if (family.NormalizedTokens.TryGetValue(rule.ExcelField, out IReadOnlyList<string>? fieldTokens)) {
                        bool hasMatch = fieldTokens.Any(ft => ft.Equals(matchToken, StringComparison.OrdinalIgnoreCase));

                        if (hasMatch) {
                            evidence.Add(new LabelEvidenceItem(matchToken, rule.ExcelField, family.FamilyID, rule.Weight, tag.Confidence));
                        }
                    }
                }
            }
        }

        return evidence;
    }

    /// <summary>
    /// True when the record carries at least one influential tag this rule may consider — the
    /// per-dimension gate SemanticMatcher uses so an untagged dimension passes candidates through
    /// instead of erasing them.
    /// </summary>
    internal static bool HasTagForRule(ImageRecord_LAMBDA record, MatchingRule rule) {
        foreach (ClassificationToken tag in record.Tags.Influential) {
            if (MatchableToken(tag).Length > 0 && rule.AppliesToFeature(tag.Feature)) return true;
        }
        return false;
    }

    // --- Helpers

    /// <summary>
    /// The token an influential tag contributes to Excel matching: its resolved feature value.
    /// Raw tags without a value (legacy or unresolved prompts) contribute nothing — a prompt
    /// sentence can never equal a column token.
    /// </summary>
    private static string MatchableToken(ClassificationToken tag) =>
        tag.Value.Trim().ToLowerInvariant();

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
