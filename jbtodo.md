



-----

# CiMini dataset needs full per-bracket coverage (raised 2026-07-17)

- [ ] CiMini coverage gap: CiMini (`test/datasets/CiMini/`) is PRISM's only committed golden
  fixture, but its 14 images only exercise a subset of the Matching waterfall. Confirmed by a
  T-3800 validation run (2026-07-17): 0 of 14 images ever reach Bracket 4 (`SemanticMatcher`),
  because every image resolves in Brackets 1-3 or sibling propagation first — meaning an entire
  bracket (and any future change to it) has zero real-data regression coverage today. What
  specific images/Excel rows need to be added so every bracket in the waterfall has at least one
  real, non-synthetic case exercising it?
- Impact:
  - Medium-High — `ImageMatcher.RunWaterfall` (`jb/src/core/Services/Matching/ImageMatcher.cs:65-128`)
    has 11 distinct decision points (listed below). A bug introduced in any bracket CiMini doesn't
    exercise can ship silently: `-Mode Full -Dataset CiMini` stays green while that bracket is
    broken, because the golden never touches it.
  - Effect on other TODOs: this is what T-3800's `totalImageTokens` fix ran into directly — the fix
    is proven correct by a hand-built unit test but has zero empirical validation on real data,
    purely because CiMini has no image that survives to Bracket 4. The same blind spot applies to
    Bracket 2-Intersect, the T-3800 fuzzy-matching fallback, substring rescue, and more (below).
- Industry standard:
  A golden/regression fixture for a multi-stage decision pipeline should have at least one case per
  distinct decision branch — otherwise coverage tools and "all green" status both overstate how much
  of the system is actually protected against regression.
- Recommended solution:
  Expand CiMini (mind the README's own <30 MB budget — downscale new images to ~1024px longest
  edge like the existing ones) with real product photos and matching Excel rows. Already covered,
  no new case needed: matching by one clean number in the filename, matching by two number pieces
  in the filename joined together, and two photos of the same cardigan where the second one's name
  alone means nothing but it inherits the product from the first because they clearly go together.
  Below are the gaps, in plain terms — what kind of photo/product situation is needed, with an
  example of a case that should work and, where it matters, a counter-example of a similar-looking
  case that should NOT work (to prove the guardrail holds, not just the happy path).

  **Two numbers in the filename, each ambiguous alone, only their combination picks one product**
  - a: a photo named "4471-2290.jpg". Three different products have "4471" somewhere in their
    reference number, and two different products have "2290" somewhere in theirs. Only one
    product — the green sweater — has both. Neither number alone can pick a winner, but the two
    together can.
  - Counter-example: "4471.jpg" alone, with only one number in the name, can't exercise this case —
    it needs two separately-ambiguous numbers that only resolve when combined.

  **A filename word that's a typo/spelling variant of a color, material, or product-type word**
  - a: a photo named "grey-scarf.jpg". The product's color column says "gray" (American spelling).
    The words aren't identical, but they're one letter apart, so it should still match.
  - Counter-example: "graphite-scarf.jpg" vs. "gray" — too many letters different, should NOT
    match this way. Also: the same one-letter-off word appearing only in a long free-text
    description column (not a color/material/type column) should NOT match this way either.

  **A filename with only one matching word, where two matching words are normally required**
  - a: a photo named "blue.jpg" that only matches one product's color word and nothing else.
    Today's rule needs at least two matching words to accept a match this way, so this photo
    should NOT match here — it should be left for a later step to figure out, not accepted on one
    word alone.
  - Counter-example (should match): "blue-hoodie.jpg" — two words, "blue" and "hoodie", both
    pointing at the same one product → accepted.

  **Two photos of the same product, same shot type, one already taken**
  - a: two flat-lay photos of the same jacket. The first one already matched product X. The
    second one's filename also points to product X, but product X already has a flat-lay photo —
    so the second one should be pushed further down the line instead of being accepted as a
    second flat-lay for the same product.

  **Bracket 4 (picture-based matching) — need x, y, and z**
  - x: a photo of a red dress with a filename that has no connection to any product number or
    word at all — but the picture itself clearly shows a red dress, and only one still-unmatched
    product in the sheet is a red dress. Should match purely because the photo and the product
    agree, with a confident, clearly-above-the-line score.
  - y: a photo of blue jeans with a filename that only weakly and partially overlaps one candidate
    product's words, and the picture itself isn't decisive either (jeans photos all look similar).
    Should end up unmatched (KO'd) rather than forcing a guess.
  - z: a photo where the picture gives a little help and the filename gives one or two real
    matching words — deliberately tuned so the accept/reject decision sits right on the edge of
    the pass/fail line. This is the one that actually proves the T-3800 fix matters: it needs to
    be built so that if the old "how many other products are still up for grabs" bug were still
    there, this exact photo would land on the wrong side of the line purely by coincidence of
    which other products happened to still be unmatched — not because of anything about the photo
    itself.

  **A filename that means nothing on its own, but is written down somewhere in the product sheet**
  - a: a photo named "photo_final_2.jpg" — nothing about the name points to any product. But one
    product's row has an extra column (e.g. a "website image link" column) that literally contains
    the text "photo_final_2.jpg". Should match purely because that exact filename shows up
    somewhere in that product's row.

  **A long number in the filename that's part of a bigger number on the product, not equal to it**
  - a: a photo named "8712345678901.jpg" (a long barcode-like number). No product's own reference
    number equals that exactly. But one product's barcode column holds a longer number,
    "18712345678901", which contains those same digits inside it. Should match because it's
    "hiding inside" a real product's barcode.
  - Counter-example: the same long number happens to be hiding inside TWO different products'
    barcodes. Should NOT match — refused as ambiguous, not guessed.

  **A sibling photo that's related but not identical in wording to an already-matched photo**
  - a: two photos of a green sweater, "green-sweater-front.jpg" and "green-sweater-back.jpg",
    already matched to product X. A third photo, "sweater-detail.jpg", only shares the word
    "sweater" with them (not "green") — related, but not worded identically. Should still inherit
    product X.
  - Counter-example: a fourth photo shares the word "sweater" with two DIFFERENT already-matched
    products that disagree on which product it is. Should NOT inherit either — refused, left
    unmatched, rather than guessing.

  **A photo whose confidence should get a small boost for having two kinds of evidence agreeing**
  - a: one photo where both the number in the filename AND the picture's visual color agree on
    the same product — two independent kinds of evidence pointing the same way. This photo's
    final confidence score should end up a little higher than a similar photo that only had one
    kind of evidence.

  **A meaningless filename inside a meaningfully-named folder**
  - a: a folder named "23456-red-tote" containing a photo just named "1.jpg". The photo's own
    name means nothing, but the folder name mentions the product's reference number, and there
    are several other similarly-named product folders next to it (not just one folder, and not a
    folder simply called "Web" or "HD"). The photo should borrow the folder's name and then match
    normally using that.

  **A product number in the filename that isn't in this batch's product sheet at all**
  - a: a photo named with a real-looking, well-formed product number that simply doesn't appear
    anywhere in this particular Excel sheet (it's a real product, just not part of this batch).
    Should be rejected with a "not in this catalog" reason, not a generic "no match found" one.

  **A photo that genuinely and permanently points at two different products**
  - a: a photo whose number or words point equally at two different products, and nothing
    anywhere breaks the tie. Should be rejected with a "matches more than one product" reason,
    naming both.

  Once source images + Excel rows exist for the cases above, follow the existing CiMini
  README procedure exactly (`test/datasets/CiMini/README.md`): downscale, build/update
  `ci-mini.xlsx`, eyeball a verified run, then recapture both goldens via
  `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Match -Dataset CiMini -Capture` and
  `-Mode Full -Dataset CiMini -Capture`.
- Answer:

-----

# Phenotype validation needs a purpose-built dataset (raised 2026-07-30, from T-4970)

- [ ] There is no dataset in this repo that can measure whether phenotype assignment works. T-4970
  ran the evidence harness on SPACINI29 (86 images) and a 60-image MMERO26 subset and could not
  answer the question, because **no dataset here exercises more than a sliver of the taxonomy**.
  SPACINI29 is 86 images of one model, front and back, on one white sweep: `hero-is-human=TRUE`
  on 85/86, `background-type=SOLIDCOLOR` on 86/86, `lifestyle-background=false` on 86/86. Every
  packshot phenotype, every ghost phenotype, every lifestyle phenotype and every non-human
  phenotype is unrepresented by construction — not measured and found wanting, just absent. What
  images and Excel rows are needed so each of the 21 phenotypes and each of the 5 product types
  has at least one real case? (20 until 2026-07-30, when T-4970 added `back-on-model-partial`.)
- Impact:
  - High — the `BypassPhenotypes` flip decision ([[T-2600]]) and the whole M11 acceptance bar
    (<5% misassignment across 20 phenotypes) are gated on a measurement nobody can currently take.
    T-4970 could only report "6/86 images got a phenotype, all of them the same one", which says
    more about the dataset than about the rules.
  - Effect on other TODOs: [[T-4945]] needs a labelled set for the hard-shadow threshold and the
    centering A/B, and explicitly wants to commission only **one** labelled asset. This is that
    asset. [[T-4948]] needs low-contrast white-on-white product shots, which also belong here.
- Industry standard:
  A classifier with N output classes needs a validation set with positive cases for all N, plus
  near-miss negatives for the pairs that are easy to confuse. A set that only contains examples of
  2 classes cannot distinguish "the rules are wrong" from "the model never sees the other 18".
- Recommended solution:
  Build one new dataset in **`test/datasets/CiGolden/`** with the images below. **Each bullet is one
  image.** Every bullet gives the filename to use, what the photo must show, and what the Excel row
  must carry. Keep the CiMini budget discipline — downscale to ~1024px longest edge.

  **22 candidates are already staged** in `test/datasets/CiGolden/candidates/`, pulled from FILA94,
  MMERO26, HEROAUT3 and SPACINI29 and described one by one in that folder's README. FILA94 turned out
  to carry a clean view-code convention (`PS_FV`/`PS_BV`/`PS_SL`/`PS_SO`/`PS_TV`/`MS_SF`/`MS_FV`/
  `MS_BV`/`MS_DV`) that maps almost one-to-one onto the taxonomy, and its packshot PNGs have real
  alpha, so they serve as the ghost cases. Six cases still have no source anywhere in the repo —
  listed at the end of that README. Note `test/datasets/*` is gitignored except CiMini, so CiGolden
  is local-only unless it is deliberately committed.

  **Update 2026-07-30 (T-4970 second pass).** Three bullets were added at the end — `TW001_m/n/o` —
  after measuring what actually goes wrong on real on-model shots. `model-detail-closeup` claimed 33
  of 86 SPACINI29 images at a 0.45 bar and **none of them is a detail crop**; 48% of SPACINI29 is a
  back view cut by a frame edge, for which **no rule existed**. Both rules have since been fixed, so
  these three bullets are now the regression cases that keep them fixed rather than the images that
  expose the bug.

  **Two experiment-design rules that constrain every filename below, and matter more than they look:**
  1. `Analyzer_FilenameEvidence` writes `hero-orientation` straight from filename keywords
     ("front", "back", "side", "detail", …). So an image whose filename says "front" tells you
     nothing about whether the pipeline can *see* front. Every filename below is deliberately
     keyword-free, so the measurement is of CLIP and the analyzers. The last group adds
     keyword-carrying twins on purpose, to measure the filename path separately.
  2. Product type drives which det slot a phenotype maps to (`DetOrderRules.json` has 5 product
     types with different slot orders), so each product type needs its own family — a phenotype
     that is det0 for `topwear` is det1 for `footwear`.

  **topwear — family TW001, a navy women's knit sweater** (Excel: FamilyID `TW001`, description
  naming "sweater"/"knit" so `ProductTypeMap` resolves `topwear`, colour column "navy")
  - a: `TW001_a.jpg` — the model standing square to camera, whole body in frame including feet,
    face fully visible, nothing touching any edge of the frame. Targets `front-on-model-full-product`.
  - b: `TW001_b.jpg` — same model, same sweater, turned fully away from camera, whole body in
    frame, back of head only (no face), nothing touching an edge. Targets `back-on-model-full-product`.
  - c: `TW001_c.jpg` — model in true left or right profile, whole body in frame. Targets `side-on-model`.
  - d: `TW001_d.jpg` — model facing camera, cropped at the waist so the frame cuts the body at the
    bottom edge. Targets `front-on-model-partial`.
  - e: `TW001_e.jpg` — tight shot of the cuff on the model's wrist, only a hand and forearm in
    frame, no torso and no face. Targets `model-detail-closeup`.
  - f: `TW001_f.jpg` — the knit fabric filling the whole frame and running off all four edges, no
    person anywhere in shot. Targets `closeup-image`.
  - g: `TW001_g.jpg` — the model wearing the sweater on a city street, whole garment clearly
    visible, real background with buildings and depth. Targets `lifestyle-hero`.
  - h: `TW001_h.png` — the sweater on an invisible mannequin, saved as a **PNG with a genuinely
    transparent background** (not white). Must be a real alpha channel. Targets `ghost-front`,
    which per `imagePhenotypes.md` is only reachable via `clipping-path=true` on a non-solid
    background — on a white sweep the rules label the identical shot `front-packshot`.

  **bottomwear — family BW001, black wide-leg trousers** (Excel: FamilyID `BW001`, description
  naming "trousers", colour "black")
  - i: `BW001_a.jpg` — the trousers laid flat, front facing up, on a plain white sweep, whole
    garment inside the frame with white margin all round, no person. Targets `front-packshot`.
  - j: `BW001_b.jpg` — same flat trousers photographed from the reverse side, same white sweep,
    same clear margin. Targets `back-packshot`.
  - k: `BW001_c.jpg` — a flat line drawing / technical sketch of the trousers with measurement
    call-outs, black lines on white, no photographic content. Targets
    `illustration-technical-drawing` (needs `is-illustration=true`).

  **footwear — family FW001, a white leather sneaker** (Excel: FamilyID `FW001`, description
  naming "sneaker"/"shoe", colour "white". Note this doubles as the [[T-4948]] white-on-white case)
  - l: `FW001_a.jpg` — one sneaker at a 3/4 angle on white, whole shoe in frame. Targets
    `diagonal-packshot`.
  - m: `FW001_b.jpg` — the same sneaker in exact side profile on white. Targets `side-packshot`.
  - n: `FW001_c.jpg` — the sneaker shot from straight overhead on white. Targets `top-packshot`.
  - o: `FW001_d.jpg` — the sole of the sneaker facing the camera on white. Targets `bottom-packshot`.
  - p: `FW001_e.jpg` — a model wearing the sneakers and also visibly carrying a bag and wearing a
    hat, so more than one product is in shot and the shoes are still clearly visible. Targets
    `on-model-with-accessories` (needs `multiple-products=true`, which is YOLO-driven).

  **bags-accessories — family BA001, a tan leather tote** (Excel: FamilyID `BA001`, description
  naming "bag"/"tote", colour "tan")
  - q: `BA001_a.jpg` — the tote upright and square to camera on white, whole bag in frame with
    margin. Targets `front-packshot` for a second product type.
  - r: `BA001_b.jpg` — the bag open, shot down into the lining so the inside compartment is the
    subject. Targets `interior-shot` (needs `interior-detected=true`).
  - s: `BA001_c.jpg` — a styled café table scene where the tote is present but incidental, not the
    focus of the shot. Targets `lifestyle-context`, the residual catch-all.

  **default product type — family DF001, a ceramic vase** (Excel: FamilyID `DF001`, description
  naming a homeware term that does *not* resolve to any of the four clothing product types, so
  `DetOrderRules.productTypes.default` is used)
  - t: `DF001_a.jpg` — the vase square to camera on white, whole object in frame with margin.
  - u: `DF001_b.jpg` — the same vase in exact side profile on white.

  **Guardrail cases — these exist to prove the rules refuse, or to pin known ambiguities**
  - v: `TW001_i.jpg` — the sweater on an invisible mannequin front-on but on a **plain white**
    background this time. Pins the documented ghost-vs-packshot ambiguity: the correct current
    answer is `front-packshot`, not `ghost-front`. If it ever returns `ghost-front`, rule order
    changed.
  - w: `BW001_d.jpg` — the trousers flat, reverse side up, on white, deliberately framed so the
    garment runs off the left and right edges. Today's rules label this `ghost-back`, because
    `back-packshot` requires `intersection-count=0` and `ghost-back` carries no intersection
    condition. Include it so that behaviour is visible and deliberate rather than discovered later.
  - x: `TW001_j.jpg` — a folded scarf photographed flat and symmetrically, where front and back are
    genuinely indistinguishable. Should NOT produce a confident `hero-orientation`; it is the
    negative case that stops the orientation threshold being tuned down until everything passes.
  - y: `FW001_f.jpg` — the white sneaker on a white sweep under flat lighting, the canonical
    low-contrast case [[T-4948]] needs for the subject-detector contrast floor.

  **Filename-path twins — the only bullets that deliberately carry keywords**
  - z: `TW001_front_k.jpg` — a duplicate shot of image (a)'s setup with "front" in the name.
    Together with (a) this isolates how much of any orientation result comes from the filename
    rather than the picture.
  - aa: `TW001_back_l.jpg` — the same for image (b) and "back".

  **The real-catalogue crop cases — added 2026-07-30, these are the ones a production set is
  actually full of.** SPACINI29 is 86 images of this shape and the rules get all of them wrong.
  - bb: `TW001_m.jpg` — the model facing camera wearing the sweater, framed so the **top of the
    head is cut off by the top edge of the frame** and the legs are cut off at mid-thigh. Face
    partly visible, nothing else touching an edge. This is the ordinary catalogue crop. Correct
    answer is `front-on-model-partial`. Today, whenever the orientation bar is not cleared, the
    pipeline calls this `model-detail-closeup` instead, because the frame-edge cut sets
    `occlusion-level=partially-occluded`. The negative case for that rule.
  - cc: `TW001_n.jpg` — the same model and sweater turned fully away from camera, framed the same
    way: **top of head cut off by the top edge**, legs cut at mid-thigh, back of head only. There
    is currently **no phenotype in the taxonomy that fits this image** — the human branch has
    front-partial but no back-partial. Include it so the gap is a visible test failure instead of
    an unexplained null.
  - dd: `TW001_o.jpg` — a genuine detail crop for contrast: the sweater's shoulder seam and collar
    filling the frame, a sliver of the model's neck visible, no face and no torso. This is what
    `model-detail-closeup` is supposed to mean. Paired with (bb) it separates "the rule is right
    and the threshold is wrong" from "the rule matches the wrong thing".

  **What the Excel must contain overall:** one row per FamilyID above (`TW001`, `BW001`, `FW001`,
  `BA001`, `DF001`), each with a primary key that satisfies `ExcelConfig.FamilyIDProperties`, a
  description column whose wording resolves the intended product type through `ProductTypeMap.json`,
  and a colour column. Filenames must carry the FamilyID so matching resolves in an early bracket —
  the point of this dataset is phenotypes, and a KO'd image never reaches phenotype assignment at
  all (`MatchingService.cs:315` skips `Refine` when `IsKo`). That is exactly what sank the T-4970
  MMERO26 attempt: 59 of 60 images KO'd on `MATCHES_MULTIPLE_FAMILYIDS` and produced no phenotype
  data whatsoever.

  **Also needed, and not satisfied by any bullet above:** a per-image ground-truth label file
  (proposed `expected-phenotype.json`, `filename → phenotype id`) so the M11 confusion matrix can
  be computed automatically rather than by eye. Without it this set measures coverage but not
  correctness.
- Answer:
