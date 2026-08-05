# Phenotype assignment — first-pass production validation

> ## Third pass, 2026-08-05 — accuracy scored, and it now clears the M11 bar on this dataset
>
> Everything below is the first and second pass, kept for the reasoning. This section supersedes their
> verdict. Measured with the same `prism-evidence-report` harness on all 86 SPACINI29 images at the
> **shipped** config, after [[T-4955]], [[T-4990]] and [[T-5000]] landed.
>
> | | Assigned | Correct | Wrong | Unassigned |
> |---|---|---|---|---|
> | Second pass, shipped config (2026-07-30) | 5 | **0** | 5 | 81 |
> | Second pass, 0.30 bar, after rule changes | 46 | 33 | 13 | 40 |
> | **Third pass, shipped config (2026-08-05)** | **29** | **25** | **4** | 57 |
>
> **Coverage 33.7%, accuracy on assigned 86.2%, and misassignment over the whole set 4.7% — under
> M11's <5% bar.** Ground truth is `dataset notes.md`: every image is on-model, `_A` front, `_B` back,
> and no image is fully in frame, so every one is a *partial* on-model shot.
>
> **All 4 remaining errors are the same failure, and it is the only one left.** CLIP flips front↔back
> at 0.345, 0.356, 0.374 and 0.389 — just above the 0.33 bar `97326fe` set. `23211056_35_B`,
> `23211094_35_A`, `23211095_35_B`, `24211513_76_A`. The second pass had 13 errors: 9 orientation and
> 4 from the detector under-count. **The 4 detector-driven errors are gone** — [[T-4990]] took
> `intersection-count` from 65/86 to 84/86 with zero under-counts, so no image reads 0 and nothing
> reaches a full-product rule through a detection error. Orientation errors dropped 9 → 4 as a side
> effect of the same run being consistent ([[T-4955]]).
>
> **What now caps coverage is a taxonomy gap, not a threshold.** `body-visible` reads `full` on 29 of
> 86. A `full` reading sends the image to `front-/back-on-model-full-product`, which both require
> `intersection-count = 0` — and ground truth says **no image in this dataset has zero intersections**.
> The partial rules only accept `three-quarter`, `half` or `bust`. So a full-length model shot cut by a
> frame edge matches **no rule at all**, at any threshold. That is 34% of the dataset sitting in a hole
> between two rules. The remaining ~28 unassigned images are `bust` with `hero-orientation` still
> UNKNOWN, which *is* a threshold question and is now genuinely next.
>
> **The M11 caveat still stands and is not weakened by the number.** This dataset has a positive case
> for exactly two of the 18 phenotypes. 4.7% misassignment on the on-model branch is real and
> welcome; it says nothing about the other 16.


Measured 2026-07-30 for [[T-4970]], the light first pass. Not [[T-2600]]'s full acceptance bar:
no labelled set, no confusion matrix, no <5% misassignment target.

Instrument: the `prism-evidence-report` in-process harness (Import → Match, plus Generate →
Transform where stated). All numbers below come from that run; the raw dumps are on the Desktop
under `T-4970-phenotype-evidence/`.

> **Second pass, 2026-07-30 (same day).** The first pass concluded "the remedy is config-only:
> lower two thresholds". That conclusion was tested and **it is wrong**. Lowering the thresholds
> raises coverage from 7% to 62% and the extra assignments are mostly incorrect. The blocking
> problems are in the rule set and the feature semantics, not in the two numbers. The sections
> "Lowering the thresholds was tested" onward are the second pass; everything above them is the
> first pass, corrected in place where it was wrong.

## What ran

| Dataset | Images | OK | KO | Families | Notes |
|---|---|---|---|---|---|
| SPACINI29 | 86 | 86 | 0 | 45 | The measurement. One model, front/back, one white sweep. |
| MMERO26 (60-image subset) | 60 | 1 | 59 | 1 | Yielded no usable data — see "The second dataset failed" below. |
| CiMini | 14 | 14 | 0 | 8 | Cross-check only. |

## Headline: phenotype assignment is effectively not happening

On SPACINI29, **6 of 86 images (7%) received a phenotype. All 6 got the same one,
`model-detail-closeup`. The other 80 got nothing** — not a weak pick, not a provisional survivor:
`SelectedPhenotype` is null.

19 of the 20 phenotypes never fired once.

The split the ticket asked for, separating a satisfied rule from a surviving provisional pick:

| Outcome | Count |
|---|---|
| A phenotype rule was fully satisfied | 6 |
| No rule satisfied, provisional pick survived the pool | 0 |
| No phenotype at all | 80 |

That middle row being zero matters. `FinalizePhenotype` (`ImageFeatureAnalyzer.cs:167`) keeps a
phase-1 provisional pick when no rule is satisfied *and* the pick is still uncontradicted. It never
happened once, so the fallback path contributes nothing today.

These counts were produced by re-implementing `PhenotypeRuleSet.EvaluateCandidates` and
`IsContradicted` in Python against the dumped feature snapshots — the dump records only the merged
`SelectedPhenotype` and cannot separate the two routes on its own. **The reimplementation agrees
with the pipeline's own recorded value on all 86 images (0 disagreements)**, so the split is
measured, not estimated.

## Cause: two config thresholds sit above what the model can reach

Phenotype rules read only 14 of the 38 features. Four of those are the gate, and three are almost
always `UNKNOWN`:

| Feature | UNKNOWN on | Configured bar | Best CLIP score seen | Clears the bar |
|---|---|---|---|---|
| `hero-orientation` | 86/86 | 0.60 | **0.582** | **0 of 86** |
| `head-visible` | 86/86 | 0.65 | **0.589** | **0 of 86** |
| `multiple-products` | 86/86 | — (YOLO) | — | 0 of 86 |
| `body-visible` | 72/86 | 0.60 | 0.736 | 14 of 86 |

`hero-orientation` and `head-visible` are not marginal — **the configured bar is above the highest
score the model produced on the entire dataset**. `hero-orientation` maxed out at 0.582 against a
0.60 bar. Not one image in 86 came within 0.018 of clearing it.

The chain, stated plainly: the bar is set higher than CLIP ever scores → the feature stays UNKNOWN
→ UNKNOWN never satisfies a condition (`PhenotypeRuleSet.cs:125`) → every rule requiring orientation
fails → **13 of the 20 phenotypes are unreachable on any image**, whatever it depicts. (Counted from
`ImageRoles.json`: the 4 orientation-bearing on-model rules, all 6 packshots, and all 3 ghosts.)

The 6 that did fire are not a success story. `model-detail-closeup` is the **only** human-branch rule
that does not require `hero-orientation`. With orientation universally UNKNOWN it is the only one
that *can* fire, so those 6 are an artifact of which rule has the fewest gates — not evidence that
detail closeups are being recognised. Spot-checked by eye: `23211056_35_A.jpg` is a front-facing
bust-crop of a model in a camel turtleneck. The honest label is `front-on-model-partial`; it got
`model-detail-closeup` because the FRONT condition was unavailable.

`multiple-products` is UNKNOWN for a different reason — `Analyzer_MultipleProducts` writes it only
when YOLO returns more than one object, and never writes `false`. So `on-model-with-accessories`,
which hard-requires `multiple-products=true`, is also unreachable here.

Note `hero-is-human` works fine, but via **YOLO** (0.96 confidence, 85/86 TRUE, and it is correct —
these are on-model shots). CLIP's own `hero-is-human` prompts top out at 0.867 against the global
0.90 bar, so the CLIP path for that feature is dead surface that YOLO happens to cover.

## The threshold is mis-set, but lowering it is not free

**Correction (second pass): `hero-orientation` and `head-visible` are not the binding gate.
`body-visible` is.** Every human-branch rule that reads orientation also reads `body-visible`, and
`body-visible` is UNKNOWN on 72/86 at its 0.60 bar. Measured: lowering *only* `hero-orientation` —
to 0.45, 0.42, 0.40, 0.375 or 0.35 — leaves coverage at exactly **7%, unchanged**, because the
newly-known orientation still meets an UNKNOWN `body-visible` and the rule fails anyway. The
first-pass claim that two numbers were the blockage understated it by one feature and, more
importantly, mistook a coverage problem for a correctness fix. See "Lowering the thresholds was
tested" below.

**`head-visible` barely matters either way**, for a different reason: the rules use it permissively.
`front-on-model-full-product` accepts FULL *or* PARTIAL; `back-on-model-full-product` accepts NONE
*or* PARTIAL. CLIP picks FULL or PARTIAL on 86/86 and **NONE on 0/86** — even on the 41 back shots
where no face is in frame. So once the value is written at all, it satisfies the front branch on
100% of images and the back branch on the 58 that came out PARTIAL. It cannot discriminate front
from back, but no rule asks it to.

Using the dataset's `_A`/`_B`
filename convention as a weak ground truth (verified by eye: `_A` is the front shot, `_B` the back),
the CLIP orientation **argmax** is right 64/86 = **74.4%** overall. But confidence separates the
good calls from the bad ones cleanly:

| If the bar were… | Images accepted | Correct | Precision | Coverage |
|---|---|---|---|---|
| 0.60 (today) | 0 | – | – | 0% |
| 0.50 | 2 | 2 | 100% | 2% |
| 0.45 | 7 | 7 | 100% | 8% |
| **0.40** | **21** | **21** | **100%** | **24%** |
| 0.375 | 24 | 23 | 95.8% | 28% |
| 0.35 | 38 | 32 | 84.2% | 44% |
| 0.30 | 69 | 53 | 76.8% | 80% |
| 0.25 (all) | 86 | 64 | 74.4% | 100% |

Every one of the 22 wrong calls scored ≤ 0.389. There is a real precision cliff between 0.40 and
0.35. **But this table measures the orientation feature in isolation, not the phenotype that comes
out the far end** — and the second pass shows those two come apart. Read it with the next section.

Mechanically the change needs no code and no new config surface: the per-feature override map
already exists at `Prism_Config.json` → `Classification.Confidence_Thresholds`, and already carries
`hero-orientation`, `head-visible`, `body-visible`, `product-color`, `product-type-label`. Only the
values would move.

For reference, the two features that do work have bars set below their achievable range:
`product-type-label` (bar 0.45, median 0.836) and `product-color` (bar 0.45, median 0.511).

## Lowering the thresholds was tested. Coverage rises; correctness does not

Method. A replay of `PhenotypeRuleSet` (first-rule-wins, UNKNOWN satisfies nothing) was run over the
dumped snapshots with the three CLIP feature bars swept. The replay reproduces the shipped pipeline
**exactly** at the shipped config — 86/86 identical, including the same 6 `model-detail-closeup`.
Because that only exercises the path where all three features are UNKNOWN, the sweep was then
**confirmed against a real pipeline run**: `Confidence_Thresholds` for `hero-orientation`,
`head-visible` and `body-visible` were set to 0.30, the harness was re-run on all 86 images, and the
config was reverted. Predicted 53 assigned, actual **53 assigned, 85/86 identical**. The single
differing image is explained below and is not a replay error.

Coverage against a common bar on all three features:

| Bar | Images with a phenotype | Which phenotypes |
|---|---|---|
| 0.60 / 0.65 / 0.60 (today) | 6 (7%) | detail-closeup 6 |
| 0.50 | 22 (26%) | detail-closeup 22 |
| 0.45 | 33 (38%) | detail-closeup 33 |
| 0.40 | 45 (52%) | detail-closeup 42, front-partial 2, front-full 1 |
| 0.35 | 47 (55%) | detail-closeup 38, front-partial 7, front-full 2 |
| 0.30 | 53 (62%) | detail-closeup 30, front-partial 19, front-full 4 *(measured, not replayed)* |
| 0.25 (accept everything) | 55 (64%) | front-partial 28, detail-closeup 23, front-full 4 |

Coverage roughly nine-folds. Now score it.

**Ground truth is hand-verified by the user**, recorded in
`test/datasets/SPACINI29/RAW IMAGES/dataset notes.md` (UTF-16, marked do-not-edit). It states: every
image contains a human; `_A` is front-facing and `_B` back-facing; 16 images touch **one** frame edge
(the top) and the other 70 touch **two** (top and bottom). **No image in the dataset is fully inside
the frame.**

That last point corrected an earlier draft of this section, which had labelled three `_A`/`_B` pairs
(`20213024_46`, `23211041_03`, `23211108_03`) as full-product shots from 300px contact sheets. They
are not — each runs off the top edge. The honest labels are:

| Honest label | Count |
|---|---|
| `front-on-model-partial` | 45 |
| back-on-model-partial — **no such rule exists** | 41 |
| every full-product phenotype | **0** |

Scoring only the 45 images for which a correct rule exists at all:

| Bar | Assigned | Correct | Wrong | Left unassigned |
|---|---|---|---|---|
| 0.60 (today) | 5 | **0** | 5 | 40 |
| 0.50 | 13 | **0** | 13 | 32 |
| 0.45 | 18 | **0** | 18 | 27 |
| 0.40 | 27 | 2 | 25 | 18 |
| 0.35 | 27 | 5 | 22 | 18 |
| 0.30 | 31 | 14 | 17 | 14 |
| 0.25 (accept everything) | 32 | **23** | 9 | 13 |

Read the first row. At today's config the pipeline assigns 5 phenotypes among these 45 images and
**every one of them is wrong**. Down to 0.45 it is still 18 assigned, 18 wrong. The best result on
the whole sweep is at 0.25 — no threshold at all — and it is 23 correct out of 45, with 9 wrong.
That is **51% correct where M11 asks for under 5% misassignment.**

## The subject detector under-counts frame intersections on 1 image in 4

Scoring against the hand-verified counts gives the first real accuracy figure for
`intersection-count`, which four phenotype rules gate on:

| Truth | Measured 0 | Measured 1 | Measured 2 |
|---|---|---|---|
| 1 intersection (16 images) | **6** | 10 | 0 |
| 2 intersections (70 images) | 0 | **15** | 55 |

**65/86 = 76% correct, and every error under-counts.** Not one image is over-counted. Identical in
the classify-only run and the with-Transform run, so this is the detector, not [[T-4955]].

The 6 that measure zero are the consequential ones: `intersection-count = 0` is the gate on
`front-on-model-full-product`, `back-on-model-full-product`, all six packshots and all three ghosts.
Truth says **no SPACINI29 image should clear that gate**. So the 2–4 images that reached a
full-product phenotype at the lower bars did so through a detector error, not a correct assignment —
and they are counted as wrong in the table above.

Related, from the same notes: `hero-is-human` is right on 85/86. The miss is `23211095_35_A.jpg`,
a camel poncho draped over the model where YOLO returned **zero** people. Consequence: every
human-branch rule requires `hero-is-human=TRUE`, and every packshot and ghost rule requires `FALSE`,
so one missed person does not merely weaken the answer — it moves the image to the opposite half of
the taxonomy.

So: the threshold is genuinely mis-set, and moving it is not the fix. Lowering it buys coverage made
mostly of wrong answers. There is no value of these three numbers at which phenotype assignment
becomes good enough to route Transform on.

## Three reasons correctness does not follow coverage

**1. `model-detail-closeup` swallows ordinary cropped on-model shots.** Its rule is
`hero-is-human=TRUE` + `occlusion-level ∈ {closeup, partially-occluded}` +
`body-visible ∈ {bust, none}`. On SPACINI29 `occlusion-level` is `partially-occluded` on **55/86** —
because the model is cut by a frame edge, which is what a cropped fashion shot always looks like.
Nothing about these images is a detail crop. **Zero of the 86 images is honestly a
`model-detail-closeup`**, and at a 0.45 bar the rule claims 33 of them. This is a feature-semantics
defect, not a threshold one: `occlusion-level=partially-occluded` is being read as evidence of a
detail shot when it only means "touches an edge".

Rule order makes it worse in a way that hides the problem. `front-on-model-partial` is rule 1 and
`model-detail-closeup` is rule 5, so front-partial wins whenever it is satisfied. It is satisfied
only when `hero-orientation` is known. **So the higher the orientation bar, the more images fall
through to `model-detail-closeup`** — the current 7%-coverage state is the worst case of this, and
it is exactly why all 6 assignments today are wrong.

**2. There is no `back-on-model-partial` phenotype.** The human branch has front-full, front-partial,
back-full, side, accessories and detail-closeup — but no back equivalent of front-partial. A back
view cut by a frame edge is one of the most common shots in a fashion catalogue: it is **41 of these
86 images, 48%**. Those images cannot be labelled correctly at any threshold. Measured at bar 0.30,
they come out as: nothing 19, `model-detail-closeup` 16, `front-on-model-partial` 4,
`front-on-model-full-product` 2. **Decided 2026-07-30: the phenotype is being added** — see
"Decisions taken" at the end.

**3. Orientation cannot be a high-precision gate.** It is a 5-way softmax, so a confident winner
sits near 0.58 at best, and the argmax is 74.4% right.

## Only 1 of the 20 phenotypes has a true case in this dataset

Counted against the hand-verified labels: `front-on-model-partial` (45). The other 41 images need
`back-on-model-partial`, which did not exist. **Nineteen of the twenty shipped phenotypes have no
positive case at all** — not measured and found wanting, absent. Every full-product phenotype, every
packshot, every ghost, every lifestyle, every non-human phenotype.

This is the single strongest argument for the purpose-built dataset. On SPACINI29 the measurement can
only ever answer one question out of twenty.

This bounds what SPACINI29 can ever prove. It can show the pipeline gets on-model shots wrong, which
it does. It cannot show anything about the other 17. That is the purpose-built-dataset dependency in
the root `jbtodo.md`, and it is now the critical path.

## The only high-confidence orientation path is the filename, and it is wrong 15 times out of 16

`Analyzer_FilenameEvidence` writes `hero-orientation` from whole-token filename matches at a fixed
**0.75** confidence (`analyzer_Config.json` → `Filename.OrientationConfidence`). That is above every
CLIP orientation score ever recorded on this dataset (max 0.582) and above the 0.60 bar, so
**whenever a filename keyword is present it wins outright** — it is the one path that can put a
confident orientation on an image today.

Two measurements of that path across every dataset in the repo (14,427 images):

**It almost never fires.** 16 filenames in 14,427 (0.1%) contain an orientation token. SPACINI29,
CiMini, FILA94, INPUTMA23/24/27, SPACINI32 and TinyTest contain **zero**. So the 7% coverage figure
is measured on a dataset where the strongest orientation signal is structurally absent — a real
customer batch that names files `..._front.jpg` would behave very differently, and nothing in this
repo tests that.

**When it does fire, it is usually a garment word, not a camera view.** All 16 hits, by hand:

| Filename | Token | What the token actually means |
|---|---|---|
| `freya_top_cinzia_skirt_F` (×4) | `top` | the garment is a *top*; `_F` says the view is front |
| `Malibu_ivory_TOP` (×2) | `top` | bikini *top* — the piece, not the view |
| `Malibu_ivory_BOTTOM` (×2), `Alba_ivory_B - BOTTOM` (×2) | `bottom` | bikini *bottoms* — the piece |
| `F-MODE-GO-…-BACK-STRAP-SANDALS-…` (×5) | `back` | *back-strap*, part of the product name |
| `25W_538_back` | `back` | genuinely a back view |

**15 of 16 are false positives**, and each writes a confident wrong orientation that CLIP cannot
outvote. `freya_top_cinzia_skirt_F` is the sharpest case: the file says front, the analyzer says TOP.

Consequence: whatever threshold is eventually chosen for CLIP orientation is moot on any batch whose
filenames carry these words, because the filename path outranks it. Fixing the token list — at
minimum requiring a view-ish context rather than a bare `top`/`bottom`/`back` token — is a
prerequisite for orientation being trustworthy at all. Separately, note there are **two independent
keyword lists that disagree**: `OrientationTokens` in `Analyzer_FilenameEvidence.cs` (25 entries,
hard-coded) and `DetOrderKeywordStems.json` (12 groups, config, and it carries `sole`, `outsole`,
`close`, `zoom` which the analyzer's list does not).

## Confirmed in passing: [[T-4955]] is real, and it hits 42% of images

The one image where the replay and the real 0.30 run disagreed (`24211511_96_A.jpg`) led to this.
Checking `intersects-top/bottom/left/right` against `intersection-count` on every image:

| Run | Images where the four flags contradict the count |
|---|---|
| With the Transform stage (subject-box promotion runs) | **36 / 86 (42%)** |
| Without the Transform stage | 0 / 86 |

Cause: `ImageTransformer.PreferSubjectGeometry` (`ImageTransformer.cs:113`) rewrites the four
`intersects-*` booleans from the detector but leaves `intersection-count`, `fully-in-frame` and
`occlusion-level` holding the earlier heuristic values. Effect: after promotion the snapshot says
one edge is touched and the count is zero, on 42% of images. Consequence for phenotypes
specifically: `front-on-model-partial` gates on `intersects-top|bottom` while
`front-on-model-full-product` gates on `intersection-count=0`, so a single image can satisfy both
mutually-exclusive rules, and first-rule-wins hands it to the full-product one. T-4955 records this
as "harmless today"; it is harmless only because phenotype routing is off. The 42% figure is new.

## What this means for ordering today

Every single OK image on SPACINI29 — including all 6 with a phenotype — came out of the Ordered
stage with `IsOverflow: true`. So det-slot assignment is being done entirely by the overflow policy
in `DetOrderRules.json` (filename hint → on-model rank → natural filename order), and phenotypes
contribute nothing to it.

Two things make that total rather than partial:

1. Almost no image has a phenotype.
2. `model-detail-closeup` — the one phenotype that does fire — **appears in no det slot for any of
   the 5 product types**. Nor does `lifestyle-context`. So even a correctly-assigned
   `model-detail-closeup` cannot win a slot; it overflows regardless.

**Output filenames are not affected.** Overflow slots start at `lastConfiguredSlot + 1` = 8, so the
raw records carry `_det8`, `_det9`, `_det10` — but `Exporter.Run` calls `ImageOrderer.CompactDetOrder`
before anything is written, which closes the gaps. Shipped filenames are `_det0`-based as documented.
The internal 8+ numbering is invisible and is not a defect. (This was checked rather than assumed
because the harness stops before Export and therefore reports the raw pre-compaction values —
the exact symptom archived ticket **T-2830** was raised and closed for.)

What *is* affected is which image lands in which slot: the relative order that ships is filename
order with an on-model bias, not a semantic ordering.

## Two structural rule-reachability findings

Independent of thresholds, found by reading `ImageRoles.json` in evaluation order:

- **`ghost-side` is unreachable on a solid background.** Its conditions are identical to
  `side-packshot` except that it also accepts `clipping-path=true`. `side-packshot` is evaluated
  first (index 8 vs 14), so on SOLIDCOLOR it always wins. `ghost-side` can only fire on a
  transparent-background image. Same reasoning applies to `ghost-front` — already documented as
  accepted-by-design in `imagePhenotypes.md`.
- ~~**`ghost-back` silently catches intersecting back packshots.**~~ **Withdrawn — this finding was
  wrong.** It read `back-packshot`'s explicit `intersection-count=0` and concluded `ghost-back` had no
  equivalent. It does: `ghost-back` carries `occlusion-level=full-product`, and `occlusion-level` is
  derived *solely* from `intersection-count` (`ImageFeatureAnalyzer.cs:249-255`, 0 → `full-product`).
  The two conditions are the same condition under two names, so both rules require zero intersections
  and an intersecting back packshot satisfies neither. `back-packshot` simply states it twice.
  `ghost-back` behaves like `ghost-side` above: on SOLIDCOLOR the packshot rule wins on index order,
  so it is reachable only on a transparent background. The error was caught while scoping the
  `occlusion-level` removal (user decision, 2026-07-30) and is what made the alias visible.

## The second dataset failed, and that is itself a finding

MMERO26 is the only dataset in the repo with genuinely non-solid backgrounds. A 60-image subset
(30 lifestyle, 30 packshot, both Excel files) produced **1 usable image**: 59 of 60 KO'd on
`MATCHES_MULTIPLE_FAMILYIDS`. The two Excel files yield 332 families, and the filenames — `1.jpg`,
`188.jpg`, `25W - 506.jpg` — match many of them ambiguously.

That is partly an artifact of subsetting (60 images against a 332-family sheet), so it says nothing
definitive about the full 2024-image set. But the consequence is firm: **a KO'd image never reaches
phenotype assignment at all** (`MatchingService.cs:315` skips `Refine` when `IsKo`). So
`lifestyle-hero`, `lifestyle-context`, and every non-solid-background case remain completely
unmeasured.

Dataset requirements to fix this properly are specified image-by-image in the root `jbtodo.md`
("Phenotype validation needs a purpose-built dataset").

## Incidental: the CiMini E2E golden is currently red

Not part of this ticket's question, found while cross-checking det numbering, and verified by
running it:

`pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` **fails** with 4 issues, all on one
image. `CARDIGAN_MAGENTA76_DETAIL.jpg` is expected `Ok` / `det2` by the committed golden but now
KOs with:

```
PREPROCESS_TOO_SMALL — Salient object 471px < minimum 570px.
```

Reproduced in-process. This is [[T-4920]]'s deliberate change — "the too-small KO is retained and
now measures the *promoted* box" — meeting an image whose promoted subject box is 471px. T-4920's
review anticipated that re-runs would not match older evidence; the golden was simply never
recaptured.

**The xUnit suite does not catch this.** `PipelineIntegrationTests.cs:127` asserts only the regex
`_det\d+\.\w+$`, and nothing in the test projects reads `expected-manifest.json` — only the pwsh CI
script does. So the E2E gate is red while `dotnet test` is green.

Whether to recapture the golden or treat the new KO as wrong is a product decision, not taken here.

## Verdict

**No. Phenotype assignment is not good enough to base Transform routing on, and the flip should not
happen yet.** At 7% coverage on the only dataset that produced data, with the single firing phenotype
mapped to no det slot, turning off `BypassPhenotypes` would replace a working geometric default with
a near-empty signal.

**The second pass hardens that from "too little signal" to "wrong signal".** At today's config,
every one of the phenotypes the pipeline assigns to a scorable SPACINI29 image is incorrect — 5
assigned, 0 right. Coverage was never the real problem.

The engine itself is sound — the replay matched the pipeline exactly at the shipped config and
missed by one image out of 86 at a lowered one. What is not sound is what the rules are asking for.

**The first pass called the blockage "two numbers in `Prism_Config.json`". That is now measured and
rejected.** The thresholds were lowered on a real run: coverage went 7% → 62% and correctness on the
scorable images peaked at 52%, with the *first* five assignments at today's config all wrong. The
blockage is three things the thresholds do not touch — `model-detail-closeup` over-firing on any
edge-cropped model shot, a missing `back-on-model-partial` rule covering 44% of a real production
set, and a 5-way orientation softmax that cannot be a precision gate.

Order of work, revised on the second pass:

1. **Fix the rule set, not the thresholds.** Two decisions, both product calls, both blocking:
   whether `model-detail-closeup` should stop keying on `occlusion-level=partially-occluded`, and
   whether `back-on-model-partial` should be added. Neither needs new data — SPACINI29 already shows
   the failure.
2. **Fix [[T-4955]]** — the snapshot the rules read is internally inconsistent on 42% of images, so
   any rule tuning done before that fix is tuning against noise. This moved from "cleanup before the
   flip" to "prerequisite for tuning".
3. **Then** pick threshold values, and re-measure. Doing this first is what the second pass tried;
   it produces coverage without correctness.
4. Build the purpose-built dataset (root `jbtodo.md`). 17 of the 20 phenotypes have no positive case
   anywhere in this repo, so steps 1–3 can only ever be validated on the on-model branch until this
   exists. [[T-4945]] and [[T-4948]] need the same asset.
5. Only then revisit the `BypassPhenotypes` flip, with the two dormant defects on [[T-4910]] /
   [[T-4920]] cleared.

## Decisions taken, 2026-07-30 — and what they measured

Both blocking questions were answered by the user and are now implemented. **No thresholds were
changed.** These are rule and taxonomy changes only.

**1. `occlusion-level` is removed from the taxonomy.** User's words: "it's intersection count in
disguise." Every one of the 13 rules that used it now states `intersection-count` directly, using the
exact mapping its producer applied (0 → full-product, 1 → mostly-visible, 2 → partially-occluded,
3+ → closeup), so the substitution is behaviour-identical. `DeriveOcclusionLevel`, the
`OcclusionLevelConfidence` config key and the feature declaration are gone. To be reintroduced later
as a real measurement rather than an alias.

Three things fell out of the removal that were invisible while the alias existed. The `ghost-back`
finding was withdrawn (above). `front-packshot`, `back-packshot` and `ghost-front` each stated the
same condition twice. And `closeup-image`'s `intersection-count ≥ 1` was dead — the `occlusion-level
= closeup` beside it already implied ≥ 3.

**2. `model-detail-closeup` is narrowed to `intersection-count ≥ 3`.** A genuine detail crop fills
the frame; an ordinary catalogue crop touches 2 edges. Accepting both in one `anyOf` discarded the
only discriminator there was.

**3. `back-on-model-partial` is added**, mirroring `front-on-model-partial`. Det slots per the user:
topwear det1 ahead of `back-on-model-full-product`; bottomwear a new det5; footwear a new det7 just
before lifestyle; bags-accessories and default share the existing back slot behind `back-packshot`.
Full definition in `imagePhenotypes.md`.

### Measured effect, on real images

At the **shipped** config, re-run on SPACINI29: the 6 `model-detail-closeup` assignments — all of
which were wrong — are gone. 86/86 now carry no phenotype, which is the honest answer at a bar CLIP
never clears.

At a **0.30** bar (measurement only, config reverted afterwards), scored against the hand-verified
labels across all 86 images:

| | Correct | Wrong | Unassigned |
|---|---|---|---|
| Before the rule changes | 15 | 38 | 33 |
| After the rule changes | **33** | **13** | 40 |

Correct more than doubles and wrong drops by two thirds, from rule changes alone. `model-detail-closeup`
went from 30 matches to 0; `back-on-model-partial` picked up 23, of which 18 are on genuine back shots.

**All 13 remaining errors are upstream, not rule errors**: 9 are CLIP calling a front shot BACK or a
back shot FRONT, and 4 are the detector's zero-intersection under-count feeding
`front-on-model-full-product`. Nothing left in this failure set is fixable by editing `ImageRoles.json`.

Det-slot wiring verified in the same run: topwear `back-on-model-partial` → det1 (19 images),
topwear `front-on-model-partial` → det5 (13), bottomwear `front-on-model-full-product` → det0.
Overflow behaves as before when a slot is already taken within a family.

Suites after the change: Matching 229/229, Core 154/154, Transform 83/83, Generate 10/10,
Upscale 17/17.

## Decisions this report does not take

Recorded so they are visibly open rather than quietly assumed:

- The threshold values for `hero-orientation`, `head-visible` and `body-visible`. Now genuinely next
  — the rule set no longer distorts what a threshold change would show.
- Whether `model-detail-closeup` and `lifestyle-context` should appear in `DetOrderRules.json`.
- Whether `ghost-back` catching intersecting back packshots should be corrected.
- Whether `Analyzer_MultipleProducts` should write `false` instead of leaving UNKNOWN.
- Whether CLIP's dead `hero-is-human` path (bar 0.90, max 0.867) should be re-barred or removed,
  given YOLO already covers the feature.
- Whether to recapture the CiMini golden or treat the new `PREPROCESS_TOO_SMALL` KO as a defect
  ([[T-4980]]).
