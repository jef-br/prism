# Image Ordering Todo

- [ ] Define `_det` suffix assignment: say whether numbering is always zero-based and whether gaps are allowed.
  - Impact:
    - Project progress: High - Suffix assignment creates the final image sequence consumed by downstream systems.
    - Effect on other TODOs: Blocks - It gates output filename suffix rules, zip duplicate handling, manifest projection, and export naming.
  - Industry standard:
    Product image pipelines assign deterministic, contiguous sequence numbers per product after all ordering evidence is evaluated.
  - Recommended solution:
    Use zero-based contiguous `_det` numbers per FamilyID with no gaps in OK output.
  - Answer:

- [ ] Define ordering tie-breakers: say how equal ordering evidence is resolved within one FamilyID.
  - Impact:
    - Project progress: High - Tie-breakers prevent nondeterministic output names.
    - Effect on other TODOs: Blocks - It affects suffix assignment, filename collision handling, and manifest reproducibility.
  - Industry standard:
    Ordering systems use deterministic secondary keys and record tie decisions so repeated runs produce identical outputs.
  - Recommended solution:
    Break ties by strongest filename hint, then vision confidence, then original filename sort order, then stable source index.
  - Answer:

- [ ] Define front image ordering rules: say which filename tokens and image labels push an image toward `_det0`.
  - Impact:
    - Project progress: High - Front image selection determines the primary product image.
    - Effect on other TODOs: Blocks - It feeds suffix assignment, output naming, and computer-vision hint influence.
  - Industry standard:
    Catalog pipelines prioritize explicit front-view metadata and high-confidence visual orientation for the first product image.
  - Recommended solution:
    Rank `front`, `frontal`, `main`, `hero`, `1`, `a`, and high-confidence front orientation labels toward `_det0`.
  - Answer:

- [ ] Define back image ordering rules: say which filename tokens and image labels identify a back view.
  - Impact:
    - Project progress: High - Back view rules establish the second major product view after front.
    - Effect on other TODOs: Influences - It affects suffix assignment, orientation labels, and tie-breakers.
  - Industry standard:
    Product media ordering uses explicit back-view tokens and visual orientation labels as strong evidence after primary/front images.
  - Recommended solution:
    Rank `back`, `rear`, `dos`, and high-confidence back orientation labels immediately after the selected front image.
  - Answer:

- [ ] Define detail image ordering rules: say how close-ups and cropped product details are ranked after main views.
  - Impact:
    - Project progress: Medium - Detail ordering improves presentation quality after main view order is stable.
    - Effect on other TODOs: Influences - It uses image type labels, filename hints, and tie-breakers.
  - Industry standard:
    E-commerce pipelines place detail shots after main views using type labels, filename hints, and stable order.
  - Recommended solution:
    Rank detail images after front/back/side views, ordered by explicit detail tokens, visual detail confidence, and original source order.
  - Answer:

- [ ] Define ambiance image ordering rules: say how lifestyle or contextual images are ordered within a FamilyID.
  - Impact:
    - Project progress: Medium - Ambiance ordering affects secondary presentation but not core matching.
    - Effect on other TODOs: Influences - It depends on image type labels and unknown ordering.
  - Industry standard:
    Lifestyle or ambiance images are usually placed after product-specific views to preserve product clarity.
  - Recommended solution:
    Rank ambiance images after main and detail views, preserving stable source order unless stronger configured hints exist.
  - Answer:

- [ ] Define unknown image ordering rules: say where images with weak or missing view evidence are placed.
  - Impact:
    - Project progress: Medium - Unknown handling prevents weak evidence from disrupting primary views.
    - Effect on other TODOs: Influences - It uses classification unknown states, tie-breakers, and suffix assignment.
  - Industry standard:
    Low-confidence media is ordered conservatively after high-confidence assets and flagged for review when necessary.
  - Recommended solution:
    Place unknown images after known main/detail/ambiance groups and keep their uncertainty in manifest evidence.
  - Answer:

- [ ] Define filename hint influence: list which filename tokens affect ordering and how strong they are.
  - Impact:
    - Project progress: High - Filename hints are often the strongest ordering signal before vision evidence.
    - Effect on other TODOs: Influences - It feeds front/back/detail/ambiance rules and tie-breakers.
  - Industry standard:
    Ordering systems maintain a configurable token dictionary with weights rather than hard-coding every supplier naming convention.
  - Recommended solution:
    Configure weighted tokens for front, back, side, detail, ambiance, sequence numbers, and supplier-specific aliases.
  - Answer:

- [ ] Define computer-vision hint influence: list which labels affect ordering and how they compare to filename hints.
  - Impact:
    - Project progress: High - Vision hints resolve cases where filenames are missing or misleading.
    - Effect on other TODOs: Influences - It connects emitted labels, orientation values, type classification, and ordering tie-breakers.
  - Industry standard:
    Vision evidence augments but usually does not override explicit trusted metadata unless confidence is high or metadata is absent.
  - Recommended solution:
    Use high-confidence orientation and image-type labels as secondary evidence, allowing them to override weak filename hints but not exact trusted sequence tokens.
  - Answer:
