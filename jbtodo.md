



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

  **Six more cases closed by `test/datasets/JBComplete/` (checked against a real run, 2026-08-05).**
  That dataset is committed to git (102 files) — the note further down this file that
  `test/datasets/*` is gitignored except CiMini is stale. The bullets below were removed from this
  list because a real image now exercises each one:
  - *Two photos of the same product competing for one det slot* — `OMB-E181-CVW_5` / `_6` (both
    interior shots of one bag) and `AY_FFK0230_83035_02FW001_A` / `_06FW001_f` (both diagonal).
  - *Filename written verbatim in a product-sheet cell* — `100267_1..7`, whose only link to
    `91337133` is the URL text in the marketing description. **The images are there; the matcher
    fails them today** — `FilenameToCellMatcher` reads the basename of the whole cell and cannot see
    a filename inside free text. That is [[T-5110]], a code defect, no longer a missing fixture.
  - *A long number hiding inside a bigger number* — `8712345678901*.jpg` matches `99985014` through
    EAN `87186798712345678901002387`. **Both counter-examples are present too:** `133726012.jpg`
    (inside two EANs, correctly refused) and `87186790_1/2` (inside two EANs, wrongly accepted —
    [[T-5090]]).
  - *A meaningless filename inside a meaningfully-named folder* — three real subfolders
    (`26182-Denim-801/`, `foldercontainsID99984905/`, `99984901/`) with the sibling decoys that
    satisfy `minPerItemSiblings: 2`. `1.jpg` appears in two of them and must not de-duplicate away.
  - *A product number that isn't in this batch's sheet at all* — `99984901/99984901_det0` and
    `_det1` (the ID is in neither the folder-enrichment path nor the Excel), plus `OMB-E180-BV_*`.
    **The open half is the KO reason, not the image:** both come back
    `MATCHES_MULTIPLE_FAMILYIDS`, which is the wrong reason — nothing distinguishes "this product
    is not in this catalog" from "this filename is ambiguous".
  - *A photo that permanently points at two products* — `133726012`, `4471-2290-*`, `87186790_*`.
    **The open half is again the KO payload:** whether the manifest records *which* families
    collided, or only that something did.

  Below are the gaps that remain, in plain terms — what kind of photo/product situation is needed,
  with an example of a case that should work and, where it matters, a counter-example of a
  similar-looking case that should NOT work (to prove the guardrail holds, not just the happy path).

  **Two numbers in the filename, each ambiguous alone, only their combination picks one product**
  - a: a photo named "4471-2290.jpg". Three different products have "4471" somewhere in their
    reference number, and two different products have "2290" somewhere in theirs. Only one
    product — the green sweater — has both. Neither number alone can pick a winner, but the two
    together can.
  - Counter-example: "4471.jpg" alone, with only one number in the name, can't exercise this case —
    it needs two separately-ambiguous numbers that only resolve when combined.
  - **Not covered by JBComplete's `4471-2290-*`, despite the name.** There the *whole* reference
    exists under two FamilyIDs across two sheets, so all four files KO as ambiguous. The decoy
    structure that makes each half separately ambiguous does not exist. `Bracket2-Intersect` has 0
    accepts on JBComplete, so this branch still has no real-data case.

  **A filename word that's a typo/spelling variant of a color, material, or product-type word**
  - a: a photo named "grey-scarf.jpg". The product's color column says "gray" (American spelling).
    The words aren't identical, but they're one letter apart, so it should still match.
  - Counter-example: "graphite-scarf.jpg" vs. "gray" — too many letters different, should NOT
    match this way. Also: the same one-letter-off word appearing only in a long free-text
    description column (not a color/material/type column) should NOT match this way either.
  - **JBComplete has the data but does not exercise it.** `C153KB460011_Cedric_City_Grey_*.png` say
    `Grey`, family `99147533` carries Color `Gray` (distance 1), and the files belong to `99147525`
    by reference — the sharper version of this case. But all three match at **Bracket 1** on the
    numeric token `460011`, so Bracket 3 never runs and `CollectFuzzyCategoricalEvidence` is never
    invoked. The image still needed is one with a fuzzy colour and **no usable reference number**.

  **A filename with only one matching word, where two matching words are normally required**
  - a: a photo named "blue.jpg" that only matches one product's color word and nothing else.
    Today's rule needs at least two matching words to accept a match this way, so this photo
    should NOT match here — it should be left for a later step to figure out, not accepted on one
    word alone.
  - Counter-example (should match): "blue-hoodie.jpg" — two words, "blue" and "hoodie", both
    pointing at the same one product → accepted.

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

  **A sibling photo that's related but not identical in wording to an already-matched photo**
  - a: two photos of a green sweater, "green-sweater-front.jpg" and "green-sweater-back.jpg",
    already matched to product X. A third photo, "sweater-detail.jpg", only shares the word
    "sweater" with them (not "green") — related, but not worded identically. Should still inherit
    product X.
  - The counter-example is **already covered** by JBComplete's `triggered-mistery.jpg`, which shares
    tokens with all three Triggered rows and is correctly refused. Only the positive case above is
    still missing — every `SiblingPropagator` accept in JBComplete today (3 of them) is propagating a
    match that should not have been made in the first place ([[T-5100]]), so nothing there proves the
    accept path works when the seed match is right.

  **A photo whose confidence should get a small boost for having two kinds of evidence agreeing**
  - a: one photo where both the number in the filename AND the picture's visual color agree on
    the same product — two independent kinds of evidence pointing the same way. This photo's
    final confidence score should end up a little higher than a similar photo that only had one
    kind of evidence.
  - **The bonus does fire on JBComplete, but only on two wrong matches** (`OMB-E129-TGV_1/_2`,
    `score=0,667` + `[convergence bonus +0,25]`). No correctly-matched image in the set earns one,
    so today the only evidence the bonus works is evidence of it inflating a match the golden says
    should not exist. A positive case is still needed.

  Once source images + Excel rows exist for the cases above, follow the existing CiMini
  README procedure exactly (`test/datasets/CiMini/README.md`): downscale, build/update
  `ci-mini.xlsx`, eyeball a verified run, then recapture both goldens via
  `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Match -Dataset CiMini -Capture` and
  `-Mode Full -Dataset CiMini -Capture`.
- Answer:

-----

# Phenotype validation needs a purpose-built dataset (raised 2026-07-30, from T-4970)

- [ ] **Mostly closed by `test/datasets/JBComplete/` — 2026-08-05.** The original question was: what
  images and Excel rows are needed so each phenotype and each of the 5 product types has at least
  one real case? (Written when the taxonomy was 21; it is **18** since [[T-5040]].) JBComplete
  answers it for **17 of the 18** — only `illustration-technical-drawing` has no positive case, and
  its `expected-phenotype.json` (99 rows, per-image ground truth) is the labelled file the last
  paragraph of this block used to ask for. Both the dataset and the labels are committed to git.

  The original framing — "no dataset here exercises more than a sliver of the taxonomy" — was true
  of SPACINI29 (86 images of one model, front and back, on one white sweep: `hero-is-human=TRUE` on
  85/86, `background-type=SOLIDCOLOR` on 86/86) and is no longer true of the repo.

  **What the measurement then showed, first time it could be taken.** Scored at shipped config:
  39.4% coverage, **30.3% misassignment** against M11's 5% bar, `front-packshot` recall **0/25**,
  `closeup-image` precision 0/8. SPACINI29's 4.7% was never a pass — it is what a dataset exercising
  2 of 18 phenotypes reports. Two causes account for nearly all of it and **neither is a dataset
  problem**: [[T-5070]] (`intersection-count = 0` is required by 7 of the 18 phenotypes and only 27
  of 100 images satisfy it) and [[T-5080]] (`hero-orientation` is UNKNOWN on 37% and never once
  produces `SIDEON`). Full numbers in [[T-2600]] step 4.

  **So this todo is now down to the images JBComplete does not supply**, listed below. Everything
  else that used to be here — the topwear/bottomwear/footwear/bags/default families and their
  per-phenotype bullets — is covered; see `test/datasets/JBComplete/README.md` §3, which does the
  per-case accounting against CiGolden's list.
- Impact:
  - Was High, now Low-Medium. The M11 acceptance bar (<5% misassignment across 18 phenotypes) was
    gated on a measurement nobody could take. JBComplete's `expected-phenotype.json` makes it
    takeable, and it has been taken — see [[T-2600]] step 4. What is left here is a short list of
    images that dataset does not supply; none of them blocks the M11 measurement, they only stop it
    being complete.
  - Effect on other TODOs: [[T-4948]] (low-contrast white-on-white) is **closed** by JBComplete's
    sock images. [[T-4945]] still needs its hard-vs-soft shadow pair, which is one of the gaps below.
- Industry standard:
  A classifier with N output classes needs a validation set with positive cases for all N, plus
  near-miss negatives for the pairs that are easy to confuse. JBComplete supplies 17 of 18 positives.
  The remaining bullets are the missing positive and the near-miss negatives.
- Recommended solution:
  Add the images below to `test/datasets/JBComplete/` (it is committed to git and already carries the
  label file, so a second dataset would only split the ground truth in two). Keep the CiMini budget
  discipline — downscale to ~1024px longest edge, and add a row to `expected-phenotype.json` for each.

  One experiment-design rule still constrains every filename here, and matters more than it looks:
  `Analyzer_FilenameEvidence` writes `hero-orientation` straight from filename keywords ("front",
  "back", "side", "detail", …). So an image whose filename says "front" tells you nothing about
  whether the pipeline can *see* front. Every filename below is deliberately keyword-free, except the
  last pair, which carries keywords on purpose.

  **The one phenotype with no positive case anywhere — `illustration-technical-drawing`**
  - a: a flat line drawing / technical sketch of a garment with measurement call-outs, black lines on
    white, no photographic content (needs `is-illustration=true`). JBComplete's `100267_6` and
    `100267_7` are the closest and are not close enough — both are marketing composites built on
    photographs. Their `expected-phenotype.json` rows are correctly `null`.

  **A near-miss negative for orientation — the case that stops the bar being tuned down**
  - b: a folded scarf photographed flat and symmetrically, where front and back are genuinely
    indistinguishable. Should NOT produce a confident `hero-orientation`. This is the negative case
    that stops the orientation threshold being lowered until everything passes — directly relevant
    now that [[T-5080]] is about to move that threshold, and doubly so because the bar has already
    been lowered twice (0.60 → 0.33) with nothing in the repo to say when it has gone too far.

  **A hard-shadow / soft-shadow twin pair ([[T-4945]])**
  - c and d: one product, shot twice — once with a hard-edged cast shadow, once with a soft diffuse
    one, everything else held constant. JBComplete has no controlled twin: its alpha PNGs carry a
    glow rather than a shadow, and the OMB bags are all soft. Without the pair the `shadow-present`
    threshold can only be tuned against uncontrolled variation.

  **Filename-path twins — the only bullets that deliberately carry keywords**
  - e and f: two duplicate shots of one keyword-free image's setup, one named `..._front_...`, one
    named `..._back_...`. Together with their keyword-free originals these isolate how much of any
    orientation result comes from the filename rather than the picture. JBComplete's
    `C153KU420009_..._FRONT` / `_BACK` and `C153KB460011_..._FRON` / `_BAC` carry keywords but have no
    keyword-free counterpart of the same shot, so they cannot separate the two paths.

  **What the Excel must contain:** nothing new. All four cases can hang off families already in
  `Brackets-Complete.xlsx`. Filenames must carry the FamilyID so matching resolves in an early
  bracket — the point of these images is phenotypes, and a KO'd image never reaches phenotype
  assignment at all (`MatchingService.cs:315` skips `Refine` when `IsKo`). That is exactly what sank
  the T-4970 MMERO26 attempt: 59 of 60 images KO'd on `MATCHES_MULTIPLE_FAMILYIDS` and produced no
  phenotype data whatsoever.

  **Removed from this list on 2026-08-05, all covered by JBComplete** (per-case accounting in
  `test/datasets/JBComplete/README.md` §3): the five per-product-type families `TW001` topwear,
  `BW001` bottomwear, `FW001` footwear, `BA001` bags-accessories and `DF001` default, with their
  ~25 per-phenotype bullets; the low-contrast white-on-white case ([[T-4948]]); and the
  `TW001_m/n/o` real-catalogue crop cases, whose `back-on-model-partial` gap is closed by
  `triggered_black-tshirt-back-americain.jpg` and `foldercontainsID99984905/2.jpg`. The three
  `ghost-front` / `ghost-back` bullets were not covered but **retired** — [[T-5040]] deleted those
  phenotypes, so they no longer name anything. The per-image ground-truth label file this block
  used to ask for is `test/datasets/JBComplete/expected-phenotype.json`, 99 rows. Note 16 of those
  rows are marked `"Confidence": "low"` and want a human pass (README §4.3); excluding them moves
  the headline misassignment number by 1.4 points, so they are worth doing but are not load-bearing.
- Answer:
