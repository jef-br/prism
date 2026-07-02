# CiMini — committed golden fixture

The **only** committed dataset (the rest of `test/datasets/` is gitignored). Kept small (<30 MB) so it
lives comfortably in git, deterministic so CI can assert exact output.

## Contents (to be added)

```
CiMini/
  images/                 # ~6–8 downscaled JPEGs (~1024px longest edge), ~3 families
  ci-mini.xlsx            # family/product rows for exactly those images
  expected-match.json     # golden: SourceReference -> FamilyId          (asserted by -Mode Match)
  expected-manifest.json  # golden: Status/FamilyId/FinalFileName/DetOrder (asserted by -Mode Full)
```

## How to build it (one-time)

Curation needs domain judgement, so it is a deliberate manual step — do **not** auto-generate blindly.

1. **Pick images.** Start from a handful of `TinyTest` images covering ~3 distinct families (a couple
   images per family so det-ordering is exercised). Copy them into `CiMini/images/`.

2. **Downscale** each to ~1024px longest edge so total stays <30 MB. Downscaling can shift CLIP
   features and the visual-dedup hash, which is why the golden is captured *after* this step, from the
   downscaled images — never from the originals.

3. **Excel.** Create `ci-mini.xlsx` containing the family/product rows for exactly those images
   (mirror the column shape of `TinyTest/tiny-test.xlsx`).

4. **Capture the golden from a verified run.** Run both modes and **eyeball that the output is
   correct** (right FamilyIDs, sensible det order, expected output filenames), then capture:

   ```powershell
   pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Match -Dataset CiMini -Capture
   pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full  -Dataset CiMini -Capture
   ```

5. **Commit** `images/`, `ci-mini.xlsx`, and both `expected-*.json`.

To refresh after an intended matcher/transform change, see "Golden drift" in
[`../../ci/README.md`](../../ci/README.md).
