# Image Classification Todo

- [ ] Define image type classification values: list allowed values such as packshot, clothing, detail, ambiance, illustration, and unknown.
  - Impact:
    - Project progress: High - Image type controls matching evidence, transform behavior, ordering, and output quality rules.
    - Effect on other TODOs: Blocks - It gates transform-facing type output, ordering rules, and ImageClassificationTraits fields.
  - Industry standard:
    Vision pipelines use bounded enumerations for item type with confidence and unknown states so downstream stages can make deterministic decisions.
  - Recommended solution:
    Define allowed types as `packshot`, `clothing`, `detail`, `ambiance`, `illustration`, and `unknown`, with confidence per classification.
  - Answer:

- [ ] Define orientation classification values: list allowed values such as front, back, left, right, top, bottom, and unknown.
  - Impact:
    - Project progress: High - Orientation directly drives ordering and transform decisions.
    - Effect on other TODOs: Blocks - It feeds front/back ordering, `_det` assignment, and transform crop choices.
  - Industry standard:
    Product media classifiers use controlled orientation labels with explicit unknown when evidence is weak.
  - Recommended solution:
    Define `front`, `back`, `left`, `right`, `top`, `bottom`, `threeQuarter`, and `unknown`.
  - Answer:

- [ ] Define border intersection detection method: say how Prism decides that content touches top, right, bottom, or left.
  - Impact:
    - Project progress: High - Border intersections alter crop, margin, and background extension policy.
    - Effect on other TODOs: Blocks - It feeds detail crop anchors, center-and-stretch behavior, and transform failure decisions.
  - Industry standard:
    Image preprocessors compute object bounds against image edges and record edge-contact flags with tolerance to account for antialiasing and shadows.
  - Recommended solution:
    Use salient object bounds plus edge tolerance to emit boolean top/right/bottom/left intersection flags with confidence.
  - Answer:

- [ ] Define human detection method: say which model or heuristic decides whether a person is visible.
  - Impact:
    - Project progress: High - Human detection affects clothing transforms, headcut logic, and classification traits.
    - Effect on other TODOs: Unblocks - It supports head visibility detection, transform tag output, and type classification.
  - Industry standard:
    Human-presence classification combines model labels or detectors with confidence thresholds rather than relying only on color heuristics.
  - Recommended solution:
    Use image-label/classification model evidence as the primary signal and optionally combine it with lightweight skin/silhouette heuristics as supporting evidence.
  - Answer:

- [ ] Define head visibility detection method: say how eyes, nose, ears, face region, or crop position proves head visibility.
  - Impact:
    - Project progress: Medium - Head visibility is important for clothing crop behavior but depends on human and border detection.
    - Effect on other TODOs: Influences - It affects detail crop headcut behavior and ImageClassificationTraits fields.
  - Industry standard:
    Face/head visibility should be model-backed where possible and represented as confidence plus reason, because partial people are common in product imagery.
  - Recommended solution:
    Detect face/head landmarks or labels when available, fall back to top-edge and human-presence evidence, and emit visible/unknown with confidence.
  - Answer:

- [ ] Define classification confidence values: say whether traits use booleans, percentages, or both.
  - Impact:
    - Project progress: Medium - Confidence representation controls how downstream stages decide between action and unknown.
    - Effect on other TODOs: Unblocks - It informs ImageClassificationTraits, unknown states, transform fallbacks, and ordering hints.
  - Industry standard:
    ML-assisted pipelines keep both normalized decision values and confidence scores so rules can use thresholds while UIs show explainability.
  - Recommended solution:
    Store typed values plus confidence from 0.0 to 1.0 for each trait.
  - Answer:

- [ ] Define unknown classification states: say how uncertain or unavailable classification is represented.
  - Impact:
    - Project progress: Medium - Unknown handling prevents weak model output from becoming false certainty.
    - Effect on other TODOs: Influences - It affects transform fallback, unknown ordering, matcher labels, and diagnostics.
  - Industry standard:
    Production classifiers represent unavailable, unsupported, and below-threshold results explicitly instead of defaulting to false.
  - Recommended solution:
    Use explicit `unknown` values with reason codes such as `below-threshold`, `not-run`, or `model-unavailable`.
  - Answer:
