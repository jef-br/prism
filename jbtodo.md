


-----

# CiMini dataset needs full per-bracket coverage (raised 2026-07-17)

- [ ] CiMini (`test/datasets/CiMini/`) doesn't exercise every bracket in the matching waterfall.
  Confirmed 2026-07-17: 0 of 14 original images ever reached Bracket 4 (`SemanticMatcher`) — every
  image resolved in Brackets 1-3 or sibling propagation first. Need real images + Excel rows so
  every bracket has at least one non-synthetic case.

  **Already closed, 2026-08-06:** six cases were merged into CiMini from JBComplete + CiGolden —
  two competing photos for one det slot, a filename written inside a free-text cell (still blocked
  by [[T-5110]], a code defect not a fixture gap), a long number hiding inside a bigger number
  (with [[T-5090]] as the buggy counter-example), a meaningless filename inside a meaningfully-named
  folder, a product number absent from the sheet entirely (KO reason still wrong), and a photo that
  points at two products (manifest doesn't record which ones). The gaps below are what's left.

  **Two numbers in the filename, each ambiguous alone, only their combination picks one product**
  - Images: 4471-2290.jpg → the green sweater; its reference is the only one containing both
    fragments "4471" and "2290"
  - Excel:
    - FID: green-sweater, Reference: contains "4471" and "2290", Color: green, Product type: sweater
    - FID: decoy1, Reference: contains "4471" only
    - FID: decoy2, Reference: contains "4471" only
    - FID: decoy3, Reference: contains "4471" only
    - FID: decoy4, Reference: contains "2290" only
    - FID: decoy5, Reference: contains "2290" only
  - Note: CiMini's existing `4471-2290-*` doesn't cover this — there the whole reference collides
    across two FamilyIDs, so all four files KO as ambiguous instead of resolving via intersection.

  **A filename word that's a typo/spelling variant of a color, material, or product-type word**
  - Images:
    - grey-scarf.jpg → scarf product, filename says "grey", no reference number in the filename
      (so Bracket 1 can't resolve it first and the fuzzy path actually runs)
    - graphite-scarf.jpg → filename word "graphite" is too many letters off "gray" — should NOT match
    - description-only-variant.jpg → a 1-letter-off color word that appears only in a long
      free-text description column, not in the Color/Material/Type column — should NOT match
  - Excel:
    - FID: scarf-product, Color: gray, Product type: scarf (no reference matching the filename)
    - FID: another-product, Description: free text containing a near-miss color word (not in
      the Color column)

  **A filename with only one matching word, where two matching words are normally required**
  - Images:
    - blue.jpg → matches only one product's color word ("blue") and nothing else → should NOT
      match here, left for a later step
    - blue-hoodie.jpg → matches two words ("blue" + "hoodie") pointing at the same product →
      should match (proves the two-word rule works)
  - Excel:
    - FID: blue-hoodie-product, Color: blue, Product type: hoodie
    - FID: blue-only-product, Color: blue (the single-word-only candidate that blue.jpg must not
      resolve to)

  **Bracket 4 (picture-based matching) — need three cases**
  - Images:
    - x-red-dress.jpg → red dress, filename has no connection to any product number or word;
      picture alone should confidently match the one still-unmatched red-dress product
    - y-blue-jeans.jpg → blue jeans, filename only weakly/partially overlaps one candidate's
      words, picture itself isn't decisive either → should end up unmatched (KO), not a forced guess
    - z-edge-case.jpg → picture gives a little help + filename gives 1-2 real matching words,
      tuned so the accept/reject decision sits right on the pass/fail line — this is the case that
      proves the T-3800 "how many other products are still up for grabs" fix actually matters
  - Excel:
    - FID: red-dress-product, Product type: dress, Color: red — the only unmatched red-dress
      candidate at match time
    - FID: jeans-candidate, Product type: jeans, Color: blue, partial word overlap only with the
      filename
    - FID: edge-case-product — tuned so the old bug would have landed this on the wrong side by
      coincidence of which other products were still unmatched

  **A sibling photo that's related but not identical in wording to an already-matched photo**
  - Images:
    - green-sweater-front.jpg → green sweater, product X (seed match)
    - green-sweater-back.jpg → green sweater, product X (seed match)
    - sweater-detail.jpg → close-up sharing only the word "sweater" with the other two (not
      "green") → should still inherit product X via sibling propagation
  - Excel: FID: product-X, Color: green, Product type: sweater
  - Note: the counter-example (should-NOT-propagate) is already covered by `triggered-mistery.jpg`.
    Every existing `SiblingPropagator` accept in CiMini today is propagating a match that shouldn't
    exist ([[T-5100]]), so nothing proves the accept path works from a correct seed match — only
    the positive case above is missing.

  **A photo whose confidence should get a small boost for two kinds of agreeing evidence**
  - Images: dual-evidence.jpg → filename contains the product's reference number AND the picture
    visually shows the product's color — both should agree and correctly match
  - Excel: FID: correct-product, Reference: matches filename, Color: matches what's visible in photo
  - Note: the convergence bonus currently only fires on two wrong matches in CiMini
    (`OMB-E129-TGV_1/_2`), so today it only proves the bonus inflates a bad match. This case is
    needed to prove it works on a correct one.

  Once the images + Excel rows exist, follow the CiMini README procedure
  (`test/datasets/CiMini/README.md`): downscale to ~1024px longest edge, update `ci-mini.xlsx`,
  eyeball a verified run, then recapture both goldens via
  `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Match -Dataset CiMini -Capture` and
  `-Mode Full -Dataset CiMini -Capture`.
- Answer:

-----

# Phenotype validation needs a purpose-built dataset (raised 2026-07-30, from T-4970)

- [ ] **Mostly closed by `test/datasets/CiMini/` — 2026-08-05, folded into CiMini proper 2026-08-06.**
  Original question: what images/Excel rows are needed so each of the 18 phenotypes and each of the
  5 product types has at least one real case? Answered for 17 of 18 — only
  `illustration-technical-drawing` has no positive case. `test/datasets/CiMini/expected-phenotype.json`
  (99 rows, per-image ground truth) is committed. What's left is the short list of images below —
  none of them blocks the M11 measurement (already taken, see [[T-2600]] step 4), they just make the
  coverage complete.

  One rule constrains every filename below: `Analyzer_FilenameEvidence` writes `hero-orientation`
  straight from filename keywords ("front", "back", "side", "detail", …), so a filename that says
  "front" tells you nothing about whether the pipeline can *see* front. Every filename below is
  deliberately keyword-free except the last pair, which carries keywords on purpose.

  **The one phenotype with no positive case anywhere — `illustration-technical-drawing`**
  - Images: technical-sketch.jpg → flat line drawing/technical sketch of a garment with measurement
    call-outs, black lines on white, no photographic content (needs `is-illustration=true`)
  - Excel: FID: existing family already in `Brackets-Complete.xlsx` — filename must carry the
    FamilyID so it resolves in an early bracket instead of KO'ing before phenotype assignment

  **A near-miss negative for orientation**
  - Images: folded-scarf-symmetric.jpg → scarf photographed flat and symmetrically, front/back
    genuinely indistinguishable → should NOT produce a confident `hero-orientation`
  - Excel: FID: existing family in `Brackets-Complete.xlsx`, filename carries the FamilyID,
    otherwise keyword-free

  **A hard-shadow / soft-shadow twin pair ([[T-4945]])**
  - Images:
    - product-hard-shadow.jpg → one product, hard-edged cast shadow
    - product-soft-shadow.jpg → same product, same setup, soft diffuse shadow
  - Excel: FID: same existing family for both images, filename carries the FamilyID, otherwise
    keyword-free

  **Filename-path twins — the only pair that deliberately carries keywords**
  - Images:
    - shot-keywordfree.jpg → base shot, no orientation keyword in filename
    - shot_front_....jpg → duplicate of the same shot, filename contains "front"
    - shot_back_....jpg → duplicate of the same shot, filename contains "back"
  - Excel: FID: same existing family, filename carries the FamilyID
  - Purpose: isolates how much of any orientation result comes from the filename vs. the picture.

  Excel note for all four: nothing new needed — every case can hang off a family already in
  `Brackets-Complete.xlsx`. Filenames must carry the FamilyID or the image KOs before reaching
  phenotype assignment at all (`MatchingService.cs:315` skips `Refine` when `IsKo`) — this is what
  sank the earlier T-4970 MMERO26 attempt (59 of 60 images KO'd, zero phenotype data).
- Answer:
