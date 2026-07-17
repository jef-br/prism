



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

# T-3300 independent branch review — findings (raised 2026-07-16)

Independent reviewer pass over the whole `t3300-distributed-seam` branch (distributed-services seam).
Verdict was "request changes". Each finding below states its cause, its effect, and its consequence.
Status per finding is kept current.

## R1 · A hung remote Upscale host can freeze a Transform job forever

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** The branch gave every service HTTP client an infinite transport timeout (on purpose — a
  stage legitimately runs for many minutes, and the old 100-second default killed healthy runs). The
  safety story became "the job's cancellation token stops a stuck call instead". But one call path never
  received that token: `ImagePreProcessor.cs` calls `remoteUpscale.UpscaleAsync(..., CancellationToken.None)`,
  and neither `Preprocess` nor the `Parallel.ForEach` in `TransformService` passes the job's token down.
- **Effect:** If the remote Upscale host stops responding mid-request (crash, network drop, GPU wedge),
  the Transform worker thread waits forever. Nothing can interrupt it — not the job timeout, not job
  cancellation.
- **Consequence:** One dead Upscale host permanently eats a Transform job and its worker threads. Before
  this branch the hang was at least capped at 100 seconds; now it is unbounded. This is a regression on
  exactly the failure the distributed seam exists to survive.

## R2 · The new HTTP failure paths have zero test coverage

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** All 14 new ServiceHost roundtrip tests exercise the happy path only. No test makes a client
  face a dead host, an error status, or a cancelled token.
- **Effect:** The behavior of each `Http*Service` client under failure (what exception, how fast, does
  cancellation work) is unverified. R1 survived precisely because no test ever cancelled a remote call.
- **Consequence:** Future changes to the clients or their timeout/cancellation behavior can silently
  break distributed error handling; the suite would stay green while a production job hangs or dies with
  a misleading error.

## R3 · CLAUDE.md still says the solution has 8 projects

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** The test split added 5 projects to `PRISM.sln` (4 per-service test projects +
  `Prism.Tests.Shared`), and the CLAUDE.md Tests section was updated — but its Architecture section
  still lists "8 projects".
- **Effect:** Every future session reads CLAUDE.md at start and learns a wrong solution layout.
- **Consequence:** Agents reason from a stale map — e.g. they won't know `Prism.Tests.Shared` exists and
  may re-create shared fixtures or miss the per-service test projects entirely.

## R4 · All five test projects write their CI results to the same file name

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** `ci.yml` now runs `dotnet test` on the whole solution but still passes one fixed logger
  file name (`core-tests.trx`) for every project.
- **Effect:** Five projects race to write the same .trx file in the same results folder; later writers
  overwrite earlier ones.
- **Consequence:** When CI fails, the uploaded test-results artifact may only contain one project's
  results — the one that happens to have written last — making failures in the other four projects
  invisible in the artifact even though the build correctly went red.

## R5 · Stale service host ports fail slow instead of loud (non-blocking)

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit) — all five ports pre-checked

- **Cause:** `Invoke-CiPipelineDistributed.ps1` pre-checks only the API port (5100) for a leftover
  occupant. The four service ports (5101–5104) are not pre-checked.
- **Effect:** A leftover process on a service port makes the new host fail to bind; the script only
  notices after its 240-second health-wait times out.
- **Consequence:** A stale process turns a 2-second "port busy, stop it first" failure into a 4-minute
  wait with a less obvious error — annoying on the runner, worse when debugging locally.

## R6 · AGENTFEEDBACK.md still carries the old T-3600 service list (non-blocking)

**Answer:** FIXED on branch t3300-distributed-seam (review-fixes commit)

- **Cause:** The branch corrected the docs (Matching may run as its own co-located host), but
  AGENTFEEDBACK.md's T-3600 note still says only Transform/Generate/Upscale may split.
- **Effect:** The reload memory contradicts the doc it summarizes.
- **Consequence:** A future session trusting AGENTFEEDBACK.md may "fix" correct code or docs back to the
  outdated model.

## R7 · Remote upscale call blocks worker threads (sync-over-async, non-blocking)

**Answer:** FIXED — `TransformService`'s loop now runs `Parallel.ForEachAsync`; `ImagePreProcessor.Preprocess`/
`Upscale` are `PreprocessAsync`/`UpscaleAsync`, awaiting `remoteUpscale.UpscaleAsync` for real instead of
`.GetAwaiter().GetResult()`. Local GPU/CPU upscale path stays synchronous (compute, not I/O). Full solution
suite green (399/399) after conversion.

- **Cause:** Inside Transform's parallel loop, the remote upscale HTTP call is awaited synchronously
  (`GetAwaiter().GetResult()`); the loop is sync so a truly async call needs a bigger refactor
  (`Parallel.ForEachAsync`).
- **Effect:** No deadlock (verified — no synchronization context there), but up to processor-count
  thread-pool threads sit blocked on network I/O while remote upscales run.
- **Consequence:** Under many concurrent jobs, blocked threads reduce throughput for everything else on
  the thread pool. Correctness is unaffected; this is a scalability cost. Natural moment to fix: when R1
  threads the cancellation token through the same chain.

## R8 · ServiceHost GPU roundtrip tests flaked on the test client's 100-second timeout

**Answer:** ROOT CAUSE FOUND, FIXED — same bug as R1's production finding, in the test harness

- **Cause:** `WebApplicationFactory.CreateClient()` returns an HttpClient with the default 100-second
  timeout. Inside `Prism.Core.Tests`, the ServiceHost collection runs in parallel with
  `PipelineIntegrationTests`, whose pipeline work holds the shared Real-ESRGAN session lock. The
  remote-upscale routing test then queues behind it; when its roundtrip crossed 100 seconds, the test's
  own client aborted the in-flight request (trx pinned it: `RemoteUpscaleRoutingTests`, 2m25s).
- **Effect:** Intermittent single-test failures with TestHost abort stacks ("Error while copying content
  to a stream", `ResponseBodyReaderStream.CheckAborted`) in full-suite runs only — standalone runs
  finish under 100s and stay green. Three occurrences across three full runs; identical mechanism to the
  production defect the review found (R1's infinite-timeout rationale), just in the test client.
- **Consequence / fix:** The fixture's `Client` now sets `Timeout = Timeout.InfiniteTimeSpan`, mirroring
  the production `ServiceHttp.CreateClient`. `-m:1` stays in `ci.yml` anyway: serialized projects give
  deterministic timing and per-project trx that already proved its worth pinning this. If any abort
  recurs after this fix, log it here — it would be a genuinely new problem.
