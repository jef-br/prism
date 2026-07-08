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

## Stubs — implementation open

- [ ] [Analyzer_FacePose](Analyzer_FacePose.md) — Haar cascades or yolov8n-pose in the person box; highest-value stub (6 features, unblocks most on-model phenotypes)
- [ ] [Analyzer_TextPresent](Analyzer_TextPresent.md) — SWT/MSER heuristic or EAST/DBNet ONNX; unblocks size-chart
- [ ] [Analyzer_Mannequin](Analyzer_Mannequin.md) — person box + no skin + no face; depends on FacePose
- [ ] [Analyzer_LogoPresent](Analyzer_LogoPresent.md) — compact high-contrast component heuristic
- [ ] [Analyzer_CameraAngle](Analyzer_CameraAngle.md) — box placement + shadow direction + CLIP prompts
- [ ] [Analyzer_IndoorOutdoor](Analyzer_IndoorOutdoor.md) — CLIP scene prompts, gated on lifestyle-background
- [ ] [Analyzer_ShadowReflection](Analyzer_ShadowReflection.md) — strip below subject box on solid backgrounds
- [ ] [Analyzer_Packaging](Analyzer_Packaging.md) — CLIP prompts + YOLO class support
- [ ] [Analyzer_MaterialTexture](Analyzer_MaterialTexture.md) — HF energy at high crop-tightness
- [ ] [Analyzer_LightingDetail](Analyzer_LightingDetail.md) — histogram shape + gradient coherence

## Cross-cutting

- [ ] Retire ImageOrderer.ResolveProductType value-sniffing fallback once Analyzer_ProductType is validated on real batches (the refined ProductTypeId path already wins when set).
- [ ] Unify ProductTypeMap.json vocabulary with TranslationDictionary.json synonymGroups (domain productType) — or document why they stay separate (project reference direction blocks Classify → TranslationConfig).
- [ ] Segmentation model (yolov8n-seg): true product-coverage-ratio pixel masks; retires SubjectGeometry's color-distance fallback.
- [ ] Analyzer_Symmetry was consciously DROPPED (no phenotype rule consumes symmetry-score; only plausible use was FRONT-orientation support). Revisit only if an orientation rule ever wants it.
- [ ] CLIP-vs-analyzer write precedence: standardize the "only overwrite when higher confidence" convention (FilenameEvidence has it; FacePose will need it).
