# PRISM Agent Tickets — Archive

Done tickets, moved here by /ticket-finish to keep `jb/ticketboard/AGENT-TICKETS.md` (read every session
start) lean. Newest at the top. When a ticket closes, its `jb/ticketboard/T-XXXX.md` body is appended here
and that file is deleted.

**How this file is kept (compacted 2026-08-12).** `jb/docs/` is the authoritative record of accepted
knowledge — this file is not a second copy of it. Three rules:

1. **A closed ticket keeps only what nothing else records.** Spec scaffolding (*What to do* /
   *Acceptance* / *Files*), reviewer verification narratives, test counts and build-clean statements are
   dropped on compaction — the code and the docs are the record now.
2. **Definitive text that also lives in `jb/docs/` is removed, not duplicated.** The entry keeps a
   pointer instead. `AGENTFEEDBACK.md` is reload-memory scratch, not a destination — a fact that only
   lives there is still worth keeping here.
3. **Entries older than about a month collapse to one line** — they are "how things are" now. They sit
   in **Settled** at the bottom. A recent ticket whose content is fully doc-owned is demoted there too.

What survives, therefore, is: the defect or decision in a few sentences, measurements no doc carries,
residual risks with no other home, and the cross-references open tickets rely on.

### Loose ends still recorded only here
No ticket tracks these. They are flagged where they occur below.

| Loose end | Where |
|---|---|
| `BypassPhenotypes` is gone, so T-4900's two "dormant, revisit at the flip" defects are now live and unrevisited | [[T-4900]] |
| Bracket 5 (`FilenameToCellMatcher`) is still absent from `PRISM-match.md`'s Waterfall Matching Gates | [[T-5110]] |
| `FindByCollapsedPrefix` has no minimum key-length floor, unlike `StringMatcher`'s ≥4-char fuzzy gate | [[T-5110]] |
| `ShortIdentifierCodePattern` now keeps `_A1`/`_F2`-shaped shot suffixes whole as identity; untested | [[T-5100]] |
| The `OMB-E129-TGV_*` reasoning was never written into `PRISM-match.md` as its acceptance required | [[T-5100]] |
| The S109 confidence-literal rule (named-const, never config, until T-2600) was deleted from `AGENTFEEDBACK.md` and now survives only below | [[T-4400]] |

New ticket bodies go directly below this line, newest first.

---

### T-6910 · Full-resolution pixel analysis runs twice per image, and the second pass is single-threaded
**Status:** Done (2026-08-11) | **Profile:** P1-feature-worker | **Review:** Approve (2026-08-11)
**Found by:** [[T-6900]] root-cause measurement.

Every image was decoded at full resolution and analysed per-pixel **twice**: `MatchingService.PrepareLambda`
→ `ImageFeatureAnalyzer.Analyze` inside the chunked `Parallel.For`, then `MatchingService.RefinePhenotypes`
→ `ImageFeatureAnalyzer.Refine`, which **re-reads the image from disk** and was a plain sequential
`foreach`. Measured on 1774 real SMASHEDLEMON45 images (13,130 MP total):

| Pass | Avg/image | Total CPU | Parallelism | **Wall clock** |
|---|---|---|---|---|
| 1 — `Analyze` | 2609 ms | 77.1 min | 8 threads (3.0 achieved) | ≈9.6 min |
| 2 — `Refine` | 750 ms | 22.2 min | **1 thread** | ≈22.2 min |

Pass 2 was 22% of the CPU work but roughly two-thirds of the wall clock, purely for want of a
`Parallel.For`. It is also far cheaper per pixel because YOLO resizes to a fixed input, so its cost is
much less resolution-driven.

**Direction 1 is what this ticket closed.** `RefinePhenotypes` now runs `Parallel.ForEach` with
`Interlocked` counters, reusing the Analyze loop's `ParallelOptions`. **Controlled measurement: 476
ms/image → 98 ms/image, 4.87×** (24 images, one process, machine idle, MaxDOP 8 on 20 cores). So
`YoloDetector.Detect`'s `RunLock` does *not* dominate Refine as feared — the decode and the
geometry/colour analyzers around it parallelize freely and are the bulk of the cost.

**Thread-safety facts, verified at source and still load-bearing for [[T-6930]]/[[T-6950]]:** all 12
`Analyzer_*` types are stateless `static` classes; `ImageFeatureAnalyzer` holds no static mutable state;
`SubjectDetector`, `ProductTypeResolver` and `PhenotypeRuleSet` expose only `readonly` config after
construction; `YoloDetector.Detect` serializes on its own `RunLock` (`YoloDetector.cs:90`);
`PhenotypePool` is constructed per call.

**The two passes are distinguishable live, which makes a stuck-looking job diagnosable.**
`ImageMatcher.RunWaterfall` emits no SSE progress, so Match is a black box — but a `Get-Process Prism.Api`
CPU-delta sample tells you which pass you are in: pass 1 sustains ~3.0 cores at ~1430 MB working set
(8 decoded images per chunk), pass 2 ~1.37 cores at ~926 MB (one image at a time).

**Three findings split out rather than left here:** the chunk barrier capping pass 1 at ~3 of 8 threads →
[[T-6930]]; a match-only run still paying for feature analysis it never reads → [[T-6940]]; direction 2
(scaled decode — changes measured values, so it needs the CiMini golden deliberately re-blessed) →
[[T-6950]].

`TempPrematchTimingHarness.cs` (Matching test project, no-op unless `PRISM_TIMING_DIR` is set) times
decode/hash/analyze/refine per image on any folder — reuse it rather than re-deriving these numbers.
Why the first end-to-end rerun looked like a regression, and why it wasn't:
`jb/docs/PRISM-postmortem-T6900-reasoning.md`.

---

### T-6900 · SMASHEDLEMON45 "hang" is feature-analysis cost, not an algorithmic defect
**Status:** Done (2026-08-11) | **Profile:** P1-feature-worker | **Review:** Approve (2026-08-11)
**Found by:** [[T-5200]] verification session.

**There is no hang. The work is real and it is O(pixels).** `ImageFeatureAnalyzer.Analyze` costs **293 ms
per megapixel**, measured directly (18.8 MP → 5514 ms, 1.4 MP → 471 ms — perfectly linear, no algorithmic
blow-up). The dataset totals **13,130 megapixels**, so feature analysis alone is ~64 minutes of
single-threaded CPU. **Measured end-to-end: 36 min 17 s**, match-only (JobID `f40f8b9d`). Every prior run
"hung" because the client timeouts were set at 8 and 10 minutes — a fifth of the job's runtime. The
reasoning failures that made this take three sessions are in
`jb/docs/PRISM-postmortem-T6900-reasoning.md`.

**The matcher was already optimal.** Live run, corroborated exactly by the fast harness:

```
images=1774  matched=1732 (97.6%)  correct=1725 (97.2%)  wrong=0  noGroundTruthRow=7
by bracket: Bracket1: 1145   Bracket2: 482   Bracket2-Permuted: 105
ko:         MATCH_NOT_FOUND: 35   MATCHES_MULTIPLE_FAMILYIDS: 7
```

**Why the 3-character tokens are already handled** (the concern [[T-5200]] raised):
`26368_725-010_1_B2C` must reach FamilyID `99984987` via Excel `RefCo` cell `26368-725/010`. Bracket 1
takes the 1145 images whose article number is unique. The other 587 belong to the 37 articles carrying
2-3 colour variants, and **Bracket 2's tokenized concatenation resolves every one** by joining
`26368`+`725`+`010` against the RefCo digit run. The 3-digit colour codes are consumed as concatenation
members, so `minNumericTokenLength: 5` never excludes them. No config or algorithm change is warranted.
`SiblingPropagator` is never reached on this dataset at all.

**Full accounting of the 49 unmatched (2.8%) — 97.2% is the arithmetic ceiling for this input:**

| Count | Articles | Cause | Verdict |
|---|---|---|---|
| 35 | 26123, 26125, 26127, 26212, 26213 | No Excel row exists for these articles | Correctly refused (`MATCH_NOT_FOUND`) |
| 7 | 26140 colour `801` | Excel has 26140 in `825`/`010`/`725` — no `801` | Correctly refused (`MATCHES_MULTIPLE_FAMILYIDS`) |
| 7 | 26316 colour `000` | Excel has only `26316-520`; matched on article alone | Wrong — see the decision below |

**Product decision (user, 2026-08-11): a contradicting colour token vetoes a unique-article match.**
`26316_520*` → `99985091` (colour agrees with the family's Excel row); `26316_000*` → must be refused.
The correct check is **not** a global colour-code dictionary — it is a per-family membership check: does
the filename's colour code appear among *this family's own* Excel rows? Implementation is [[T-6920]].

**A false lead worth recording.** Three files here are **PNGs carrying a `.jpg` extension**
(`26303_720-998_7_B2C.jpg`, `26328_010_2_B2C.jpg`, +1). A hand-rolled JPEG header scan desyncs on them
and reports 48831×65265 (≈3187 MP), which looks exactly like an out-of-memory hang. They are really
1024×1536, and ImageSharp dispatches on content, not extension, so they import fine. **Verify image
dimensions with a real decoder before believing a header-scan outlier.**

**Tooling kept:** `TempSmashedLemonMatchHarness.cs` (Matching test project) runs the real `ModelBuilder`
and real `ImageMatcher.Run` over all 1774 filenames with ground-truth scoring in ~1 s, no image decoding;
no-op unless `PRISM_SL_OUT` is set. It is a validated proxy for match-only runs and [[T-6920]]'s
acceptance depends on it. `test/test-scripts/Run_SMASHEDLEMON45.ps1` needs `-TimeoutMinutes 60` — the
10-minute default cannot complete this dataset and produces a `TIMEOUT_OR_NO_RESULT` that reads as a
pipeline defect.

---

### T-5110 · FilenameToCellMatcher cannot see a filename inside free text
**Status:** Done (2026-08-11) | **Profile:** P1-feature-worker | **Review:** Approve (2026-08-11)
**Found by:** scoring JBComplete `-Mode Match` against its golden, 2026-08-05.

**Cause.** `FilenameToCellMatcher.GetOrBuildIndex` treated the *whole cell value* as one path — basename
after the final `/`, rejected unless it carried an image extension. **Effect:** a cell could only ever
contribute one candidate, and only when its entire value was a bare path; a marketing-description cell
listing seven image URLs indexed **nothing**. **Consequence:** the last-resort bracket was inert for the
common real shape (filenames mentioned among other words), and those images KO'd as
`MATCHES_MULTIPLE_FAMILYIDS`, reading as a matcher failure rather than a missing capability.

**Fix.** Cells are split on whitespace/commas/semicolons and every piece indexed as its own candidate
path. Three details, each learned the hard way:
- An extension-less piece (`100267_7`) indexes only when its cell also contains a sibling piece carrying
  a real image extension. An initial "has extension OR has a digit" rule regressed the existing SKU
  safety test — a bare cell like `AB12` would have false-matched `AB12.jpg`.
- A collapsed-key **prefix** fallback (beyond exact-collapsed-equal) carries `100267_6  - BW001_c.jpg`,
  where the real file has a double space and a suffix present in no cell at all.
- The "exactly one family names it" uniqueness rule stays the only guardrail — widening what counts as a
  filename token widens the false-positive surface, and no score was added.

**Blocked end-to-end by an upstream ingestion gap, not by the matcher.** All seven `100267_*` still KO in
the CiMini golden. The built index is **empty** — 0 keys — because
`ModelBuilder.ColumnHasEnoughUsefulValues` (`ExcelConfig.json` → `ColumnValidity.MinimumUsefulValueRatio`
= 0.2) drops the whole `description` column: one filled cell in ~34 rows is ~3%. The cell data is gone
before `FamilyIDRecord.MergeProperty` ever runs. Whether to always retain a description-shaped column,
lower the ratio, or something else is [[T-5130]] — it was not decided unilaterally here.

**Two gaps left open.** `FindByCollapsedPrefix` has no minimum key-length floor, unlike the codebase's own
precedent (`StringMatcher`'s ≥4-char fuzzy-fallback gate) — a cell literally listing `1.jpg` could
prefix-match unrelated images. And Bracket 5 is still undocumented in `PRISM-match.md`'s Waterfall
Matching Gates section, which this ticket's own Files list committed to touching.

---

### T-5100 · Bracket 3 hands an image to a neighbouring family on brand+colour alone
**Status:** Done (2026-08-11) | **Profile:** P1-feature-worker | **Review:** Approve (2026-08-11)
**Found by:** scoring JBComplete `-Mode Match` against its golden, 2026-08-05 — the dataset was built to
catch exactly this and its README predicted the outcome verbatim.

Six images of a product with **no Excel row** were assigned a different product's family.
`OMB-E180-BV_1..6.jpg` all matched `98636303` (= `OMB-E166-BV`, a same-colour same-type bag sitting right
there to be stolen).

**Cause.** `bracket3MinDistinctTokens` is 2. The filename tokenises to `omb`, `e180`, `bv`. The brand and
the colour resolve to `98636303`; `e180` — the **only** token that identifies the product — resolves to
nothing. Bracket 3 counted how many tokens hit, not whether the discriminating one did, so 2-of-3 cleared
the bar and the match was recorded as *unique*. **Effect:** a reference that exists in no row is treated
as absent evidence rather than as a signal of absence, and the generic tokens elect the nearest
neighbour. **Consequence:** silent misattribution of a whole product's shot set — score 0.667, no KO, no
near-tie, nothing in the manifest flags it. Six images would ship renamed as another bag.

**Fixed in two files, because one was not enough.** `StringMatcher.TryMatch` now refuses outright when
any identifier-grade filename token (letters+digits, ≥`identifierTokenMinLength`) is absent from the
entire token index — checked before ranking, so generic tokens cannot outvote it. `SiblingPropagator` then
independently re-derived the same wrong match, exactly as this ticket predicted: `BuildProfile` split
`e180` into `e` (dropped, too short) and `180` (discarded by `ShotSuffixPattern` as a bare shot number).
Fixed by keeping short-letter-prefix-plus-digits tokens (`e180`, `a129` — 1-2 letter prefix,
reference-code shape) whole, while still splitting genuine word+digit tokens (`magenta76`). The two shapes
are distinguished by prefix length; nothing was disabled globally.

**This is [[T-5210]]'s concern materializing in practice** — SiblingPropagator's separate tokenizer
disagreeing with StringMatcher on the identical filename. The patch keeps `e180` whole; it does not
resolve whether SiblingPropagator should read `MatchEvidence` instead of re-deriving its own tokens.

**Two things left open.** `ShortIdentifierCodePattern` (`^[a-z]{1,2}\d+$`) is a strict superset of
`ShotSuffixPattern`'s `[a-z]\d{1,2}` alternative and is checked first, so single-letter+1-2-digit
shot/angle suffixes (`_A1`, `_F2` — CiMini has this shape in `Pareo_exotica_F1/F2.jpg`) are now kept
whole as identity instead of filtered as shot noise. No test isolates whether same-product multi-angle
shots still propagate correctly. And `OMB-E129-TGV_*` (reference is a *transposition* of a real one) stays
unmatched by the same mechanism, but the write-up of that reasoning into `PRISM-match.md` — which this
ticket's acceptance required either way — was never done.

---

### T-5000 · Filename orientation analyzer fires on garment words, not camera views
**Status:** Done (2026-08-11) | **Profile:** P1-feature-worker | **Review:** Approve (2026-08-11)
**Found by:** [[T-4970]] second pass, 2026-07-30.

`Analyzer_FilenameEvidence` wrote `hero-orientation` from whole-token filename matches. `top`, `bottom`
and `back` are common apparel nouns as well as view words, so a front-facing model shot got
`hero-orientation = TOP` — and because the filename confidence sits above every CLIP orientation score
ever recorded on real data (max 0.582), a matching token wins outright and any CLIP threshold is moot.

**The ticket's own numbers were stale and its framing was wrong — re-measuring inverted the design.** It
claimed 16 hits in 14,427 images and "it almost never fires". `test/datasets` held **17,616 images with
1,610 hits**, because of VINGINO79 — **2,847 images, 1,567 hits (55%)**, a real customer batch naming
files `..._FRONT.png` / `..._BACK.png`. The path described as vanishingly rare is the dominant orientation
signal on the one dataset that behaves like production. That settled it by measurement:

| Candidate | Verdict |
|---|---|
| Require a view-ish neighbour token | **Refuted** — VINGINO79's 1,564 hits are bare `_FRONT`/`_BACK` with no neighbour. Would destroy the signal it was meant to protect. |
| **Positional convention (chosen)** | 1,564 of 1,567 real hits put the token last; every known false positive is mid-name. |
| Drop bare `top`/`bottom` | **Unnecessary** — position already rejects all 10, and dropping them would cost `top-packshot`/`bottom-packshot` their only filename evidence. |

**Fix: the orientation token must be the FINAL token of the filename stem.** All 15 documented false
positives stop writing an orientation, `25W_538_back` still writes BACK, VINGINO79's 1,564 are untouched.
A third defect fell out of the same change: the analyzer used to scan left-to-right and stop at the first
hit, so three VINGINO79 files whose colour name is **"Deep Back"** were labelled BACK on front shots.

**Two keyword lists stay two lists, deliberately.** `DetOrderKeywordStems.json` maps filename hints to
*det slots* and carries six groups naming no orientation at all (detail, pack, label, material, lifestyle,
interior); they overlap only on the orientation-bearing groups. The token map moved out of code into
`analyzer_Config.json` (it was a values list living in code, against the config rule) with the
relationship written into the config file. Keys must be lowercase and `Validate()` throws otherwise —
filename tokens keep their case and are lowercased for lookup, so a capitalised key would match nothing.
Vocabulary widening is a calibration question and stays with [[T-4000]].

**Known residual, stated rather than hidden:** a file literally named `Product_Colour_TOP.jpg` where TOP
is a bikini top is still misread. Nothing in the filename separates that from an overhead shot; only a
neighbour rule would, and that costs the 1,564.

**Addition 2026-08-07 — a second kind of token wants the same final position, unfixed.** [[T-5120]] will
read a trailing `_1`, `_2`, `_A`, `_B` as a weak det-order signal. Sequence markers sit at the end of a
filename; so do orientation words. Append a shot number to a name that worked and the signal disappears
silently: `..._Navy_FRONT.png` → FRONT, `..._Navy_FRONT_2.png` → **nothing**. Any customer who numbers
their shots loses filename orientation entirely, which matters because CLIP orientation is itself
unreliable ([[T-5080]], [[T-2840]]). But "take the last token, or the one before it if the last is a
sequence marker" is not automatically safe — `F-MODE-GO-…-BACK-STRAP-SANDALS-01.jpg` has a numeric final
token and a `BACK` that is still part of the product name, so scanning backwards revives exactly the false
positive this ticket eliminated. **The `_FRONT_2` pattern's real frequency is unmeasured** (VINGINO79 is
the batch to check). Measure before changing the rule — this ticket's history is that its first framing
was wrong because the numbers were stale.

---

### T-4990 · Subject detector under-counts frame intersections on 1 image in 4
**Status:** Done (2026-08-11) | **Profile:** P1-feature-worker | **Review:** Approve (2026-08-11)
**Found by:** [[T-4970]] second pass, 2026-07-30 — first measurement against hand-verified ground truth
(`test/datasets/SPACINI29/RAW IMAGES/dataset notes.md`, user-authored, do-not-edit, true intersection
count for all 86 images).

At the shipped config the detector scored **65/86 = 76%, and every one of the 21 errors under-counted** —
not one over-count, which pointed at a threshold or an edge-strip width rather than noise. It matters more
than 76% sounds: `intersection-count = 0` is the hard gate on 12 of 21 phenotypes, so six images whose
subject runs off the top edge satisfied a gate ground truth says nothing in the dataset should satisfy,
and were routed to the wrong transform.

**Fixed by two thresholds in `ClassifyConfig.json` → `SubjectEdgeDetector`: `BgColorDiffThreshold`
0.15 → 0.12 and `IntersectionFraction` 0.20 → 0.02.** Nothing else moved. Re-scored: **84/86 = 97.7%,
zero under-counts, 2 over-counts**, and the 70 two-edge images (the ordinary catalogue crop) are now
perfect. Critically **no image reads 0 any more**, so the false-`full-product` gate is closed.

**Why 0.20 was never reachable.** `IntersectionFraction` is a fraction of the *whole edge-strip area*. On
a 384×30px top band a model cut at the top touches it with head and shoulders — 5-10% of that area, never
a fifth of it. `BgColorDiffThreshold` 0.15 compounded it by reading light garments on a white sweep as
background.

**The two other candidates were refuted, which also kills a hypothesis other tickets carried.**
`MaxAnalysisSize` 512 / 1024 / 2400 score **identically** at matched thresholds — so the [[T-4948]]
downscale interaction this ticket hypothesised does not exist. (The ticket cited 1024; that is
`SubjectDetector`'s value. This detector's was always 512.) `StripDepthFraction` 0.02/0.04/0.08 is
near-flat and 0.10 makes over-counts worse. `MinRunLength` must stay at 3 — raising it to 8+ costs 4
points, because a thin limb at an edge produces short runs.

**Residual risk, no held-out set exists.** Both the parameter sweep and the validation draw on the same
single hand-labelled set — SPACINI29 is the only dataset in the repo with ground-truth intersection
counts, SPACINI32 has none. Mitigated by not tuning against the 6 failing images alone, and by
`SubjectEdgeDetectorAccuracyTests`, which parses the notes file, scores all 86 real images every run, and
asserts zero under-counts *separately* from the accuracy floor — so a change that raises overall accuracy
while re-introducing an under-count still fails.

**Downstream effect, measured the same day:** the 4 phenotype mislabels [[T-4970]] attributed to detector
under-count are gone — see `jb/docs/ImageNGP/phenotype-assignment-validation.md`.

---

### T-4955 · Derived edge features go stale when the subject box is promoted
**Status:** Done (2026-08-11) | **Profile:** P1-feature-worker | **Review:** Approve (2026-08-11)
**Found by:** [[T-4800]] review of [[T-4850]], 2026-07-28 — priority raised twice since.

`ImageTransformer.PreferSubjectGeometry` overwrote `intersects-top/bottom/left/right` with the detector's
signals but left `intersection-count` and `fully-in-frame` holding values `ImageFeatureAnalyzer` had
derived from the *old* heuristic intersects. **Magnitude, not a corner case:** on SPACINI29 a run
including Transform left **36 of 86 images (42%)** with `intersects-*` contradicting `intersection-count`
(`23211041_03_A.jpg` reported one edge touched and a count of zero); the same dataset without Transform
left 0/86. **Consequence:** `front-on-model-partial` gates on `intersects-top|bottom` while
`front-on-model-full-product` gates on `intersection-count=0`, so one image could satisfy both
mutually-exclusive rules and first-rule-wins handed it to the wrong one. Tuning any phenotype threshold
against a snapshot that contradicts itself on 42% of images is tuning against noise.

**Fixed by recomputing, not by documenting the fields as pre-promotion-only.** Recompute was the only
option that survives contact with the rules: they read `intersects-*` and `intersection-count` in a single
evaluation, so "these two fields describe different moments in time" is not a contract a rule author can
work with. `WriteDerivedEdgeFeatures` recomputes both from the four booleans it just promoted, at the
single promotion call site. `SubjectPromotionConsistencyTests` builds a snapshot whose pre-promotion
values deliberately contradict the detector on every field, across all five 0-4 edge patterns, and pins
the negative too: below the promotion confidence floor nothing is promoted, so the derived pair must be
left alone.

---

### T-5060 · Det compaction reorders a family when only some of its images win a slot
**Status:** Superseded (2026-08-07) | **Profile:** P4-critical-architecture
**Found by:** [[T-4980]] item 2, 2026-08-05.

**Closed Superseded — user verdict: a bad ticket, not an Approve. It was implemented and then reverted.**

**It treated a symptom and named the wrong culprit.** The ticket blamed det compaction. Compaction only
renumbers, and it renumbered exactly what it was handed. The error was one step earlier, in the Order
stage: the fix gave an image with *no phenotype at all* an anchor of 2.5, placing it **ahead of a real
det3 winner**. An image with zero evidence was promoted past an image that had earned its slot.

**And the real defect was never touched.** On CiMini family `90861052`, `CARDIGAN_MAGENTA76_A.jpg` and
`24211507_CARDIGAN_76_MAGENTA_B.jpg` produce **no phenotype**, despite both filenames carrying a usable
trailing `_A`/`_B` sequence marker that PRISM reads neither of. Instead of asking why two ordinary product
shots classify to nothing, the ticket reordered the output so the failure was less visible, and the golden
then looked right for the wrong reason. That question is [[T-5120]].

**What replaced it (2026-08-07):** slot winners lead in slot order; images with no qualifying phenotype go
to the end of the family in filename order; no cap past det9. `DetOrderAxis`,
`AssignmentRecord.AxisPosition`, `AnchorOverflow`, `ResolveHintSlot`, `OnModelRank` and the whole
`overflowPolicy` config block are deleted.

**Consequence, stated plainly:** family `90861052` now reads DETAIL, B, A — the detail crop leads. That is
not the desired output. It is the *honest* output: what a family looks like when two of its three images
carry no evidence, and it stays wrong until [[T-5120]] lands. The old behaviour hid the same failure behind
a lucky ordering. `CiMini_Manifest_MatchesCommittedGolden` went from 20 mismatched fields to 84 as a
result; the golden has **not** been re-blessed and closing that gap belongs to [[T-4980]].

---

### T-5040 · Prune the phenotype set to the ones PRISM actually needs
**Status:** Done (2026-08-05) | **Profile:** P4-critical-architecture | **Review:** Approve (2026-08-04)
**Landed:** `69b5eba`

The 21 phenotypes in `ImageRoles.json` were authored by an agent, not derived from what PRISM has to
decide. The set is now **18**, derived from four constraints (every phenotype must be reachable,
distinguishable, and consumed; every det slot must be reachable from a phenotype) rather than trimmed by
taste. The resulting set, the evaluation order, and the governing precedence principle all live in
`jb/docs/ImageNGP/imagePhenotypes.md` — that doc is authoritative, not this entry.

**Hard constraint, accepted by the user 2026-08-04 and the reason the model is shaped this way: a
phenotype never encodes the product type.** A polo and a t-shirt are distinct product types — that
distinction belongs to the matcher — but both resolve to the same phenotype. Phenotype describes view and
composition only. Product type re-enters one step later, in `DetOrderRules.json`, which maps phenotype →
det slot *per product type*. Any candidate phenotype only one product type could satisfy is malformed and
must be rejected.

**`ghost-*` merged into `*-packshot`, judged against this ticket's own collision-keeping rule.** A
collision survives only when a *named* signal that would separate the two is specified, or
measurable-but-unmeasured. The separating property — does the garment hold a worn 3D shape — is real, and
`test/datasets/JBComplete/README.md` §4.3 names three concrete cues (waistband holds an open rounded form;
legs carry internal volume; shadows *inside* the garment opening). But no analyzer is specified and
nothing measures any of the three, so the collision did not qualify. Deleting cost nothing operationally:
every det slot listing a ghost phenotype also listed its packshot equivalent. The cues were used
successfully by a human at ~700 px, so the signal is not fictional — but every ghost rule also gated on
`hero-orientation`, which is not reliable today, so stacking a harder 3D-shape signal on an unreliable
orientation gate buys nothing until [[T-2600]] resolves.

**Both orphan phenotypes were wired in rather than deleted.** `model-detail-closeup` is the human-branch
twin of `closeup-image` and every `detail` slot listed only `closeup-image`, so an on-model detail crop
could never win a detail slot. `lifestyle-context` is the natural remainder of `lifestyle-hero`. Each
appended behind its existing sibling, so no existing win is displaced.

**`default.det6` (`"pack"`) is a documented keyword-only slot, not dead code.** Its `phenotypes` list is
empty because `packaging-visible` was removed by [[T-4700]]. `ImageOrderer.BuildCandidates` can never win
it by phenotype, but the overflow-anchor path still uses its keyword, so a `*_packaging.jpg` still sorts
there.

**Still open, deliberately:** `on-model-with-accessories` overlaps `front-on-model-partial` (all three
JBComplete scarf images satisfy both, earlier wins). It is an overlap, not a subsumption, and no image in
the repo distinguishes them — it needs data, not a rule edit.

---

### T-5030 · Normalize every input to JPG on white
**Status:** Done (2026-08-05) | **Profile:** P1-feature-worker | **Review:** Approve (2026-08-04)
**Landed:** `69b5eba`

**Two of this ticket's premises were wrong, and that changed what the work was.** (1) Compositing onto
white was already happening — `Importer.LoadImageWithExifOrientation` already called
`context.BackgroundColor(Color.White)` unconditionally and JPEG was already written for every accepted
format. (2) `clipping-path = true` was **already unreachable in production**:
`ImageFeatureAnalyzer.AnalyzeBackground` computed `hasAlpha` as `DecodedImageFormat != JpegFormat.Instance
&& HasTransparentPixels(...)`, and `Refine` always loads `NormalizedJpgPath`, which Import always writes
as JPEG — so the feature had **never once been `true`** on a pipeline image. It fired only in unit tests
that built a PNG in memory. So the real work was removing the separate alpha *capture* path, not adding a
composite.

**Product decision (user, 2026-08-04): remove `clipping-path` outright** — "reduced complexity with
identical end-result is more of a boon than a bane", and it is literally identical. Deleted from
`ImageNGP.json`, the three `ghost-*` rules, `ImageFeatureAnalyzer`, `ClassifyConfig.json` and every doc.
`AlphaSubjectCapture.cs` and the `Subject` threading through import are deleted;
`ImageRecord_INPUT.Subject` is gone; `transparent-background` is now an unconditional `false`
(diagnostic only, no phenotype rule consumes it). `SubjectDetectionResult.Producer` was **kept** — it
still separates `classical-cv` from `edge-bleed` and leaves room for a segmentation producer; that never
depended on alpha.

**Consequences other tickets depend on.** With `clipping-path` gone the three ghost rules became
character-for-character identical to their packshot counterparts and provably unreachable → [[T-5040]]
merged them. There is no alpha-derived box anywhere in the system, so `Producer = "alpha"` can never occur
→ [[T-4960]] fully obsoleted. [[T-4950]]'s "both producers encode a mask" framing is stale (only
`classical-cv` does now), but its keep / `[JsonIgnore]` / config-gate decision is untouched and still open.

**What no longer exists, stated plainly:** there is now **no signal that distinguishes "cut out against a
flat background" from "shot on a seamless white sweep."** Both present identically downstream — flat JPEG,
uniform near-white corners, `background-type = SOLIDCOLOR`, `white-background = true`. Reviving
`clipping-path` would require a genuinely new measurement, not a repurposing of anything alpha provided.

---

### T-5020 · Folder names never reach the matcher
**Status:** Done (2026-08-05) | **Profile:** P1-feature-worker | **Review:** Approve (2026-08-04)
**Landed:** `69b5eba`

An image's folder is often the only thing identifying it — `1.jpg` inside `26182-Denim-801/` is useless
alone and unambiguous with its folder. `FolderNameEnricher` existed to borrow that name and could never do
so through the normal job path. Three independent causes, all fixed:

1. **The test runner threw the folder away before upload.** `PrismJobRunner.psm1` de-duplicated by *leaf
   filename*, so `26182-Denim-801/1.jpg` and `foldercontainsID99984905/1.jpg` collided on `1.jpg` and the
   second was silently dropped; upload and ZIP entry names were both `GetFileName(path)`. A new
   `Get-PrismRelativePath` now computes each file's path relative to the submitted root (and, for
   ZIP-expanded files, relative to that ZIP's own expansion dir), and that relative path is the de-dup
   key, the ZIP entry name and the multipart part filename.
2. **The core ZIP reader stripped member folders.** `ZipHandler.cs` set `originalFileName =
   Path.GetFileName(memberPath)`; it now returns the full in-archive path with `\` normalised to `/`.
   `memberPath` itself is untouched wherever it is used for KO records, safe-extraction paths and the
   encrypted-entry lookup. No `Importer.cs` change was needed — the widened value flows into
   `InitialFullName`, and `BuildNormalizedFileName` is safe with a `/` because
   `Path.GetFileNameWithoutExtension` strips the directory part before the invalid-char filter.
3. **`MeaningfulTokens` kept a mixed letter+digit run whole and skipped the split.** The `continue` after
   adding a whole mixed run skipped the letter↔digit split below it; a `CollectRunTokens` helper now emits
   the split pieces **in addition to** the whole run, through the same length/noise/bare-number filters.

**Declined: the optional digit-run concatenation** (`26182-801` → `26182801`). It manufactures a token
present in neither the folder name nor the Excel, in exactly the shape of a real 8-digit FamilyID, so it
could collide with an unrelated family with no textual basis.

**Not verified:** whether a multipart part *filename* containing `/` survives ASP.NET Core's form parser
end to end. Source reading says yes (`AddUploadedInputRecords` sets `InitialFullName = file.FileName`
verbatim, and `/` needs no escaping in a `Content-Disposition` quoted string), but no live HTTP round-trip
was run. It matters little — only the single seed image travels loose; every other image goes through the
ZIP path, which is covered.

---

### T-4960 · Alpha-derived box should retire SubjectGeometry's colour-distance fallback
**Status:** Obsolete (2026-08-05) | **Profile:** P1-feature-worker

**Closed Obsolete, no code landed and none was supposed to.** [[T-5030]] deleted `AlphaSubjectCapture` and
the entire alpha path, so the alpha-derived box this ticket was to wire up does not exist anywhere and
`Producer = "alpha"` can never occur. Its premise — "exact geometry sits unused on the same record" — is
false at HEAD: there is no second producer to prefer, and `Analyzer_SubjectGeometry`'s colour-distance
fallback is now the only producer rather than the worse of two. The linked todo in
`Analyzer_SubjectGeometry.md` ("fallback box on transparent-background images should use alpha") is
likewise unactionable — there are no transparent-background images downstream of Import — and was retired
to that file's "Retired" section per the todo lifecycle.

---

### T-4970 · Phenotype assignment validation (first + second pass)
**Status:** Done (2026-08-03) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-31)
**Found by:** [[T-2600]] rewrite, 2026-07-29.

**Full write-up: `jb/docs/ImageNGP/phenotype-assignment-validation.md`** (indexed in `PRISM-index.md`) —
authoritative for the distribution, the threshold precision/coverage curve, and the verdict. This entry
keeps only what that doc does not.

**The first pass said "7% coverage, remedy is two config thresholds". That was tested and is wrong.**
Lowering `hero-orientation` alone changes nothing — every rule reading orientation also reads
`body-visible`, UNKNOWN on 72/86 at its own bar. Lowering all three raises coverage 7% → 62% and the extra
assignments are mostly incorrect: 5 images assigned, **all 5 wrong**.

**Three findings superseded the threshold question (user decisions 2026-07-30, implemented in `07c886b`):**
1. `model-detail-closeup` over-fired on any edge-cropped model shot (55/86 read `partially-occluded` just
   for touching a frame edge, while **zero** images are honestly detail crops) → narrowed to
   `intersection-count >= 3`.
2. `occlusion-level` was `intersection-count` in disguise (its producer derived it 0/1/2/3+ →
   full-product/mostly-visible/partially-occluded/closeup) → **deleted from the taxonomy**; all 13 rules
   now state `intersection-count` directly. To return later as a real measurement.
3. A back view cut by a frame edge is 38/86 (44%) and had no correct label at any threshold →
   **`back-on-model-partial` added**, per-user det slots: topwear det1 *ahead of*
   `back-on-model-full-product`; bottomwear new det5 (lifestyle/label/material shift one later); footwear
   new det7 before lifestyle; bags-accessories + default share the existing back slot behind
   `back-packshot`, so a real back packshot always wins.

**Measured effect of the rule changes alone**, at a 0.30 bar against hand-verified labels over all 86
images: **correct 15 → 33, wrong 38 → 13.** All 13 survivors are upstream — 9 CLIP orientation errors, 4
detector under-counts — none fixable in `ImageRoles.json`. The rule replay reproduces the shipped pipeline
**86/86 exactly**, so the rule engine itself is sound.

**The measurement apparatus was verified reliable, which is what makes the numbers usable.** Two full runs
(fresh process, fresh ONNX load) produced **byte-for-byte identical** JSON; output is substantive not
vacuous (86/86 OK, 0 KO, all carrying influential CLIP tags across a real confidence range); every feature
records value + confidence + producing source (`clip`/`yolo`/`heuristic`); one positive and one negative
assignment were **re-derived by hand** from the dump plus `ImageRoles.json` alone; and the threshold gate
is exact — 37 images score below the 0.33 bar and exactly 37 read UNKNOWN.

**Ceiling explained, not just asserted.** The highest `hero-orientation` confidence observed anywhere on
SPACINI29 is **0.5817**, so the pre-`97326fe` bar of 0.60 sat above every score CLIP can produce here —
that is the mechanism behind the original "7% coverage" finding. At the shipped thresholds coverage is
**37.2% (32/86)**, and **27 of 86 now get a real phenotype-driven det slot** where previously 100% of OK
images left Ordered with `IsOverflow: true`.

**Deliberately not measured, still open.** Whether those 32 assignments are *correct* (that is the
accuracy question, separate from reliability). And the non-solid-background half: MMERO26 is the only such
dataset and a 60-image subset KO'd 59/60 on `MATCHES_MULTIPLE_FAMILYIDS` — a KO'd image never reaches
`Refine` — so `lifestyle-hero`/`lifestyle-context` remain unmeasured. Ground truth now exists at
`test/datasets/SPACINI29/RAW IMAGES/dataset notes.md` (user-authored, do-not-edit); it corrected this
ticket's own scoring — **no SPACINI29 image is fully in frame** — and gave the first accuracy figures:
`intersection-count` 65/86, `hero-is-human` 85/86.

**Coverage gap flagged at review, does not block Done:** no direct `ruleSet.Assign(...)` test exercises
`back-on-model-partial` — neither positive nor negative — while every other materially-rewritten rule has
at least one. The SPACINI29 measurement covers it end to end, so it is a coverage gap rather than a
correctness risk. Close it before the rule is trusted unattended.

**Spun off:** [[T-4990]], [[T-5000]], [[T-4980]], [[T-5010]]; [[T-4955]] reclassified from cleanup to
prerequisite.

---

### T-4900 · ESRGAN toggle + unified final-size upscale (epic)
**Status:** Done (2026-07-30) | **Profile:** P0-orchestrator
**Found by:** 2026-07-28 upscale-perf investigation.

Index ticket, five children (T-4905/T-4910/T-4920/T-4930/T-4940), all Done with reviewer Approve. The
upscale stage was the pipeline's dominant cost — **122.9 s per 800×800 image on the GPU** with the old
fixed-64 model. Goal: make ESRGAN opt-in, default OFF, with both paths targeting the same exact
final-output-size bar. **The decisions and the resulting behaviour live in
`jb/docs/PRISM-transform-generate.md` → "Unified upscale" and `jb/docs/PRISM-api.md`** — including the 740
px pass-through threshold and why the Lanczos-only cap is unreachable on the centre-and-stretch route.

**⚠ Two defects the reviews found were parked on "not reachable while `BypassPhenotypes = true`; revisit at
the flip". The flip happened — [[T-5010]] removed `BypassPhenotypes` on 2026-07-31, and `git grep` finds
no occurrence in `jb/src` today. Neither was revisited and no open ticket carries them:**
1. `FinalOutputSize.RoutesToCenterAndStretch` encodes only 2 of `SelectTransformer`'s 3 branch conditions
   — bbox-null and edge-intersect, but **not** the `SelectedPhenotype is null` half of Step 1. For a
   bbox-present / no-intersect / phenotype-null record the predicate says centre-and-stretch while the
   real routing says `Tx_ProblemImageProcessor` ([[T-4910]]).
2. `Tx_ProblemImageProcessor` derives its `OutputWidth`/`OutputHeight` metadata from
   `InputImage.Width`/`Height` — the deliberately-unscaled original-resolution field — while its actual
   resize reads real decoded dimensions ([[T-4920]]).

**Three defects the epic uncovered and fixed along the way** (user decisions 2026-07-29; all three were
blocking the epic's own premise, not scope creep):
1. **The bounding box was never rescaled after upscale.** `UpscaleAsync` enlarged the bytes while
   `lambda.BoundingBox` stayed in original-image pixels, so `Tx_CenterAndStretch` cropped an
   original-coordinate rect out of an enlarged image, and the canvas was still sized off the un-scaled
   bbox so the output never reached 800px anyway. The ON path was paying full ESRGAN cost for an output
   that met neither the crop nor the size it claimed.
2. **`Tx_CropSquare.Transform` never applied its crop.** It recorded a `CropRectangle` on the OutputRecord
   without touching `ProcessedBytes`, and Export ships `ProcessedBytes` — so the exported file was the
   whole frame while the manifest claimed a square.
3. **Upscale sized against the pre-promotion box.** Subject promotion and shadow accounting ran *after*
   preprocessing, so upscale measured a box Transform then replaced. Both moved into
   `ImageTransformer.FinalizeGeometry`, called from `PreprocessAsync` before the upscale decision.

---

### T-4905 · Dynamic-shape ESRGAN export + even-dimension padding
**Status:** Done (2026-07-29) | **Profile:** P4-critical-architecture | **Review:** Approve (2026-07-29)

The committed `Real-ESRGAN_x2plus.onnx` had a fixed `[1,3,64,64]` input, so an 800px image was upscaled as
**625 serialized 64×64 tile Runs** (~0.2 s DirectML dispatch overhead each = 122.9 s). The RRDBNet is
already spatially size-agnostic internally (`pixel_unshuffle` derives shape from `Shape(input)`; both
Resize use scales `[1,1,2,2]`) — only the *declared* input shape pinned it to 64. A **metadata-only** edit
(input dims → dynamic `height`/`width`, weights untouched, bit-identical output) makes it accept whole
images in one Run: **122.9 s → 10.19 s, ~12×**. The review verified this by loading both `.onnx` files and
hashing all 702 initializers in each — identical SHA256, identical 1226-node graph, sole difference the
declared input.

`Upscaler.RunTiled` rounds the whole-image tile up to even H/W — `pixel_unshuffle(2)` rejects odd dims and
the existing pad+accumulator clips the ×2 overshoot back. Whole-image single-pass is the chosen mode; a
configurable capped tile (e.g. 512) is the documented fallback if a large image ever OOMs the GPU. The
dynamic `.onnx` is gitignored (too big for git) and lives in the source tree next to the fixed-64 backup.

---

### T-4910 · Exact final-output-size calculator (shared helper)
**Status:** Done (2026-07-30) | **Profile:** P4-critical-architecture | **Review:** Approve (2026-07-30)

`FinalOutputSize` (`jb/src/core/Services/Transform/FinalOutputSize.cs`) owns four things:
`HasEdgeIntersect`, `RoutesToCenterAndStretch` (the routing predicate, now also used by
`ImageTransformer.SelectTransformer` and `ApplyShadowAccounting` — one predicate, no copies),
`CenterAndStretchCanvasSize` (which `Tx_CenterAndStretch` now calls instead of holding its own copy), and
the forward/inverse pair `LongestDimension` / `MinimalScaleToReach`.

**The inverse is not solved algebraically** — it takes the continuous inverse of the canvas formula
(provably never above the answer, since floor/even/trim only ever shrink the canvas) and steps up against
the forward function until the bar is cleared. Converges in ≤3 passes and cannot land a pixel short the
way hand-derived algebra can. The review brute-forced the pair against the true minimum across margins
0.0001–0.1999 × targets 1–2000: zero mismatches, worst case 3 iterations.

Scope grew past "no behaviour change yet" because two of [[T-4900]]'s three defects sit inside this
ticket's remit: geometry promotion had to move ahead of upscale (new `ImageTransformer.FinalizeGeometry`,
promotion result recorded on `ImageRecord_LAMBDA.SubjectGeometryPromoted` so the evidence line survives
the move), and `Tx_CenterAndStretch` had to read the shared helper for the single-source-of-truth
acceptance to mean anything. See [[T-4900]] for this ticket's unrevisited routing-predicate gap.

---

### T-4920 · Unified upscale-scale + ESRGAN/Lanczos gate + KO
**Status:** Done (2026-07-30) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-30)

`UpscaleAsync` rewritten to the unified model: minimal scale from
`FinalOutputSize.MinimalScaleToReach(MinOutputWidth, …)`, then the toggle picks resampler and cap only.
Past the applicable cap → `PREPROCESS_UPSCALE_EXCEEDED`, and the OFF message appends "Enable ESRGAN
upscaling to process this image." The too-small KO is retained and now measures the **promoted** box — the
change that later surfaced as [[T-4980]]'s `PREPROCESS_TOO_SMALL` case.

**Geometry follows pixels.** `ScaleGeometryToUpscaledImage` moves `BoundingBox` and `LegacySalientBox` into
the enlarged space and the BGR `Mat` handed downstream is re-decoded from the new bytes; width and height
scale first and are never clamped, so the longest side lands on exactly the pixel count the scale was
derived from, with the origin absorbing the ≤1px rounding overhang. Deliberately **not** scaled:
`ImageRecord_Base.Width`/`Height` (the original-resolution contract Export depends on) and
`lambda.Subject` (pre-upscale evidence, self-consistent with its own mask).

**One config-rule finding, fixed here and worth remembering:** `MaxLanczosOnlyUpScaleFactor` was declared
`{ get; private set; }` to match its ~40 legacy siblings, but the no-shadow-defaults rule binds *new or
touched* config code regardless of the surrounding class. Note `private set` **cannot** carry `required`
(CS9032 — the setter would be less visible than the type), so this is an `init` accessor, not a one-word
change. **Still uncovered, pre-existing:** no test exercises the `MaxLanczosOnly > MaxUpScale` invariant or
a missing-key load failure, because there is no `PrismConfiguration` test file anywhere in the repo. See
[[T-4900]] for this ticket's unrevisited `Tx_ProblemImageProcessor` metadata defect.

---

### T-4930 · ESRGAN toggle plumbing (per-job parameter, default OFF)
**Status:** Done (2026-07-29) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-29)

`PrismProcessingParameters.AllowEsrganUpscale` (no initializer, so an omitted field is false),
`PrismProcessRequest.AllowEsrganUpscale`, mapped in `PrismProcessIngressReader`, read once in
`TransformService` and passed to `PreprocessAsync`.

**Deviation from the spec, deliberate:** the flag is read off `matched.Ingest.Parameters` inside
`TransformService` rather than threaded as a method argument like `headcut`. The parameters already ride
inside `MatchingResult` across the matching→transform HTTP boundary and the ServiceHost route reads
`Transform`/`Headcut` exactly this way, so one read cannot be dropped at a call site; the alternative was
signature churn across `ITransformService`, `Pipeline`, `PrismService`, the ServiceHost route and the HTTP
client for a boolean already on the record. Wire-format agreement verified concretely: `HttpTransformService`
POSTs via `ServiceHttp.Json` (`PropertyNamingPolicy = null`) and the ServiceHost's
`ConfigureHttpJsonOptions` sets the same. The get-only-collection trap from the microservices split does
not apply — these are `bool { get; init; }`.

**Known gap, pre-existing and shared:** the `PrismProcessRequest` → `PrismProcessingParameters` mapping is
untested, because there is no `Prism.Api` test project and the request record is `internal`. `Rename`,
`Transform`, `Generation`, `Format`, `ReturnOriginalImages` and `SkipClassification` all share it.

---

### T-4940 · Workbench UI toggle for ESRGAN upscaling
**Status:** Done (2026-07-29) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-29)

Added as a fifth entry in `JobParameterPanel`'s `binaryParameterFields` ("High-quality upscaling (ESRGAN —
slower)"), so it renders through the same checkbox path as the existing four rather than introducing a
parallel control. `allowEsrganUpscale` added to the TS interface, to `defaultParameters` as `false`, and to
both request builders (the match-lite builder hardcodes `false` alongside its other disabled options). No
web test framework exists in this repo, so there is no component-test gap introduced.

**Noted, not fixed:** `Headcut` is on `PrismProcessingParameters` server-side but is not on
`PrismProcessRequest` and has no UI control — it cannot be set by any caller today.

---

### T-4800 · Model-aware subject isolation for Transform (epic)
**Status:** Done (2026-07-28) | **Profile:** P0-orchestrator
**Found by:** [[T-4700]] follow-up.

Index ticket; design lives in `jb/src/core/Services/Transform/Engine/jbtodo.md` ("Subject Isolation &
Model-Aware Transformation"). Goal: give Transform a real subject mask/box (shadow- and
background-excluded) produced **upstream** and consumed as pure geometry+fill, plus Excel+CLIP seeding
that steers transform behaviour. v1 ported the vendored classical-CV prototype
`jb/docs/reference/process_images.py`; ONNX stays upstream so Transform stays deterministic. Children:
T-4805, T-4810, T-4820 (Wave 0); T-4830 (Wave 1); T-4850, T-4860 (Wave 2); T-4870 (Wave 3).

**Detector stage move (user decision, 2026-07-28) — the durable architectural choice here.**
`SubjectDetector` moved out of `ImagePreProcessor.PreprocessAsync` (Transform stage) into
`ImageFeatureAnalyzer.Refine` wave 3, directly before `FinalizePhenotype`. That is the only point where
every precondition holds at once: the FamilyIDRecord is resolved (Excel seed available),
`Analyzer_ProductColor`/`Analyzer_BackgroundColor` have just run two lines earlier (the toggle-(a) seed),
the image is already decoded and shared across the analyzer chain (no second decode from disk), and the
phenotype is not yet assigned — which is what makes a detector-measured `shadow-present` a *usable*
feature instead of one that is always UNKNOWN when the rules evaluate. Transform then only reads
`lambda.Subject` and detects nothing.

**Both original deferrals were pulled into scope** (user, 2026-07-28): ingress alpha capture was built
(and later deleted wholesale by [[T-5030]]), and seed-aware detection's stated blocker ("seed resolves
after preprocessing") turned out to be an ordering accident — `TransformSeed.Resolve` sat seven lines
below the `PreprocessAsync` call in the same method.

---

### T-4805 · Unify Transform/Process entry points (fix latent divergence)
**Status:** Done (2026-07-28) | **Profile:** P4-critical-architecture | **Review:** Approve (2026-07-28)

`Tx_*.Process` methods ignored the `lambda` parameter and always cropped to `FullImageBounds(arr)`,
violating the `IImageTransformation` contract. Not live — the deployed transform service routes through
`Transform(lambda)` — but a future per-image webservice on `Process` would have diverged from pipeline
behaviour and ignored the persisted SubjectBox. Both paths now funnel through one shared core
(`CropResizeAndStretch`), not a duplicated patch. All four Tx classes were re-read rather than trusted:
`Tx_DetailCropper` already honoured the lambda; `Tx_CropSquare`/`Tx_ProblemImageProcessor` never read a
bbox at all, so the divergence could not occur in them. Dead `Tx_LowContrastEnhancement.Enhance` removed;
the standalone CLAHE `Process` utility retained. **Not covered:** a null-`BoundingBox` failure path
reaching `Process`.

---

### T-4810 · Persisted subject mask/box contract
**Status:** Done (2026-07-28) | **Profile:** P4-critical-architecture | **Review:** Approve (2026-07-28)

Persisted `SubjectMask` + `SubjectBox` + per-edge intersect flags on the image record, produced upstream
and read by Transform, with a pluggable-producer seam so a segmentation producer (SAM3 / yolo26s-seg,
[[T-2600]]) can replace the v1 classical-CV producer without touching Transform. The seam is genuinely
swappable — Transform consumes `SubjectDetection` generically, never `SubjectDetector`-specific state.

**Answered planner question, still worth knowing: the no-shadow-defaults rule does *not* extend from
config classes to runtime data contracts.** The rule is about config that loads from JSON and must fail
loud on a missing key. `SubjectDetection` is a data-carrying contract and follows sibling convention
(`ImageRecord_LAMBDA`, `BoundingBox`), which is what landed — the ticket's "following the
no-shadow-defaults rule" phrasing was loose. Same conclusion reached independently by [[T-4550]].

---

### T-4820 · Seeding access in Transform
**Status:** Done (2026-07-28) | **Profile:** P4-critical-architecture | **Review:** Approve (2026-07-28)

`TransformSeed` threads the already-measured `product-color`, `background-type`, `background-color` and
the resolved `ProductTypeId` to Transform, plus each lambda's `FamilyIDRecord`. It is genuinely
data-access-only — every signal read from the already-populated feature snapshot, nothing recomputed. A
missing `FamilyIDRecord` is modelled as a first-class absent case (unmatched image, or a job with no
Excel), not a defensive null-coalesce and not a throw. The seed surfaces `ProductTypeId` rather than the
ticket's `product-type-label` — the Excel-authoritative slug is the better signal and consistent with
"product = Excel + CLIP".

---

### T-4830 · Port the v1 subject detector (+ ingress alpha path)
**Status:** Done (2026-07-28) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-28, 2nd pass)

The vendored `jb/docs/reference/process_images.py` detector ported to C#/OpenCvSharp4: chroma-plane +
texture + shadow-strip-by-shape + Canny corroboration, with **lightness never a criterion** — that is the
algorithm's single defining invariant and what excludes cast shadows. Background is genuinely fitted as a
least-squares plane (same 500-sample cutoff as the reference). Classify-stage cost on SPACINI29:
**+18.3 s / +11.7%**.

The first review pass blocked on three of four mandated test scenarios being absent — white-on-white /
texture-only, cast-shadow exclusion, and gradient background (every test used a uniform backdrop, so the
plane-fit coefficients were trivially zero and the plane fit was never exercised). Worth remembering as a
test-design failure mode: a fixture that is too clean can make a whole code path untested while the suite
reads green. Also flagged at the time: `MaxAnalysisSize` was set to 1024 against the reference's 2400,
with an explicit reference-author warning that fabric weave disappears when this is low. (The ingress
alpha path added here was deleted wholesale by [[T-5030]].)

---

### T-4850 · Consume subject mask/box in Transform
**Status:** Done (2026-07-28) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-28, 2nd pass)

Centre/stretch/detail-crop geometry now operates on the real SubjectMask/SubjectBox instead of the salient
rectangle, and routing uses the detector's cleaner intersect signals. Fill stays the existing
`Tx_util_BgStretch`, unchanged, just fed better geometry.

**Two blocking findings, both instructive.** (1) `PreferSubjectGeometry` claimed in both its comment and
the design doc to promote a *confident* subject but never read `Subject.Confidence` — it gated only on the
whole-frame flag, so a 0.1-confidence sparse-blob detection overrode the legacy bbox unconditionally,
including where a null legacy bbox previously routed safely to `Tx_ProblemImageProcessor`. Now gated on a
config-driven floor, and the gate does real work: **71 of 86 SPACINI29 images promote, and the 15 that do
not are exactly those below the 0.35 floor.** (2) Promotion overwrote the legacy salient bbox with no copy
retained, making the ticket's own A/B acceptance unverifiable from a run's evidence; the pre-promotion box
is now kept on the record and emitted as evidence.

---

### T-4860 · Behavior toggles + shadow wiring
**Status:** Done (2026-07-28) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-28, 2nd pass)

**Seeding behaviour settled (user, 2026-07-28)** — see `Services/Transform/Engine/jbtodo.md`:
- **(a) product-color ≈ background-color → this is where CLAHE belongs.** When the product colour is
  clearly distinct from the background, CLAHE is superfluous and is skipped; it earns its cost only when
  the two nearly match and the weave has to be lifted clear of the noise floor.
- **(b) background not flat → a second discrimination step decides the treatment.** B1 = soft gradients
  plus minor noise/dust (a photo-studio sweep) gets one treatment; B2 = a real-life background triggers
  `HeroDetectionOnSteroids`, the documented everything-we-have escalation path (prior evidence, yolo26n,
  saliency, whatever helps). Deliberately not built out; the method exists and is named so the escalation
  path is explicit rather than implied.
- **(c)** detector candidate-shadow evidence drives the existing `Tx_CenterAndStretch` shrink.

**Two blocking findings.** (1) `background-type = UNKNOWN` was normalised to null and therefore read as
*flat*, identical to a known `SOLIDCOLOR` — inverting the spec, since UNKNOWN is precisely not SOLIDCOLOR.
(2) The shadow shrink was applied unconditionally *before* routing, so it perturbed
`Tx_CropSquare`/`Tx_DetailCropper`/`Tx_ProblemImageProcessor` inputs too, where the ticket scopes it to
`Tx_CenterAndStretch`.

**Real-data outcome:** toggle (a) fires on 19/86, the shadow toggle on 23/86 after calibration. **Toggle
(b) fires on 0/86** because SPACINI29 is entirely `SOLIDCOLOR` — so the `HeroDetectionOnSteroids` path has
no real-data coverage at all, tracked in [[T-4945]]. Note on toggle (a): the colour comparison is exact
string equality on categorical palette names, because that is the only product/background colour data that
reaches Transform — coarse (misses "ivory" vs "white") but the best available from today's signals.

---

### T-4870 · Populate the transform-evidence carrier (detection/toggle evidence)
**Status:** Done (2026-07-28) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-28, 2nd pass)

**Re-scoped (user, 2026-07-28).** Originally worded as "the transform manifest carries the new evidence",
which the reviewer correctly failed: `transform-manifest.json` does not exist anywhere in the codebase and
`Exporter.cs`/`Prism_Config.json` were untouched. What landed is the *carrier* —
`OutputRecord.SafeSummaryText` carrying detection evidence (producer, box, confidence, per-edge
intersects, hard-shadow flag, whole-frame flag), the three toggle states, the pre-promotion legacy salient
box and whether promotion fired, in a stable parseable `key=value;` encoding. No parallel evidence store;
the pixel mask is correctly kept out of the text field. Emission of the manifest file itself stays with
Export Todo 4, which owns the `Manifests` config section and the per-Tx parameter capture — this ticket
leaves it a pure serialization job with the data already in place.

---

### T-4700 · Remove unimplemented analyzers; trim ImageNGP/ImageRoles/DetOrderRules to real+reachable only
**Status:** Done (2026-07-27) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-27)
**Landed:** `fe9ac38`

`ImageNGP.json` declared 60 features and 26 phenotypes, but only 11 of 21 analyzer classes were
implemented — the other 10 were empty-body stubs. **Because `PhenotypeRuleSet` treats `UNKNOWN` as never
satisfying a required condition, every phenotype gated on a stub-only feature was mathematically
unreachable**: 6 of 26 phenotypes dead on arrival, cascading into 13 of 19 `DetOrderRules.json`
product-type tables carrying an inert det slot. First half of the user-directed "simplify by subtraction,
then re-expand piecemeal" effort (see [[T-4000]], [[T-2600]]).

Deleted the 10 stub `.cs`/`.md` pairs and their call sites; features 60 → 37 (including the structurally
dead `background-type=STUDIO` enum value); phenotypes 26 → 20. **Every `DetOrderRules.json` slot that lost
its only phenotypes became `[]` rather than being deleted, preserving overflow slot numbering** — the
detail that keeps this a pure removal of unreachable paths. `jb/docs/ImageNGP/HowToAddAPhenotype.md` was
written here: the full analyzer→feature→phenotype→det-order wiring chain with a worked example.

Removed features that may return later, per the milestone table: `contains-mannequin`, `face-visible`,
`pose-type`, `camera-angle`, `top-view`, `packaging-visible`, `text-present`, `logo-present`, `lighting`.

---

### T-4400 · Adopt Roslyn analyzers: SA1402/SA1649/SA1101/SA1633/S109
**Status:** Done (2026-07-24) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-24)

`StyleCop.Analyzers` + `SonarAnalyzer.CSharp` wired into every production `.csproj` via
`jb/src/Directory.Build.props` (test projects excluded by `*Tests*` name match, to keep S109 off test
literals). Curated severities in the root `.editorconfig`. **SA1402 / SA1649 / S109 / SA1101 are at zero
and CI-gated** (`-warnaserror:SA1402,SA1649,S109,SA1101` in `ci.yml`). `jb/src/SonarLint.xml` sets S109's
allowed magic-number exceptions to `0, 1, -1` — do not widen it casually, that is the point of the rule.

**Two rules suppressed permanently, both because they contradict house style:** SA1500 enforces Allman
brace placement against the K&R rule, and SA1633 (per-file header) is pure noise under the class-level
`/// <summary>`-only comment convention. SA1025/SA1503 are deferred, not rejected.

**⚠ Standing rule that now survives only here** — the `AGENTFEEDBACK.md` bullets carrying it were deleted
2026-08-12. **S109 triage default is a named `private const` at point of use, not config.** ~163 warnings
across ~30 files (file-format magic bytes, RGB/luma/CHW-tensor math, alpha thresholds, pixel-sample
strides, switch-pattern case values, config-validation bounds) were named in place — zero behaviour
change. Only one file was genuine infra tuning (`WetransferClient.cs` → `HostRules.json`'s
`weTransferPolling` section). **Per-feature CLIP/heuristic confidence weights** (`ImageFeatureAnalyzer.cs`,
`NumericMatcher.cs`'s `SubstringRescueConfidence`, `SiblingPropagator.cs`'s
`SiblingPropagationConfidence`, `StringMatcher.cs`'s `NonExactTokenMatchConfidence`) were named-const'd
too and **explicitly NOT moved to config** — calibration is an open product question tracked by
[[T-2600]], and inventing config surface for values that will likely be redesigned once it resolves would
just create churn. Apply the same treatment to any newly-discovered confidence literal until T-2600
resolves.

---

### T-2820 · Ordered stage assigns non-deterministic det-slots for tied images within a family
**Status:** Done (2026-07-15) | **Profile:** P1-feature-worker | **Review:** Approve (2026-07-15)
**Found by:** [[T-2800]] end-to-end verification (2026-07-02).

Three consecutive CiMini Full runs against the same unchanged build once produced three different det-slot
assignments for images tied within a family (`94613033` → det10, det8, det9; `90861083` flip-flopping
det8/det9), which made any golden-file test unsafe for a family with more than one image at the same
precedence tier.

**Closed as acceptance-met / not-reproducing (user, 2026-07-15).** Five consecutive `-Mode Full -Dataset
CiMini` runs on an unchanged build were byte-identical to golden, including both tied families. Mechanism
confirmed: `ImageOrderer.CompareCandidates` already tie-breaks on `string.CompareOrdinal(Filename)` before
`SourceIndex`, so exact ties are deterministic and input-order-independent.

**The residual risk is real and is what [[T-2840]] pursues.** CLIP/NGP confidences differing by GPU
float noise produce *near*-ties, which flip ordering **before** the filename key ever engages. **User
direction: the fix lives in CLIP refinement, not a tie-break hack** — if two images in a family score
near-identically, that is the classifier failing to distinguish them, and a deterministic secondary key
would only freeze an arbitrary answer in place. Look at the model side, `ClipPrompts.json`, and the
thresholds before adding tie-break machinery.

---

## Settled — closed on or before 2026-07-14

One line each. These are "how things are" now; where a decision needed a permanent home it is in
`jb/docs/` (start at `PRISM-index.md`) or in the code itself.

| Ticket | Closed | Outcome |
|---|---|---|
| T-4110 · Unify ONNX Runtime execution-provider policy | 2026-07-20 | Single pinned `Microsoft.ML.OnnxRuntime.DirectML` (CPM in `Directory.Packages.props`); `OnnxSessionFactory` is the sole session-construction path for CLIP/YOLO/Upscale; conventions-hook category `onnx-session-bypass` enforces it. Full policy, the no-algorithm-switching-on-GPU rule, and the health-probe caveat: `jb/docs/PRISM-model-runtime.md`. |
| T-4600 · SSE progress carries no per-item counts or blocked state | 2026-07-20 | `StageProgress.EmitCompleted` populates `CompletedCount`/`TotalCount`/`Severity` (Warning when koCount>0), wired from `IngestService` (Import) and `Pipeline.ExportAsync` (Export); `StatusPanel.tsx` renders a blocked-state chip. Mid-pipeline stages deliberately stay `EmitStarted`-only. |
| T-4710 · Collapse DetOrderRules/ProductTypeMap to 5 product types | 2026-07-27 | 19 → 5 (`default`, `topwear`, `bottomwear`, `footwear`, `bags-accessories`); the other 13 fall back to `default`. `bottomwear` = `clothing-bottoms`' table verbatim (user tie-breaks). `OrderEvidence.WinningPhenotype` exposed on the export manifest so a consumer can see *why* an image landed in a slot. `jb/docs/ImageNGP/PRODUCTTYPES.MD`. |
| T-3300 · Validate and complete the Phase 2 distributed-services seam | 2026-07-17 | Proven by CI run `29451640778`: in-process and 4-separate-`ServiceHost` runs produce identical CiMini goldens. Real-HTTP roundtrip test per `Http*Service`; test projects split per public service (`jb/docs/PRISM-testing.md`). |
| T-3500 · Fuse Import→Match in-process handoff | 2026-07-15 | **Measured and rejected.** Decision + the SPACINI29 numbers: `jb/docs/PRISM-io-import.md` → "Import→Match Handoff: Disk Is the Contract". No production code changed. |
| T-3600 · Matching's HTTP contract assumes a shared filesystem | 2026-07-15 | Option (b) chosen: Ingress + Matching + Export are always co-deployed. `jb/docs/PRISM-io-import.md` → "Co-Deployment Contract". `MatchingService.MatchAsync` now throws an explicit co-deployment error instead of KO-ing every image. |
| T-4100 · Health reports CPU-only on a GPU dev machine | 2026-07-15 | Not a GPU regression — `SupportedRuntimeProviders = ["CPU"]` was a hardcoded string literal that never queried ONNX. Now `OrtEnv.Instance().GetAvailableProviders()` plus a per-session `SessionRuntimeProviders` field. Surfaced the version skew and YOLO-CPU-only issues that became T-4110. |
| T-3700 · Align project/assembly names with the Services/ restructure | 2026-07-15 | Three engine `.csproj` renamed to `Prism.Services.*`; missing Upscale entry added to `PRISM.sln`; stale `Images` solution folder replaced by `Services`. Pure identity rename, no runtime change. |
| T-2830 · `_det#` numbering starts at det8 instead of det0 | 2026-07-15 | Already fixed; the requested toggle-able collapse pass already existed. `ImageOrderer.CompactDetOrder` + `Output.DET-ORDER-GAPS-ALLOWED` — `jb/docs/PRISM-order-rename.md`. |
| T-4500 · Master: generic ConfigLoader + Transform cleanup | 2026-07-14 | Index ticket for T-4510…T-4560, all Done and individually reviewed. |
| T-4560 · Migrate remaining PRISM to ConfigLoader; retire PrismConfigLocator + ConfigCache | 2026-07-14 | All 23 + 9 call sites migrated; both classes deleted. **Do not re-add a config cache** (measured worthless: 62 KB of config total, loaded once per job) and `PrismConfigurationException` is the single fail-loud config type — `jb/docs/PRISM-pipeline-core.md`. |
| T-4550 · Fold ImageTransformationResult into ImageRecord_OUTPUT | 2026-07-14 | Record lifecycle completed (Base→INPUT→LAMBDA→OUTPUT); Transform fills the transform block, Export enriches the same instance. `TransformStatus` is nullable so "never evaluated" stays distinguishable. `jb/docs/PRISM-models.md`. |
| T-3400 · Web workbench: dark mode, layout compaction, feedback | 2026-07-14 | Dark palette + tri-state header toggle persisting to `localStorage`; three theme-bypassing hardcoded colours fixed in `f9df410`. **Accepted as-is (user):** `.primary-button`/`.action-button` white-on-accent = 4.43:1 in dark mode, marginally under the 4.5:1 AA bar — the accent is a brand colour. The backend counts gap became T-4600. |
| T-3900 · `DetermineTieBreaker` rescan can mislabel the deciding tiebreaker | 2026-07-13 | Diagnostic-only fix; the winner is now compared against its closest still-unassigned rival, not the whole family. Labels + reasoning: `jb/docs/PRISM-order-rename.md` → "Which tie-breaker the evidence names". |
| T-4540 · Analyzers adopt ConfigLoader; root AnalyzerConfig dissolves | 2026-07-12 | Two-phase shape: sections load independently and self-validate (`IValidatableConfig`), then compose into `AnalyzerParameters`. `jb/docs/PRISM-pipeline-core.md` → "Loading is two phases". |
| T-4530 · Transform adopts ConfigLoader; delete Configure() push-in | 2026-07-12 | Same two-phase shape → `TransformParameters`. Self-load survives **only** in the two fixed-signature webservice `Process(byte[], int, float)` entry points, which have no parameter to pass config through. `CropTransformSettings` moved into `transform_Config.json`'s `Crop` section. |
| T-4520 · Transform layout cleanup + delete dead BackgroundType | 2026-07-12 | Files relocated; `BackgroundType` deleted (background typing already flows as the `"background-type"` feature-snapshot string). |
| T-4510 · ConfigLoader core | 2026-07-12 | `ConfigLoader` (`Section<T>`/`Root<T>`/`RequireFile`), `IValidatableConfig`, `ModelAssetLocator` in `Prism.Config`, with required-member enforcement and an internal cache keyed on `LastWriteTimeUtc`. |
| T-4300 · Strip shadow defaults from Analyzer config classes | 2026-07-12 | All 9 classes `required` with zero initializers; `analyzer_Config.json` is the only source. |
| T-4200 · Transform engine config retrofit | 2026-07-12 | 11 empirical `Tx_*` tunables extracted to `transform_Config.json`, values byte-for-byte. |
| T-3200 · Close Services test coverage gaps | 2026-07-10 | `Prism.Core.Tests/Ingest/` covers multipart/ZIP/URL/stream ingestion; direct `LocalArtifactStore` tests added. |
| T-3100 · Bracket 4 perf | — | `RunWaterfall` skips Bracket 4 when no record has an influential CLIP tag; `StringMatcher` reuses Bracket 3's inverted token index instead of an un-indexed per-family scan. |
| T-3000 · Parallelize image import normalization | — | Both image loops normalize via `Parallel.ForEach`; already-conforming JPEGs are copied unchanged instead of decoded/re-encoded. |
| T-2810 · PipelineIntegrationTests hard-depend on an uncommitted dataset | — | Fixture path walks up to `test/datasets` keyed by the committed `CiMini` folder; CI filter exclusion removed. |
| T-2800 · API/in-process pipeline never initializes the GPU upscaler | — | `PipelineServiceFactory` calls `UpscaleService.Create` once; upscaler init made idempotent, thread-safe and non-throwing. Exposed the fixed-`[1,3,64,64]` model input, which became T-4905. |
| T-2700 · Wire fetcher strategies into API ingress | — | `FetchDispatcher` — ordered strategy list with `CanHandle`/`FetchAsync`, content-type first and URL extension as fallback. |
| T-2500 · GPU upscaler (Real-ESRGAN via DirectML) | — | Implemented; later reshaped by T-4110 into the single `Upscaler` class — `jb/docs/PRISM-transform-generate.md`. |
| T-2400 · Cross-bracket tie accumulator | — | `RunWaterfall` maintains per-image `crossBracketCandidates`; `KoUnmatched` emits `MATCHES_MULTIPLE_FAMILYIDS` (≥2) vs `MATCH_NOT_FOUND` (0). |
| T-2300 · User decisions: detail crop saliency, headcut, greedy crop | — | BoundingBox from `ImagePreProcessor` is the sole saliency anchor; headcut is a bool threaded from `has-human`; greedy crop aligns bbox centre to canvas centre with `Tx_util_BgStretch` fill. |
| T-2200 · Spec and implement Tx_util_HeadCutter | — | Algorithm B (full-image Haar face search, centroid Y < 50%, pick face furthest from top, `cutY = face.Y + 0.75×face.Height`). Algorithm A (anatomy-ratio guided) deferred. |
| T-2100 · Implement Tx_DetailCropper pixel flow | — | Full 6-branch decision tree over every bbox edge-intersection pattern; crop sizing from config. |
| T-2000 · Implement Tx_CenterAndStretch pixel flow | — | Crop to bbox → resize to margin-adjusted target preserving aspect → centre on canvas → stretch background (guarantees a non-negative placement offset). |
| T-1900 · Tx_LowContrastEnhancement | — | CLAHE via OpenCvSharp4 on the full image, dual-interface `Process(byte[], int, float)`. |
| T-1800 · ProductTypeId write to ImageRecord_LAMBDA | — | Written in `ImageOrderer.ProcessFamily`; resolved from Excel IEM dynamic columns, normalized kebab-case against `DetOrderRules.json`. |
| T-1700 · Tx_util_BgStretch | — | Tiered fill: ≤125% edge clamp, ≤142% content-aware extension, >142% `INPAINT_TELEA`, >250% solid white, with seam feathering after tiers 1-2. |
| T-1600 · SD-8: ImageRecord_OUTPUT Width/Height/Checksum | — | Not a bug — declared on `ImageRecord_Base` and inherited. No code changes. |
| T-1500 · Split StageShells.cs | — | Deleted; eight `ShellStage_Xyz.cs` files, one per stage. |
| T-1400 · Fetch_DropBox | — | Public shared links normalized `?dl=0` → `?dl=1` and delegated to `Fetch_HTTPS_DirectFile`. Private OAuth out of scope for V1. |
| T-1300 · Fetch_HTTPS_DirectFile | — | Streams direct HTTPS downloads to `%TEMP%/prism/{jobID}/`, validated against `HostRules.json`. |
| ONNX Singleton (M5 gate item) | 2026-06-29 | `InferenceSession` hoisted to an application-scoped singleton on `MatchingService`; `_clipLock` serializes every `Run()` (required for DML). Also in `AGENTFEEDBACK.md` Behavioral Memory — an *answered* decision, do not re-open. |
