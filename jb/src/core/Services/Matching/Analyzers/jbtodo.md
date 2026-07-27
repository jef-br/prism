# Analyzers Todo

TOC of per-analyzer working documents. Each analyzer has its own md file next to its .cs with
proposed workings, open questions, and calibration plans. An unchecked box means the analyzer
still has open work (implementation for stubs, calibration for implemented ones).

## Implemented — calibration/validation open

- [ ] [Analyzer_ProductType](Analyzer_ProductType.md) — IEM producttype/ngp → canonical slug; vocabulary unification + term-collision audit open
- [ ] [Analyzer_FilenameEvidence](Analyzer_FilenameEvidence.md) — filename tokens → product type + orientation; token list to config?
- [ ] [Analyzer_HasHuman](Analyzer_HasHuman.md) — YOLO person → has-human/human-count; partial-body recall to validate
- [ ] [Analyzer_SubjectGeometry](Analyzer_SubjectGeometry.md) — YOLO subject box → geometry features; segmentation-model milestone for true coverage
- [ ] [Analyzer_DominantColors](Analyzer_DominantColors.md) — 4 fg buckets, bg+skin excluded; white-on-white and skin-colored-product cases to calibrate
- [ ] [Analyzer_ProductColor](Analyzer_ProductColor.md) — largest fg bucket → palette name; palette granularity + LAB distance open
- [ ] [Analyzer_BackgroundColor](Analyzer_BackgroundColor.md) — SOLIDCOLOR border mean → palette name; gradient backgrounds open
- [ ] [Analyzer_Exposure](Analyzer_Exposure.md) — luminance flags, bg excluded; FlaggedFraction to calibrate
- [ ] [Analyzer_MultipleProducts](Analyzer_MultipleProducts.md) — YOLO counts; shoe-pair false positives to handle
- [ ] [Analyzer_Interior](Analyzer_Interior.md) — cavity detection; product-type-gating doc discrepancy to reconcile
- [ ] [Analyzer_IsIllustration](Analyzer_IsIllustration.md) — 3-signal topology; stale doc path to fix

## Removed (deferred pending future re-introduction)

T-4700 deleted these 10 empty-body stubs, and the 22 features they would have written (23
including `type-of-shot`, which was removed alongside them for the same UNKNOWN-forever reason
despite never having even a stub producer), because `PhenotypeRuleSet` never treats `UNKNOWN` as
satisfying a required condition — a stub-only feature makes every phenotype that hard-requires it
permanently unreachable, not just "not yet calibrated." Re-introduction is gated on Ticket B's
5-product-type catch-all proving reliable first (see `project_imagengp_phenotype_simplification`
memory / T-4700's ticket note); pick these
back up one at a time via git history for the proposed workings, not all at once:

- `Analyzer_FacePose` — would have set has-head, head-visible, has-face, face-visible,
  body-visible, pose-type from Haar cascades or yolo26s-pose in the person box; highest-value
  (unblocks most on-model phenotype nuance) if re-introduced.
- `Analyzer_TextPresent` — text-present via SWT/MSER heuristic or EAST/DBNet ONNX.
- `Analyzer_Mannequin` — contains-mannequin via person box + no skin + no face; depended on
  FacePose.
- `Analyzer_LogoPresent` — logo-present via compact high-contrast component heuristic.
- `Analyzer_CameraAngle` — camera-angle/top-view via box placement + shadow direction + CLIP
  prompts.
- `Analyzer_IndoorOutdoor` — indoor/outdoor via CLIP scene prompts, gated on lifestyle-background.
- `Analyzer_ShadowReflection` — shadow-present/reflection-present via a strip below the subject
  box on solid backgrounds.
- `Analyzer_Packaging` — packaging-visible/scale-reference-present via CLIP prompts + YOLO class
  support.
- `Analyzer_MaterialTexture` — material-texture-visible via HF energy at high crop-tightness.
- `Analyzer_LightingDetail` — lighting/lighting-detail via histogram shape + gradient coherence.

## Cross-cutting

- [ ] Retire ImageOrderer.ResolveProductType value-sniffing fallback once Analyzer_ProductType is validated on real batches (the refined ProductTypeId path already wins when set).
- [ ] Unify ProductTypeMap.json vocabulary with TranslationDictionary.json synonymGroups (domain productType) — or document why they stay separate (project reference direction blocks Classify → TranslationConfig).
- [ ] Segmentation model (yolo26s-seg): true product-coverage-ratio pixel masks; retires SubjectGeometry's color-distance fallback.
- Analyzer_Symmetry closed out for good: symmetry-score was consciously dropped (no phenotype rule ever consumed it) and the feature itself was removed from ImageNGP.json in T-4700 — no longer a "revisit if" item.
- [ ] CLIP-vs-analyzer write precedence: standardize the "only overwrite when higher confidence" convention (FilenameEvidence has it) for whichever stub analyzer is re-introduced first.