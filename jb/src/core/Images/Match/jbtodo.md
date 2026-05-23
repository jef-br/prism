# Image Matching Todo

- [ ] Define remaining matcher score aggregation rules: specify the aggregation formula for string evidence, image-label evidence, and non-exact numeric evidence after exact 8-digit FamilyID matches and ordering-token usage are accounted for.
  - Impact:
    - Project progress: High - The remaining aggregation formula decides non-exact product matches for every image.
    - Effect on other TODOs: Blocks - It gates threshold enforcement, tie-breaking, matcher evidence, output naming, and KO unmatched policy for candidates that are not exact 8-digit FamilyID matches.
  - Industry standard:
    Matching systems combine specialized signals with explicit weights, thresholds, and evidence retention so decisions are reproducible and tunable.
  - Recommended solution:
    Define an explicit weighted formula where exact numeric FamilyID evidence remains authoritative, string evidence supports candidates, and image labels act as secondary corroboration. Build on the completed exact-threshold decision and completed image-label weights in `MatchingConfig.json`, and explicitly define how ordering-only tokens from `DetOrderRules.json` are excluded from FamilyID aggregation.
  - Answer:
    - Today, all FamilyIDs are 8-digit numbers. Therefore, today, numerical tokens of length 8 score with an edit distance of 0 have 100 confidence.
    - String tokens serve 2 purposes:
      - match the image to a familyID in IEM (Internal Excel Model) via a column other than the FamilyID column
      - influence the suffix of the new filename the image will receive after it has been associated to a single FamilyID.
        - This happens if a string token matches with a keyword found in `jb\src\core\Images\Order\DetOrderRules.json`. The json document lists the order in which an image whose original filename contains a string token found in the list should appear. This is an intermediate solution. The improvement on this would be for this list and the ImageNGP.json sitting next to be used for both these purposes. (ordering as well as deciding and configuring image transformations)

- [ ] Define matcher tie-breaking: say how Prism chooses between products with equal or near-equal scores.
  - Impact:
    - Project progress: High - Tie-breaking prevents nondeterministic product assignments.
    - Effect on other TODOs: Blocks - It influences `MatchEvidence` tie state, output naming, and KO policy.
  - Industry standard:
    Matching systems avoid arbitrary ties; they either apply deterministic secondary evidence or reject ambiguous candidates for review.
  - Recommended solution:
    Break ties with exact numeric evidence, then stronger filename token provenance, then image-label corroboration; KO near-equal unresolved ties.
  - Answer:
    - The image matching should happen in multiple brackets like a tournament:
      - First bracket of the tournament is a very strict exact matching round.
        - all identical single-token numerical matches to familyID are associated to a familyID.
        - the image count per family is not checked yet.
      - Second bracket of the tournament allows slack:
        - multi-token matches are now allowed to match with unmatched familyIDs
      - Third bracket of the tournament filters all likely matches:
        - images where a single familyID remains when using non-numerical tokens get matched
      - Fourth bracket of the tournmant cleans up:
        - Remaining images get a chance to match with familyIDs that have zero images associated with them using all available information. The match is accepted only if the evidence confidence is very high and only one familyID candidate was found for that image.
      - Once the competition is over, all images resolve to their single familyID
      - The whole competition revolves around the modelling of 3 parameters:
        - Image parameters: This is called `ImageNGP`: what an image is showing (head? human? front? back? packshot? ...) The ImageNGP parameters resolve the image to an image phenotype
        - Order Schemes: `DetOrderRules.json` how images should be sorted in the family, given the image ImageNGP and the product type.
        - Product type map: A map of where a specific imagetype can exist per product type
        ``` json
          {
            "orderscheme": {
              "default": {
                  "det0": { "ImageNGP": "front", "IsProductInFullView": true},
                  "det1": { "ImageNGP": "back" },
                  "det2": { "ImageNGP": "detail"},
                  "det3": { "ImageNGP": "side"},
                  "det4": { "ImageNGP": "lifestyle" } 
              },
              "shoes": {
                  "det0": { "ImageNGP": "front", "IsProductInFullView": true},
                  "det1": { "ImageNGP": "side" },
                  "det2": { "ImageNGP": "back" }
              },
              "clothing": {
                  "det0": { "ImageNGP": "front", "IsProductInFullView": true},
                  "det1": { "ImageNGP": "back" },
                  "det2": { "ImageNGP": "detail" }
              },
              "fragrance": {
                  "det0": { "ImageNGP": "packshot", "IsProductInFullView": true },
                  "det1": { "ImageNGP": "front"},
                  "det2": { "ImageNGP": "lifestyle" },
                  "det3": { "ImageNGP": "detail"} 
              }
            }
          }
        ```
          - The structure of the scheme is accurate.
          - The concrete values per producttype and ImageNGP are a sketch of what the final mapping will look like. That is something for later.
          - Gaps in the det-order are allowed here and will be closed when the entire image collection is actually renamed



- [ ] Define numeric false-positive handling for dimensions, dates, and units: say how measurement-like, date-like, and unit-adjacent numbers avoid matching product IDs.
  - Impact:
    - Project progress: High - Numeric noise filtering prevents dimensions, dates, and measurements in filenames from corrupting product matches.
    - Effect on other TODOs: Influences - It supports numeric matching, threshold enforcement, mixed column matching, and evidence quality.
  - Industry standard:
    Matchers classify token context before scoring identifiers so measurements, dimensions, dates, timestamps, percentages, and unit values do not become entity keys.
  - Recommended solution:
    Detect dimension patterns such as `800x1200`, common date formats, adjacent date words, and unit-adjacent numbers such as `cm`, `kg`, or `%`; exclude those tokens from FamilyID matching by default unless explicitly configured otherwise.
  - Answer:
    - **Numeric noise filtering does not happen during matching.**
      - Filenames are tokenized by `Importer.cs` which sends the filename to `FilenameTokenizer.cs` and receives a set of tokens
      - 
      - For Excel data: filtering needs to happen by the `NoiseFilter.cs` strategy class loaded by `jb\src\core\Excel\ModelBuilder.cs` The NoiseFilter should use regex patterns loaded from the `ExcelConfig.json` file. The patterns are stored as strings under "NoiseFilterPatterns" where each entry is a key/value "name of pattern" / "escaped regex pattern as a string"
    - **Numeric noise filtering happens when normalizing the media source**
    - 

- [ ] Define descriptive column matching: say how long product descriptions are searched without overwhelming stronger evidence.
  - Impact:
    - Project progress: Medium - Descriptive text can add useful context but is noisy.
    - Effect on other TODOs: Influences - It relies on stop words, language handling, aggregation, and evidence retention.
  - Industry standard:
    Long text fields are tokenized and down-weighted so broad descriptions do not swamp precise identifiers or categorical evidence.
  - Recommended solution:
    Search normalized description tokens with low weight, require multiple meaningful token matches, and never let descriptions override numeric identity. Define the descriptive-column rule source before implementation, because the current matching config only names numeric fields and product color/type/material/image-label rules.
  - Answer:

- [ ] Define mixed column matching: say how columns containing letters and digits feed both numeric and string matchers.
  - Impact:
    - Project progress: Medium - Mixed fields often contain supplier SKUs and product labels useful for matching.
    - Effect on other TODOs: Influences - It depends on numeric and string normalization and feeds score aggregation.
  - Industry standard:
    Mixed identifiers are split into numeric and text components while preserving the original combined value as evidence.
  - Recommended solution:
    Tokenize mixed columns into numeric and string evidence streams, score each through its matcher, and retain combined source evidence. Define how mixed columns are discovered or configured, because current matching config does not yet name a generic mixed-column rule.
  - Answer:

- [ ] Define language handling: say whether strings are matched language-agnostically or through configured language rules.
  - Impact:
    - Project progress: Medium - Multilingual supplier data needs predictable matching without overfitting to one language.
    - Effect on other TODOs: Influences - It affects stop words, categorical matching, descriptive matching, and image-label prompts.
  - Industry standard:
    Multilingual aggregators start with language-agnostic normalization and add configured dictionaries for high-value domains such as colors and product types.
  - Recommended solution:
    Use language-agnostic token normalization by default and support configured multilingual synonym lists for known product attributes. Keep matching synonyms separate from the current multilingual ordering tokens in `DetOrderRules.json` unless a shared taxonomy is explicitly defined.
  - Answer:

- [ ] Define stop word handling: say which common words are ignored and where that list is configured.
  - Impact:
    - Project progress: Medium - Stop words improve string matching precision.
    - Effect on other TODOs: Influences - It affects descriptive columns, language handling, and evidence scoring.
  - Industry standard:
    Text matchers keep stop-word lists configurable and domain-specific to avoid dropping meaningful product terms.
  - Recommended solution:
    Store stop words in a matching-local config with language/domain groups and keep ignored tokens visible in debug evidence when diagnostics are enabled. No matching-local stop-word config exists yet, so this todo should name the config location and default groups before implementation.
  - Answer:
