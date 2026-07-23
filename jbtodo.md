



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

# T-4400 phase 2: S109 (magic-number) triage may be too granular in places (raised 2026-07-23)

- [ ] Named every flagged S109 magic number to a `private const` at point of use, across ~40 files
  (Matching/Classify/Analyzers, Transform engine, Upscale, Match/Order/Excel, Zip). Build is clean
  (0 S109 warnings, CI-gated in `ci.yml`) and the full suite is green (408/408), so nothing is
  *broken* — but some of the extractions may hurt more than they help: a single-use const for a
  plain array/channel index or a `/2` midpoint divisor can make code harder to scan, not easier,
  especially when the name barely says more than the literal did. Which ones should be reverted
  back to a bare literal, and which genuinely earn their name?
- Concrete examples worth judging first (not exhaustive — the same pattern repeats across most
  files touched by the S109 pass):
  - `ImageClassifier.cs` — `BlueChannelIndex = 2`, used once to index `NormMean[BlueChannelIndex]`/
    `NormStd[BlueChannelIndex]`/`data[BlueChannelIndex * plane + idx]`. Same pattern repeated in
    `YoloDetector.cs` and `Upscaler.cs` (`BgrThirdChannelIndex`).
  - `SubjectBox.cs` — `MidpointDivisor = 2f` for `(X1 + X2) / MidpointDivisor`. Same pattern in
    `Tx_util_HeadCutter.cs` (`MidpointDivisor`), `Analyzer_SubjectGeometry.cs` (`CenteringScale`/
    `CenterMidpoint`).
  - `Upscaler.cs` — `TensorHeightDimIndex = 2` / `TensorWidthDimIndex = 3` for
    `inputDims[TensorHeightDimIndex]` — arguably clearer as a plain comment on the bare `2`/`3`
    than as named constants, since the names just restate the NCHW convention already documented
    two lines above in the method's own comment.
  - `NumericMatcher.cs` — `DecimalDigitCount = 10` for a `for (int i = 0; i < 10; i++)` digit-count
    loop; `AnalyzerMath.cs`/`Analyzer_Exposure.cs`/`Analyzer_IsIllustration.cs`/
    `Analyzer_DominantColors.cs` — `PixelSampleStride = 2` for `y += 2`/`x += 2` sampling loops,
    redeclared near-identically in 5+ files rather than shared from one place.
  - Full file list touched: every file under `jb/src/core/Services/Matching/`,
    `jb/src/core/Services/Transform/`, `jb/src/core/Services/Upscale/Engine/Upscaler.cs`,
    `jb/src/core/lib/Zip/`, `jb/src/core/lib/Excel/`, plus `WetransferClient.cs` (promoted to
    `HostRules.json`'s `weTransferPolling` section instead of a local const). `git log` /
    `git show --stat` on the commit(s) once committed gives the exact list.
- Options once you've picked which ones to revert:
  1. Revert the const, restore the bare literal, and scope an S109 suppression to just that line
     (`#pragma warning disable S109` / `restore`, or a narrower `.editorconfig` exclude if a whole
     file's pattern should be exempt) so the CI gate (`-warnaserror:...,S109`) doesn't regress the
     next time someone edits that file for something unrelated.
  2. Consolidate the repeated `PixelSampleStride`/`AlphaOpaqueThreshold`/`MaxChannelValueF`-style
     consts (same value, same meaning, redeclared per-file) into one shared internal location
     instead of either keeping them local or reverting to bare literals — trades "many small
     private consts" for "one shared const used everywhere", which may read better without giving
     up S109 compliance.
- Answer:
