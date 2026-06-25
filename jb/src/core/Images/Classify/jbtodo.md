# Image Classification Todo

-------
- [ ] HANDMADE BY ME: Temporarily GATE the phenotypes so we can get basic transformations online.
  - Status: gate implemented as `ImageTransformer.BypassPhenotypes` (currently `true`). While on, transform routing ignores `SelectedPhenotype` and decides off geometry only (`salient-bbox` + edge intersects): bbox present + no intersect → `Tx_CenterAndStretch`; bbox + intersect → `Tx_CropSquare`; no bbox → `Tx_ProblemImageProcessor`. `Tx_DetailCropper` (phenotype-driven) is unreachable while bypassing. Flip the flag to `false` once phenotype assignment is validated; this todo stays open until then.


-------
- [ ] Define final ImageNGP taxonomy and feature combinations: list all possible ImageNGPs and the ImageFeature values required to derive each phenotype.
  - Impact:
    - Project progress: High - ImageFeatures and ImageNGPs control matching evidence, transform behavior, DetOrder assignment, and output quality rules.
    - Effect on other TODOs: Blocks - It gates ordering rules, `ImageNGP.cs` fields, `ImageRecord_LAMBDA.cs` fields, transform-facing phenotype use, and unknown-state handling for derived phenotypes.
  - Industry standard:
    Vision pipelines keep measured attributes separate from derived image phenotypes, then document the feature combinations that produce each phenotype so downstream stages can make deterministic decisions.
  - Recommended solution:
    Use the accepted `jb/docs/PRISM-classify.md` decision as the baseline: ImageFeatures are measured attributes with source/confidence/unknown state, and `ImageNGP` is a phenotype derived from combinations of ImageFeatures rather than a single `TypeOfShot` list. Complete this todo by listing the concrete ImageNGP values and their required feature combinations.

  - Answer (proposed pointer from existing data — PRISM-classify.md "Taxonomy & Prompt Configuration"; PRISM-index.md File Map; pending approval):
    The enumerated taxonomy already exists as accepted artifacts — this todo is reconciliation/transcription, not net-new design:
      - Canonical machine source: `jb/src/core/ImageNGP/ImageNGP.json` — every IF id with datatype/allowed values, plus the 26-phenotype catalogue (the runtime authority; `ImageNgpValidator` fails fast on any drift).
      - IF→phenotype feature combinations: `jb/src/core/ImageNGP/ImageRoles.json` (first-match rules).
      - Human-readable definitions: `jb/docs/ImageNGP/imagePhenotypes.md` (26 phenotypes) and `jb/docs/ImageNGP/PRODUCTTYPES.md`; IF catalog in `jb/docs/ImageNGP/ImageFeatures.md` (40 IFs).
    Recommended close-out: confirm `ImageNGP.json` ↔ `imagePhenotypes.md` ↔ `ImageRoles.json` agree on the 26 phenotypes and their required IF combinations, then record that reconciled list here. (No new phenotypes should be invented in this step.) NOTE: the `illustration-technical-drawing` scope todos below must be settled as part of confirming the feature combinations.

-------
- [ ] Resolve whether `illustration-technical-drawing` should remain a broad catch-all or require additional conditions.
  - Impact:
    - Project progress: Medium — once CLIP provides `hero-is-human = FALSE` for real images, every non-human image that does not match any earlier rule (including plain products with unusual or ambiguous features) will be silently assigned `illustration-technical-drawing`. This is almost certainly wrong for most of those images.
    - Effect on other TODOs: Affects DetOrder slot assignment (the phenotype is documented to always receive the last configured det slot) and transform routing. Misclassification here directly degrades ordering quality.
  - Industry standard:
    Catch-all rules in phenotype taxonomies are either placed at the very bottom and clearly scoped (e.g. "all remaining lifestyle images → lifestyle-context") or gated by a positive signal (e.g. a CLIP prompt confidence for "graphic/schematic rendering"). A rule that means "graphic/schematic" but fires for any non-human image is an unscoped catch-all masquerading as a specific phenotype.
  - Recommended solution:
    Either (a) add CLIP-based conditions to tighten the rule (e.g. require a classification token above threshold for "technical drawing", "vector illustration", or "schematic"), or (b) replace the current rule with a null assignment so unrecognized non-human images get no phenotype and are handled by deterministic fallback in the Ordered stage. Option (b) is safer until the CLIP-based signal is proven reliable.
  - Answer (proposed recommendation, decision still yours — grounded in PRISM-classify.md "UNKNOWN States" (below-threshold → UNKNOWN, never default) and current impl where most IFs are UNKNOWN/no CLIP prompt writes a "schematic" token; pending approval):
    Existing data favours option (b) for now: there is currently no CLIP prompt or analyzer that writes a positive "technical drawing / vector illustration / schematic" signal, so an unscoped catch-all firing on any non-human image would systematically misclassify plain products once `hero-is-human = FALSE` becomes available. Replacing it with a null/no-phenotype assignment keeps unrecognized non-human images in the deterministic Ordered-stage fallback (consistent with the docs' rule that absent evidence stays UNKNOWN rather than defaulting). Option (a) becomes the preferred long-term fix only after a dedicated CLIP prompt for "schematic/technical drawing" is added and proven on the validation set — which is new data, so it is out of scope for this pass. Final pick is your call.

-------
- [ ] interior-shot phenotype is silently unreachable in CPU-only mode.
  - File: `jb/src/core/ImageNGP/ImageRoles.json` — interior-shot entry requires `packaging-visible = false`.
  - Issue: `packaging-visible` is always UNKNOWN in CPU-only mode. UNKNOWN never satisfies a condition in PhenotypeRuleSet. No CLIP prompt or analyzer currently writes `packaging-visible`. interior-shot can never be assigned.
  - Fix:
    - add producttype requirements to interior-shot:
      - has to be a wallet, bag, suitcases or similar
      - cannot be clothing
    - Create an Analyzer class for interiors:
      - Core Pattern: the image must satisfy
          1. Large enclosed region.
          2. Surrounded by a strong boundary.
          3. Contained within a larger foreground object.
          4. Number of connections with the image border add confidence.
          5. Interior differs from its surroundings.
          Topology:Background > Foreground Object > Boundary (zipper / seam / opening) > Interior Region
        
      - Do not attempt to recognize bags, zippers, leather, fabric, or handles.
      - Treat it as geometric and topological analysis: "A large cavity enclosed by a strong boundary and contained within a larger object."

      - Analyzer Pipeline:
        1. Canny Edge map > dilate to close gaps > Extract closed contours ignoring tiny contours
        2. Per contour: Compute enclosed mask, area, perimeter, mean edge strength along contour.
        3. For each enclosed region: Measure texture, color variance, brightness, and distance to image border. > Measure surroundings. Expand region outward by N pixels > Compute texture/color statistics in the surrounding ring > Compute edge density in the surrounding ring.

      - Interior Candidate Requirements:
        ✓ Region is enclosed
        ✓ Region area exceeds threshold
        ✓ Boundary strength exceeds threshold

      - confidence enhancers: 
        ✓ Interior texture < surrounding texture.
        ✓ Interior variance < surrounding variance.
        ✓ Region lies inside a larger foreground component.
        ✓ Boundary forms a substantial closed loop.

      - Scoring: AreaScore + EnclosureScore + BoundaryStrengthScore + InteriorVsSurroundingTextureScore+ ForegroundContainmentScore + BorderDistanceScore

      - Reject if: Area too small, Boundary weak, pr  No larger enclosing foreground object exists

      - Usage: regardless of detorder, fires for images with a qualifying producttype in the FamilyID excel column. CLIP labels don't matter here (might not have been recognized) If a det0 or det1 gets tagged as interior-shot, it gets bumped to a position after det1 it qualifies for, or added to the end.

-------
- [ ] Code stub: `RecordUnknownFeatures()` in `ImageFeatureAnalyzer.cs` marks 35+ features as UNKNOWN.
  - File: `jb/src/core/Images/Classify/ImageFeatureAnalyzer.cs` lines 195–235.
  - Block: These features require a CLIP-backed classifier or specialized detectors that are not yet wired in. The open todos above (ImageNGP taxonomy definition and `illustration-technical-drawing` scope) must be resolved first — they determine which features need CLIP prompts and which need separate detectors.

  - Fix: After taxonomy and role todos are answered, replace each `SetUnknownIfNotSet` call with a real measurement call to the appropriate analyzer (CLIP classifier for semantic features like `hero-is-human`, `hero-orientation`, `product-type-label`; specialized detectors for `salient-bbox`, `dominant-colors`, `pose-type`, etc.). Features with no planned analyzer keep `SetUnknownIfNotSet` until a detector is available.
  - Answer:

-------
- [ ] Phenotype production validation: define the protocol and acceptance criteria required before phenotype assignment can be trusted in production.
  - Issue: The 26 phenotypes in `imagePhenotypes.md` were defined from spec and taxonomy documentation without real-image testing. Production-quality assignment requires validation against a representative labeled image set. Currently most features are UNKNOWN (see RecordUnknownFeatures stub), so phenotype assignment is unreliable for any image where CLIP evidence is needed.
  - What is needed for production readiness:
    1. A labeled validation set — minimum ~100 images per major phenotype category across the product types in `DetOrderRules.json` (packshots, ghost images, on-model, lifestyle, technical drawings).
    2. All four Tx class stubs must be implemented so that transform routing can be validated end-to-end, not just assignment.
    3. The `RecordUnknownFeatures()` stub must be replaced with real measurements so phenotype rules fire from actual signals.
    4. `illustration-technical-drawing` scope resolved.
    5. A confusion matrix showing predicted vs. expected phenotype per image, with per-class precision and recall.
    6. CLIP confidence thresholds tuned per feature (not uniform) to minimize misassignment on the validation set.
    7. Edge-case pass: ghost images, extreme orientations, lifestyle images, and illustrations all assign a correct or null phenotype — no silent misassignment to a wrong category.
  - Acceptance criteria: < 5% misassignment rate on the labeled validation set across all 26 phenotypes, with no systematic error pattern on any single phenotype category.

  - Fix: Schedule a validation sprint after RecordUnknownFeatures() is resolved. Build or curate the labeled image set, run the pipeline, measure assignment accuracy, and tune thresholds iteratively.
