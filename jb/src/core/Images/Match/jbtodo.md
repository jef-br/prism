# Image Matching Todo

- [ ] Define matcher score aggregation: say how numeric, string, and image-label scores combine into one candidate decision.
  - Impact:
    - Project progress: High - Aggregation decides the final product match for every image.
    - Effect on other TODOs: Blocks - It gates threshold enforcement, tie-breaking, matcher evidence, output naming, and KO unmatched policy.
  - Industry standard:
    Matching systems combine specialized signals with explicit weights, thresholds, and evidence retention so decisions are reproducible and tunable.
  - Recommended solution:
    Use weighted aggregation where exact numeric FamilyID evidence dominates, string evidence supports candidates, and image labels act as secondary corroboration.
  - Answer:

- [ ] Define matcher threshold enforcement: say what minimum score makes an image eligible for a FamilyID.
  - Impact:
    - Project progress: High - Thresholds determine OK versus KO for unmatched images.
    - Effect on other TODOs: Blocks - It affects unmatched naming, manifest KO groups, output filename stems, and workbench diagnostics.
  - Industry standard:
    Entity matching pipelines set explicit acceptance thresholds and report below-threshold candidates as rejected evidence rather than forcing a match.
  - Recommended solution:
    Require a high confidence threshold for automatic FamilyID assignment, with exact numeric matches accepted fastest and ambiguous matches sent to KO.
  - Answer:

- [ ] Define matcher tie-breaking: say how Prism chooses between products with equal or near-equal scores.
  - Impact:
    - Project progress: High - Tie-breaking prevents nondeterministic product assignments.
    - Effect on other TODOs: Blocks - It influences MatcherResult tie state, output naming, and KO policy.
  - Industry standard:
    Matching systems avoid arbitrary ties; they either apply deterministic secondary evidence or reject ambiguous candidates for review.
  - Recommended solution:
    Break ties with exact numeric evidence, then stronger filename token provenance, then image-label corroboration; KO near-equal unresolved ties.
  - Answer:

- [ ] Define matcher evidence retention: say which evidence is stored for manifest and workbench explanation.
  - Impact:
    - Project progress: High - Evidence retention makes automated matches explainable and supportable.
    - Effect on other TODOs: Unblocks - It feeds MatcherEvidence fields, ProcessedImageRecord references, manifest rows, and diagnostic snapshots.
  - Industry standard:
    Large data matching pipelines store selected positive and negative evidence, not just the winning score, so operators can debug false matches.
  - Recommended solution:
    Retain top candidate evidence, rejected near-tie evidence, token/label sources, scores, weights, and explanation text.
  - Answer:

- [ ] Define numeric token combination rules: say when separate number tokens may be joined to match a FamilyID.
  - Impact:
    - Project progress: High - Numeric token joining is central to filename-to-FamilyID matching.
    - Effect on other TODOs: Blocks - It feeds edit-distance scoring, length penalty, false-positive filters, and score aggregation.
  - Industry standard:
    Identifier matching combines adjacent or ordered numeric fragments only under strict rules and records the combination cost.
  - Recommended solution:
    Join numeric tokens when their order in the filename is preserved, separators are non-semantic, and the combined length matches configured FamilyID patterns.
  - Answer:

- [ ] Define numeric edit-distance scoring: specify how character differences reduce a numeric match score.
  - Impact:
    - Project progress: High - Numeric scoring controls the strongest matching signal.
    - Effect on other TODOs: Unblocks - It supports aggregation, thresholds, and tie-breaking.
  - Industry standard:
    Numeric identifiers use stricter distance penalties than natural-language strings because small differences often mean different entities.
  - Recommended solution:
    Start exact numeric matches at 1.0 and subtract normalized edit distance, plus any token-combination penalty.
  - Answer:

- [ ] Define numeric length-penalty scoring: specify how shorter or longer numeric candidates are penalized.
  - Impact:
    - Project progress: High - Length penalties prevent partial serials from matching full FamilyIDs too strongly.
    - Effect on other TODOs: Influences - It affects thresholds, false-positive handling, and aggregation.
  - Industry standard:
    Identifier matchers penalize partial-length candidates aggressively unless configured patterns explicitly allow them.
  - Recommended solution:
    Apply a normalized length-difference penalty and reject candidates that cannot plausibly represent the configured FamilyID length.
  - Answer:

- [ ] Define numeric false-positive handling for dimensions: say how values like `800x1200` avoid matching product IDs.
  - Impact:
    - Project progress: High - Dimension false positives are common in image filenames and can corrupt product matches.
    - Effect on other TODOs: Influences - It supports numeric matching, threshold enforcement, and evidence quality.
  - Industry standard:
    Matchers classify token context before scoring identifiers so measurements, dimensions, and timestamps do not become entity keys.
  - Recommended solution:
    Detect dimension patterns such as `800x1200`, `800_1200px`, and width/height contexts and exclude them from FamilyID candidates.
  - Answer:

- [ ] Define numeric false-positive handling for dates: say how date-like tokens avoid matching product IDs.
  - Impact:
    - Project progress: Medium - Date filtering reduces common filename noise.
    - Effect on other TODOs: Influences - It improves numeric candidate quality and evidence explanations.
  - Industry standard:
    Identifier matchers suppress date-like patterns through format recognition and context words before candidate scoring.
  - Recommended solution:
    Exclude tokens matching common date formats or adjacent date words unless they exactly match a configured FamilyID and have stronger evidence.
  - Answer:

- [ ] Define numeric false-positive handling for units: say how values followed by `cm`, `kg`, `%`, or similar units are treated.
  - Impact:
    - Project progress: Medium - Unit filtering prevents measurements from becoming product IDs.
    - Effect on other TODOs: Influences - It supports numeric matching and mixed column matching.
  - Industry standard:
    Tokenizers classify numbers with units as measurements and route them away from identity matching unless explicitly configured.
  - Recommended solution:
    Treat unit-adjacent numeric tokens as measurement tokens and exclude them from FamilyID matching by default.
  - Answer:

- [ ] Define string normalization: say how casing, accents, punctuation, separators, and whitespace are normalized before matching.
  - Impact:
    - Project progress: High - String normalization is required before any categorical or descriptive matching can be stable.
    - Effect on other TODOs: Blocks - It gates categorical, descriptive, mixed, language, and stop-word handling.
  - Industry standard:
    Text matching pipelines normalize case, accents, punctuation, separators, and whitespace while preserving original text for evidence.
  - Recommended solution:
    Normalize to lowercase accent-folded tokens, split separators consistently, collapse whitespace, and keep original token text in evidence.
  - Answer:

- [ ] Define categorical column matching: say how short low-cardinality product values influence filename matching.
  - Impact:
    - Project progress: High - Categorical columns provide strong supporting evidence such as color, type, and material.
    - Effect on other TODOs: Influences - It depends on string normalization and feeds aggregation and evidence.
  - Industry standard:
    Low-cardinality catalog attributes are useful match features but should support identity evidence rather than override exact identifiers.
  - Recommended solution:
    Score categorical matches with high supporting weight when normalized filename tokens match product attributes, especially color/type/material.
  - Answer:

- [ ] Define descriptive column matching: say how long product descriptions are searched without overwhelming stronger evidence.
  - Impact:
    - Project progress: Medium - Descriptive text can add useful context but is noisy.
    - Effect on other TODOs: Influences - It relies on stop words, language handling, aggregation, and evidence retention.
  - Industry standard:
    Long text fields are tokenized and down-weighted so broad descriptions do not swamp precise identifiers or categorical evidence.
  - Recommended solution:
    Search normalized description tokens with low weight, require multiple meaningful token matches, and never let descriptions override numeric identity.
  - Answer:

- [ ] Define mixed column matching: say how columns containing letters and digits feed both numeric and string matchers.
  - Impact:
    - Project progress: Medium - Mixed fields often contain supplier SKUs and product labels useful for matching.
    - Effect on other TODOs: Influences - It depends on numeric and string normalization and feeds score aggregation.
  - Industry standard:
    Mixed identifiers are split into numeric and text components while preserving the original combined value as evidence.
  - Recommended solution:
    Tokenize mixed columns into numeric and string evidence streams, score each through its matcher, and retain combined source evidence.
  - Answer:

- [ ] Define language handling: say whether strings are matched language-agnostically or through configured language rules.
  - Impact:
    - Project progress: Medium - Multilingual supplier data needs predictable matching without overfitting to one language.
    - Effect on other TODOs: Influences - It affects stop words, categorical matching, descriptive matching, and image-label prompts.
  - Industry standard:
    Multilingual aggregators start with language-agnostic normalization and add configured dictionaries for high-value domains such as colors and product types.
  - Recommended solution:
    Use language-agnostic token normalization by default and support configured multilingual synonym lists for known product attributes.
  - Answer:

- [ ] Define stop word handling: say which common words are ignored and where that list is configured.
  - Impact:
    - Project progress: Medium - Stop words improve string matching precision.
    - Effect on other TODOs: Influences - It affects descriptive columns, language handling, and evidence scoring.
  - Industry standard:
    Text matchers keep stop-word lists configurable and domain-specific to avoid dropping meaningful product terms.
  - Recommended solution:
    Store stop words in a matching-local config with language/domain groups and keep ignored tokens visible in debug evidence when diagnostics are enabled.
  - Answer:

- [ ] Define image-label trigger conditions: say when Prism runs vision labeling instead of relying only on filename and Excel text.
  - Impact:
    - Project progress: High - Labeling is expensive and should run when it adds decision value.
    - Effect on other TODOs: Blocks - It affects ONNX use, emitted labels, aggregation, progress timing, and diagnostics.
  - Industry standard:
    Large media pipelines gate expensive ML inference based on uncertainty, data availability, or configured quality requirements.
  - Recommended solution:
    Run image labeling when filename/Excel evidence is ambiguous, when descriptive/color matching needs visual corroboration, or when classification traits are required for transform/order.
  - Answer:

- [ ] Define emitted image labels: list the label categories expected from `ImageLabelingMatcher`.
  - Impact:
    - Project progress: High - Label categories define how vision evidence enters matching and transform.
    - Effect on other TODOs: Blocks - It feeds classification tag output, matcher evidence, ordering hints, and ONNX prompts.
  - Industry standard:
    Vision matchers emit a bounded taxonomy of labels with confidence and prompt/model provenance.
  - Recommended solution:
    Emit labels for object type, clothing/product category, color, orientation, human presence, background, and detail/ambiance cues.
  - Answer:
