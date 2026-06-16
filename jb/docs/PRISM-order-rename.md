# PRISM — Image Ordering & Rename

## `ImageOrderer.cs`

Orders the set of images associated to a single FamilyID using ImageFeatures, derived ImageNGPs, DetOrder rules, and relevant information from matching.

---

## `_det` Suffix Rules

- Suffix is always **zero-based** (`_det0`, `_det1`, `_det2`, …).
- Order gaps are **allowed** between original images when missing det positions can be filled by copying and transforming an existing image (e.g., generating a det1 from det0).
- After renaming, any remaining gaps are **closed**.

---

## Ordering Strategy

Ordering uses this model:

`ImageFeature -> ImageNGP -> DetOrder position`

- `ImageFeature` is one measured attribute, produced by CLIP classification or a purpose-built analyzer.
- `ImageNGP` is the phenotype label derived from a combination of ImageFeatures.
- `DetOrder` is the product-type-specific priority list that decides which ImageNGPs can fill `_det0`, `_det1`, etc.

The standalone `ImageRole` concept is not part of the ordering model. The role of an image with ImageNGP `JEANS_GHOST_FRONT` is to do `_det0` only when that ImageNGP wins the relevant DetOrder position.

Ordering happens inside one matched `FamilyID` group. A `Family` is the collection of images that share the same `FamilyID`.

1. Each image has measured ImageFeatures.
2. Those ImageFeatures produce candidate ImageNGPs.
3. Each DetOrder position has a priority list of qualifying ImageNGPs.
4. Multiple ImageNGPs can qualify for one DetOrder position.
5. One ImageNGP can qualify for multiple DetOrder positions.

**Step 1 — DetOrder candidate assignment:**

For each image, build every qualifying ImageNGP/DetOrder combination. The final placement is decided by the highest-ranking combination:

- Earlier DetOrder position wins first, so `_det0` outranks `_det1`, `_det1` outranks `_det2`, etc.
- Within the same DetOrder position, the ImageNGP priority rank for that DetOrder wins.
- Example: if one image ranks second for `_det0` and first for `_det3`, it becomes `_det0` unless another image qualifies for `_det0` at rank one.

**Step 2 — Filename ordering hints:**

Scan original filename for suffix indicating order:
- Keyword tokens: `front`, `side`, `back`, `frontal`, `a`, `1`, `det0`, …
- Numerical suffix: `..._1.jpg`, `..._2.jpg`
- Alphabetical suffix: `..._A.jpg`, `..._B.jpg`
- Alphanumerical suffix: `..._A1.jpg`, `..._A2.jpg`, `..._B1.jpg`

Filename hints can support or break ties only after ImageNGP qualification and DetOrder eligibility are already established. They cannot define an ImageNGP, cannot assign `_det#` directly, and cannot override DetOrder eligibility. `DetOrderKeywordStems.json` is a source of ordering hints only.

Keep DetOrder keyword stems as ordering hints, not ImageNGP definitions and not transform rules. Complete supplier schemes such as `_det#`, `_A/_B/_C`, `_1/_2/_3`, and `_front/_back/_side` are compatibility evidence and tie-breakers after ImageNGP eligibility is known. Preserve token source, position, original text, normalized text, and purpose as matching or ordering evidence.

**Step 3 — Classification and analyzer evidence:**

Use ImageFeatures from image classification labels and purpose-built analyzers to derive ImageNGPs:
- If a `front` orientation feature is found, front-facing ImageNGPs can qualify.
- Human, head, product type, background, edge-intersection, and detail features can distinguish ImageNGPs such as `PAP_FRONT`, `JEANS_GHOST_FRONT`, or `JEANS_GHOST_DETAIL`.
- Classification labels and analyzer outputs do not assign `_det#` directly; DetOrder rules assign positions from qualifying ImageNGPs.

**Step 4 — Deterministic tie-breaking:**

When multiple images inside one Family compete for the same DetOrder position and ImageNGP priority rank, break ties by:
- Selected ImageNGP confidence
- Compatible filename ordering hint
- Stable import/source index

Selected ImageNGP confidence is evidence-count-based for ordering tie-breaks. Count the qualifying pieces of evidence for the selected ImageNGP; each qualifying evidence category has equal value for now. Individual evidence pieces may store weighted ML confidence, but ordering tie-breaks do not use that ML confidence as weighting yet.

Filename hints only break ties when compatible with the already-qualified DetOrder position. If ImageNGP confidence and compatible filename hints do not resolve the tie, use the stable import/source index. Treat "first image opened" as the source index assigned during import.

DetOrder assignment evidence must record which tie-breaker won: ImageNGP confidence, filename hint, or stable source index.

---

## Output Filename Rules

- The output filename stem is the matched `FamilyRecord` FamilyID.
- Source filenames, display labels, and non-FamilyID catalog properties do **not** become the final stem.
- Rename collapses the probability of FamilyID + order into a filename.
- Every processed PRISM output image uses the `.jpg` extension.
- Valid ordering guarantees unique final filenames in the form `FamilyID_det#.jpg` inside each FamilyID group.
- Output names are reserved before export. The reservation covers final filenames, zip entry paths, and JSON artifact paths.
- A duplicate final filename, zip path, or JSON artifact path is an ordering/rename invariant failure. PRISM must KO the whole affected FamilyID/family with `RENAME_COLLISION` or `export-path-collision`, keep every original filename as provenance, emit safe manifest evidence, and continue the rest of the batch.

## Output Filename Sanitization

- Output/export basenames use the conservative portable allowlist `A-Z`, `a-z`, `0-9`, `.`, `_`, and `-`.
- Replace every character outside the allowlist with `_`.
- Replace whitespace runs with one `_`, collapse repeated `_`, then trim leading and trailing whitespace, `_`, and `.`.
- Empty sanitized basenames are KO, not fallback names.
- Forbidden/replaced characters include at least `<`, `>`, `:`, `"`, `/`, `\`, `|`, `?`, `*`, ASCII NUL, and control characters `U+0001` through `U+001F`.
- Do not allow path separators from any platform: `/`, `\`, or legacy/display-path `:`.
- Do not allow names ending in a space or period, and do not allow `.` or `..` as output basenames.
- Reject Windows reserved device basenames case-insensitively, including with extensions: `CON`, `PRN`, `AUX`, `NUL`, `COM1` through `COM9`, and `LPT1` through `LPT9`.

---

## Unmatched Image Naming

Images that cannot be matched to an acceptable FamilyID → **KO records**.

- Keep original filename as safe provenance in `manifest.json`.
- Do not receive an OK FamilyID-based output filename.
- Excluded from OK output images.
- KO export placement governed by zip/layout policy — must not look like a successful product match.
