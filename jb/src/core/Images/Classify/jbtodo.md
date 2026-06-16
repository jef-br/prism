# Image Classification Todo

- [ ] Define final ImageNGP taxonomy and feature combinations: list all possible ImageNGPs and the ImageFeature values required to derive each phenotype.
  - Impact:
    - Project progress: High - ImageFeatures and ImageNGPs control matching evidence, transform behavior, DetOrder assignment, and output quality rules.
    - Effect on other TODOs: Blocks - It gates ordering rules, `ImageNGP.cs` fields, `ImageRecord_LAMBDA.cs` fields, transform-facing phenotype use, and unknown-state handling for derived phenotypes.
  - Industry standard:
    Vision pipelines keep measured attributes separate from derived image phenotypes, then document the feature combinations that produce each phenotype so downstream stages can make deterministic decisions.
  - Recommended solution:
    Use the accepted `jb/docs/PRISM-classify.md` decision as the baseline: ImageFeatures are measured attributes with source/confidence/unknown state, and `ImageNGP` is a phenotype derived from combinations of ImageFeatures rather than a single `TypeOfShot` list. Complete this todo by listing the concrete ImageNGP values and their required feature combinations.
  - Answer:

- [ ] Fix `ImageRoles.json` ordering bug: `ghost-front` is permanently unreachable because `front-packshot` appears before it and matches the same five conditions first.
  - Impact:
    - Project progress: Medium — invisible in CPU-only mode (hero-is-human is UNKNOWN so neither rule fires), but becomes a silent misclassification once CLIP provides `hero-is-human = FALSE`. Every ghost garment will be assigned `front-packshot` instead.
    - Effect on other TODOs: Directly affects DetOrder slot assignment (ghost vs. packshot may map to different slots in `DetOrderRules.json`) and transform behavior.
  - Industry standard:
    First-match-wins rule engines always place more-specific rules (more required conditions) before less-specific ones. `ghost-front` is a strict superset of `front-packshot` requirements; it must come first.
  - Recommended solution:
    In `jb/src/core/ImageNGP/ImageRoles.json`, move `ghost-front`, `ghost-back`, and `ghost-side` to appear immediately before their corresponding packshot variants (`front-packshot`, `back-packshot`, `side-packshot`). Update the corresponding assertion in `PhenotypeRuleSetTests.Assign_GhostFront_OrderingBug_CurrentlyReturnsFrontPackshot` from `"front-packshot"` to `"ghost-front"` after the fix.
  - Answer:

- [ ] Resolve whether `illustration-technical-drawing` should remain a broad catch-all or require additional conditions.
  - Impact:
    - Project progress: Medium — once CLIP provides `hero-is-human = FALSE` for real images, every non-human image that does not match any earlier rule (including plain products with unusual or ambiguous features) will be silently assigned `illustration-technical-drawing`. This is almost certainly wrong for most of those images.
    - Effect on other TODOs: Affects DetOrder slot assignment (the phenotype is documented to always receive the last configured det slot) and transform routing. Misclassification here directly degrades ordering quality.
  - Industry standard:
    Catch-all rules in phenotype taxonomies are either placed at the very bottom and clearly scoped (e.g. "all remaining lifestyle images → lifestyle-context") or gated by a positive signal (e.g. a CLIP prompt confidence for "graphic/schematic rendering"). A rule that means "graphic/schematic" but fires for any non-human image is an unscoped catch-all masquerading as a specific phenotype.
  - Recommended solution:
    Either (a) add CLIP-based conditions to tighten the rule (e.g. require a classification token above threshold for "technical drawing", "vector illustration", or "schematic"), or (b) replace the current rule with a null assignment so unrecognized non-human images get no phenotype and are handled by deterministic fallback in the Ordered stage. Option (b) is safer until the CLIP-based signal is proven reliable.
  - Answer:

- [ ] CLIP prompt format is key=value schema annotations, not natural language.
  - File: `jb/src/core/Pipeline/StageShells.cs` `BuildDefaultPrompts()`.
  - Issue: CLIP is a vision-language model expecting prompts like "a photo of a person wearing a shirt", not "hero-is-human=TRUE". The schema-annotation format produces semantically meaningless embeddings.
  - Block: No spec-prescribed prompt list exists. Requires user decision on what the CLIP prompts should be before this can be fixed.
  - Fix: Replace BuildDefaultPrompts() with natural-language prompts that map to the same feature values via TryParseFeatureToken or a new lookup table.

- [ ] interior-shot phenotype is silently unreachable in CPU-only mode.
  - File: `jb/src/core/ImageNGP/ImageRoles.json` — interior-shot entry requires `packaging-visible = false`.
  - Issue: `packaging-visible` is always UNKNOWN in CPU-only mode. UNKNOWN never satisfies a condition in PhenotypeRuleSet. No CLIP prompt or analyzer currently writes `packaging-visible`. interior-shot can never be assigned.
  - Fix: Either add a CLIP prompt or analyzer that writes `packaging-visible`, or change the interior-shot phenotype rule to not require it. Requires user decision.

- [ ] Code stub: `RecordUnknownFeatures()` in `ImageFeatureAnalyzer.cs` marks 35+ features as UNKNOWN.
  - File: `jb/src/core/Images/Classify/ImageFeatureAnalyzer.cs` lines 195–235.
  - Block: These features require a CLIP-backed classifier or specialized detectors that are not yet wired in. The open todos above (ImageNGP taxonomy definition and `illustration-technical-drawing` scope) must be resolved first — they determine which features need CLIP prompts and which need separate detectors.
  - Fix: After taxonomy and role todos are answered, replace each `SetUnknownIfNotSet` call with a real measurement call to the appropriate analyzer (CLIP classifier for semantic features like `hero-is-human`, `hero-orientation`, `product-type-label`; specialized detectors for `salient-bbox`, `dominant-colors`, `pose-type`, etc.). Features with no planned analyzer keep `SetUnknownIfNotSet` until a detector is available.
