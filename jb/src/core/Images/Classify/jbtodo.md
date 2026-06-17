# Image Classification Todo


## ONNX session scope

- [ ] ONNX `InferenceSession` is created per pipeline run, not application-scoped — may contradict `AGENTFEEDBACK.md`.
  - File: `jb/src/core/Pipeline/StageShells.cs` `ClassifyStageShell.Run()` — `ImageClassifier` is instantiated inside `Run` with `using ImageClassifier classifier = new()` followed by `InitializeClassifier(classifier)`.
  - `AGENTFEEDBACK.md` note (if present) states sessions are "application-scoped." Current code creates a fresh session per job and disposes it when the job ends.
  - Trade-off: Per-job sessions are safe (no cross-job state) but add ONNX model load time per job (~100–500 ms). Application-scoped sessions would amortize load cost but require thread-safe session management across concurrent jobs.
  - Decision needed: Accept per-job session lifecycle (current), or move to application-scoped singleton session with thread-safe access?
  - Answer: move to application-scoped singleton session with thread-safe access



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

- [ ] interior-shot phenotype is silently unreachable in CPU-only mode.
  - File: `jb/src/core/ImageNGP/ImageRoles.json` — interior-shot entry requires `packaging-visible = false`.
  - Issue: `packaging-visible` is always UNKNOWN in CPU-only mode. UNKNOWN never satisfies a condition in PhenotypeRuleSet. No CLIP prompt or analyzer currently writes `packaging-visible`. interior-shot can never be assigned.
  - Fix: Either add a CLIP prompt or analyzer that writes `packaging-visible`, or change the interior-shot phenotype rule to not require it. Requires user decision.

- [ ] Code stub: `RecordUnknownFeatures()` in `ImageFeatureAnalyzer.cs` marks 35+ features as UNKNOWN.
  - File: `jb/src/core/Images/Classify/ImageFeatureAnalyzer.cs` lines 195–235.
  - Block: These features require a CLIP-backed classifier or specialized detectors that are not yet wired in. The open todos above (ImageNGP taxonomy definition and `illustration-technical-drawing` scope) must be resolved first — they determine which features need CLIP prompts and which need separate detectors.
  - Fix: After taxonomy and role todos are answered, replace each `SetUnknownIfNotSet` call with a real measurement call to the appropriate analyzer (CLIP classifier for semantic features like `hero-is-human`, `hero-orientation`, `product-type-label`; specialized detectors for `salient-bbox`, `dominant-colors`, `pose-type`, etc.). Features with no planned analyzer keep `SetUnknownIfNotSet` until a detector is available.

- [ ] Phenotype production validation: define the protocol and acceptance criteria required before phenotype assignment can be trusted in production.
  - Issue: The 26 phenotypes in `imagePhenotypes.md` were defined from spec and taxonomy documentation without real-image testing. Production-quality assignment requires validation against a representative labeled image set. Currently most features are UNKNOWN (see RecordUnknownFeatures stub), so phenotype assignment is unreliable for any image where CLIP evidence is needed.
  - What is needed for production readiness:
    1. A labeled validation set — minimum ~100 images per major phenotype category across the product types in `DetOrderRules.json` (packshots, ghost images, on-model, lifestyle, technical drawings).
    2. All four Tx class stubs must be implemented so that transform routing can be validated end-to-end, not just assignment.
    3. The `RecordUnknownFeatures()` stub must be replaced with real measurements so phenotype rules fire from actual signals.
    4. Known rule bugs fixed: `ghost-front`/`front-packshot` ordering bug corrected; `illustration-technical-drawing` scope resolved.
    5. A confusion matrix showing predicted vs. expected phenotype per image, with per-class precision and recall.
    6. CLIP confidence thresholds tuned per feature (not uniform) to minimize misassignment on the validation set.
    7. Edge-case pass: ghost images, extreme orientations, lifestyle images, and illustrations all assign a correct or null phenotype — no silent misassignment to a wrong category.
  - Acceptance criteria: < 5% misassignment rate on the labeled validation set across all 26 phenotypes, with no systematic error pattern on any single phenotype category.
  - Fix: Schedule a validation sprint after RecordUnknownFeatures() and the ghost-front ordering bug are resolved. Build or curate the labeled image set, run the pipeline, measure assignment accuracy, and tune thresholds iteratively.
