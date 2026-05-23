# Image Ordering Todo

- [x] Define `_det` suffix assignment and output filename suffix rules: say whether numbering is always zero-based, whether gaps are allowed, and how `_det` numbers are assigned after image ordering.
  - Impact:
    - Project progress: High - Suffix assignment creates the final image sequence consumed by downstream systems.
    - Effect on other TODOs: Blocks - It gates output filename suffix rules, zip duplicate handling, manifest projection, export naming, and filename collision handling.
  - Industry standard:
    Product image pipelines assign deterministic, contiguous sequence numbers per product after all ordering evidence is evaluated.
  - Recommended solution:
    Use zero-based contiguous `_det` numbers per FamilyID with no gaps in OK output.
  - Answer: Det suffix is always zero-based. Order gaps are allowed between original images if images belonging to the family collection that can fulfill the role of det0, det1, or det2 (see imageNGP) can be copied and transformed into an image that can perform the role of the image that should have that order. When the renaming is performed, any remaining gaps are then closed.

- [ ] Define ordering tie-breakers: say how equal ordering evidence is resolved within one FamilyID.
  - Impact:
    - Project progress: High - Tie-breakers prevent nondeterministic output names.
    - Effect on other TODOs: Blocks - It affects suffix assignment, filename collision handling, and manifest reproducibility.
  - Industry standard:
    Ordering systems use deterministic secondary keys and record tie decisions so repeated runs produce identical outputs.
  - Recommended solution:
    Break ties by strongest filename hint, then vision confidence, then original filename sort order, then stable source index.
  - Answer:

- [ ] Define remaining front image ordering rules: say how `det0` tokens from `DetOrderRules.json`, sequence tokens, and `ImageNGP.HERO_ORIENTATION.FRONT` evidence are weighted toward `_det0`.
  - Impact:
    - Project progress: High - Front image selection determines the primary product image.
    - Effect on other TODOs: Blocks - It feeds suffix assignment, output naming, filename hint weights, and computer-vision hint influence using the completed orientation values.
  - Industry standard:
    Catalog pipelines prioritize explicit front-view metadata and high-confidence visual orientation for the first product image.
  - Recommended solution:
    Use the current `det0` token bucket as the configured filename source, then define sequence-token and `HERO_ORIENTATION.FRONT` weights against it.
  - Answer:

- [ ] Define remaining back image ordering rules: say how `det1` tokens from `DetOrderRules.json` and `ImageNGP.HERO_ORIENTATION.BACK` evidence identify a back view.
  - Impact:
    - Project progress: High - Back view rules establish the second major product view after front.
    - Effect on other TODOs: Influences - It affects suffix assignment, orientation labels, and tie-breakers.
  - Industry standard:
    Product media ordering uses explicit back-view tokens and visual orientation labels as strong evidence after primary/front images.
  - Recommended solution:
    Use the current `det1` token bucket as the configured filename source, then define `HERO_ORIENTATION.BACK` weight and placement after the selected front image.
  - Answer:

- [ ] Define remaining detail image ordering rules: say how `det3` and other detail-related buckets from `DetOrderRules.json` combine with `ImageNGP.TypeOfShot.DETAIL` after main views.
  - Impact:
    - Project progress: Medium - Detail ordering improves presentation quality after main view order is stable.
    - Effect on other TODOs: Influences - It uses image type labels, filename hints, and tie-breakers.
  - Industry standard:
    E-commerce pipelines place detail shots after main views using type labels, filename hints, and stable order.
  - Recommended solution:
    Use the current detail-related token buckets as configured filename sources, then define `TypeOfShot.DETAIL` confidence and original-source tie behavior.
  - Answer:

- [ ] Define ambiance image ordering rules: say how lifestyle, still-life, or contextual images are ordered within a FamilyID.
  - Impact:
    - Project progress: Medium - Ambiance ordering affects secondary presentation but not core matching.
    - Effect on other TODOs: Influences - It depends on `ImageNGP.TypeOfShot.LIFESTYLE`, `STILLIFE`, image type confidence, and unknown ordering.
  - Industry standard:
    Lifestyle or ambiance images are usually placed after product-specific views to preserve product clarity.
  - Recommended solution:
    Rank `LIFESTYLE` and `STILLIFE` images after main and detail views, preserving stable source order unless stronger configured hints exist.
  - Answer:

- [ ] Define unknown image ordering rules: say where images with weak, missing, or unavailable view/type evidence are placed.
  - Impact:
    - Project progress: Medium - Unknown handling prevents weak evidence from disrupting primary views.
    - Effect on other TODOs: Influences - It uses `HERO_ORIENTATION.UNKNOWN`, the missing unknown coverage for `TypeOfShot` and `HERO_HASHEAD`, tie-breakers, and suffix assignment.
  - Industry standard:
    Low-confidence media is ordered conservatively after high-confidence assets and flagged for review when necessary.
  - Recommended solution:
    Place unknown or unavailable-evidence images after known main/detail/ambiance groups and keep their uncertainty in `MatchEvidence` and manifest evidence.
  - Answer:

- [ ] Define filename hint influence weights: say how tokens already listed in `DetOrderRules.json` are weighted, how missing buckets are handled, and how future `ImageNGP.json` integration shares ordering and transform rules.
  - Impact:
    - Project progress: High - Filename hint weights decide how the existing token buckets affect ordering before vision evidence.
    - Effect on other TODOs: Influences - It feeds front/back/detail/ambiance rules, tie-breakers, and future `ImageNGP.json` rule sharing.
  - Industry standard:
    Ordering systems maintain a configurable token dictionary with weights rather than hard-coding every supplier naming convention.
  - Recommended solution:
    Keep `DetOrderRules.json` as the current token source, add explicit weights and missing-category behavior, and define how `ImageNGP.json` will absorb shared ordering/transform rules.
  - Answer:

- [ ] Define computer-vision hint influence: list which `ImageNGP` labels affect ordering and how they compare to filename hints.
  - Impact:
    - Project progress: High - Vision hints resolve cases where filenames are missing or misleading.
    - Effect on other TODOs: Influences - It connects emitted labels, completed orientation values, type classification, head/human detection, and ordering tie-breakers.
  - Industry standard:
    Vision evidence augments but usually does not override explicit trusted metadata unless confidence is high or metadata is absent.
  - Recommended solution:
    Use high-confidence `HERO_ORIENTATION`, `TypeOfShot`, `HERO_HASHEAD`, and `HERO_ISHUMAN` labels as secondary evidence, allowing them to override weak filename hints but not exact trusted sequence tokens.
  - Answer:
