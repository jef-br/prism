# PRISM Agent Tickets — Archive

Done tickets, moved here by /ticket-finish to keep `jb/ticketboard/AGENT-TICKETS.md` (read every session
start) lean. Newest at the top. When a ticket closes, its `jb/ticketboard/T-XXXX.md` body is appended here
and that file is deleted.

### T-5060 · Det compaction reorders a family when only some of its images win a slot
**Status:** Superseded (2026-08-07) | **Profile:** P4-critical-architecture
**Found by:** [[T-4980]] item 2, 2026-08-05 — surfaced the moment `dotnet test` started reading the
CiMini goldens, and confirmed by dumping `WinningPhenotype` per row.

**Closed Superseded 2026-08-07 — user verdict: a bad ticket, not an Approve.** The fix (an axis-ordered
compaction pass) treated a symptom and named the wrong culprit: compaction only renumbers what it's
handed, and the real error was one step earlier in the Order stage, which gave a zero-phenotype image
an anchor ahead of a real slot winner. The actual defect — `CARDIGAN_MAGENTA76_A.jpg` and
`24211507_CARDIGAN_76_MAGENTA_B.jpg` producing no phenotype at all despite carrying a usable `_A`/`_B`
sequence-marker signal — was never touched; the ticket just reordered the output so the failure was
less visible. Reverted and replaced 2026-08-07 with: slot winners lead in slot order, unclassified
images go to the end in filename order, no cap past det9. `DetOrderAxis`, `AssignmentRecord.AxisPosition`,
`AnchorOverflow`, `ResolveHintSlot`, `OnModelRank`, and the `overflowPolicy` config block are deleted.
`CiMini_Manifest_MatchesCommittedGolden` went from 20 mismatched fields to 84 as a result — the golden
has **not** been re-blessed, and closing that gap is [[T-4980]]'s open item, not this ticket's. Follow-on
work (missing phenotypes from filename/folder tokens) is [[T-5120]].

**The catalogue order comes out backwards.** On CiMini family `90861052` the committed golden is
front, back, detail — and the pipeline now produces detail, back, front:

| Image | Winning phenotype | Slot won | After compaction | Golden |
|---|---|---|---|---|
| `CARDIGAN_MAGENTA76_DETAIL.jpg` | `model-detail-closeup` | det3 (configured) | **det0** | det2 |
| `24211507_CARDIGAN_76_MAGENTA_B.jpg` | *none* | overflow (8+) | det1 | det0 |
| `CARDIGAN_MAGENTA76_A.jpg` | *none* | overflow (8+) | det2 | det1 |

**Cause.** Overflow slots start at `lastConfiguredSlot + 1` = 8, and `ImageOrderer.CompactDetOrder`
closes the gaps before export. That is correct and deliberate when a family's images all overflow, or
all win slots. It is wrong in the mixed case: an image holding a *late* configured slot (det3, the
detail slot) still sorts ahead of every overflow image (8, 9), so compaction renumbers it to det0.
**Effect:** the relative order is decided by *which* images happened to get a phenotype, not by what
the configured slots mean. **Consequence:** the detail crop becomes the family's lead image and the
front shot is buried last — the single most visible output the pipeline produces.

**Why it is live now and was not before.** [[T-4970]] measured 7% phenotype coverage, so essentially
every image overflowed and filename order held — which is why the goldens looked right. Three changes
since raised coverage: [[T-5010]] removed `BypassPhenotypes`, [[T-4955]] made the edge-feature
snapshot self-consistent, and [[T-4990]] recalibrated the edge detector (65/86 → 84/86). The defect
did not appear; it became reachable.

**Second family, same run, shows the other half of the problem.** On `94613033`, `Pareo Exotica.jpg`
won `back-packshot` and `Pareo_exotica_F1.jpg` won `back-on-model-partial` — two *back* phenotypes on
what the filenames call front shots (`_F1`, `_F2`). That is [[T-4970]]'s known CLIP orientation error
class, not this defect, but it is what decided the slots here. Fixing compaction will not make this
family right; it will make it wrong for a legible reason instead of an illegible one.

**Do not re-bless the CiMini golden to make this pass.** The golden's order is the correct catalogue
order. `CiMiniGoldenTests.CiMini_Manifest_MatchesCommittedGolden` is red on exactly these 12 fields
and should stay red until this is fixed — that test is the reason the defect is visible at all.

**Acceptance:** a family mixing slot-winners and overflow images comes out in configured-slot order
with overflow images after them, not before; CiMini family `90861052` reads front, back, detail; the
golden test goes green without editing `expected-manifest.json`.

---

## Fixed 2026-08-05 — awaiting review verdict

**The anchor already existed; compaction just wasn't reading it.** `DetOrderRules.json`'s
`overflowPolicy.unhintedAnchor` is `2.5`, and its own comment states the intent: an unhinted overflow
image sits *between* the main-view slots (det0-2) and the detail/label/material slots (det3+), "so a
detail-hinted file can never jump ahead of the family's main shots". That anchor was only ever used to
sort overflow images **against each other**. They were then all stamped `lastConfiguredSlot + 1, +2, …`
— i.e. behind every configured slot — and `CompactDetOrder` sorted on that stamp. So the config's
stated rule was honoured among overflow images and silently inverted against slot winners.

**Change:** every image now carries a position on the configured-slot axis, and compaction orders on
that instead of on `DetOrder`.
- `ImageRecord_Base.DetOrderAxis` (new `double`) — a slot winner's axis is its configured slot index;
  an overflow image's axis is its filename-hint slot, or `unhintedAnchor` when unhinted.
- `AssignmentRecord.AxisPosition` carries it out of `ProcessFamily`; `AnchorOverflow` replaces the
  inline hint-slot tuple so the anchor is computed once and used for both the overflow sort and the axis.
- `CompactDetOrder` orders by `(DetOrderAxis, DetOrder)`. `DetOrder` as the secondary key preserves the
  Order stage's own overflow sequence within one anchor, keeps a phenotype winner ahead of an overflow
  image that hints at the same slot, and makes a second compaction pass a no-op (it runs from both
  `PrismService` and `Exporter`).
- `DetOrder` itself is unchanged, so gaps-allowed mode (`DET-ORDER-GAPS-ALLOWED: true`) exports exactly
  what it did before. An overflow image has no configured slot to claim, so `lastConfiguredSlot + 1…`
  remains the only sensible answer there.

**Result on CiMini family `90861052`:** axis positions are B `2.5`, A `2.5`, DETAIL `3.0` → det0 B,
det1 A, det2 DETAIL. Matches the committed golden. `expected-manifest.json` was **not** touched.

**The golden test is still red — on 4 fields, not 12, and for a different reason.** Family `94613033`
remains wrong and ordering cannot fix it, exactly as predicted above. Evidence from the in-process
harness:

| Image | `hero-orientation` | `hero-is-human` | phenotype | slot |
|---|---|---|---|---|
| `Pareo Exotica.jpg` | BACK 0.346 | FALSE 0.60 | `back-packshot` | bottomwear det1 |
| `Pareo_exotica_F1.jpg` | BACK 0.400 | TRUE 0.97 | `back-on-model-partial` | bottomwear det5 |
| `Pareo_exotica_F2.jpg` | BACK 0.476 | TRUE 0.96 | `back-on-model-partial` | overflow (det5 taken) |

All three are read `BACK` just over the 0.33 bar, on files the filenames call front (`_F1`, `_F2`).
Tracked as [[T-5080]]. Note that even with orientation corrected this family would still not match the
golden: `bottomwear` det0 lists `front-packshot` ahead of the on-model shots, so the packshot would
lead. That is a `DetOrderRules` product question, not a code defect — flagged, not changed.

**Verification:** full suite `dotnet test jb/src/PRISM.sln -m:1` → **532 tests, 531 pass, 1 fail**
(`CiMiniGoldenTests.CiMini_Manifest_MatchesCommittedGolden`, down from 12 mismatched fields to 4).
`ImageOrdererTests` 23/23 green — including the three `CompactDetOrder` tests, which construct records
with no `OrderEvidence` and axis 0 and therefore still exercise the pure renumber-by-`DetOrder` path.
The test's KNOWN-RED message was rewritten to name the remaining cause instead of this one.

**Files:** `jb/src/core/Models/ImageRecord_Base.cs`,
`jb/src/core/Services/Matching/ImageOrderer.cs`,
`jb/src/core/Services/Matching/Order/AssignmentRecord.cs`,
`jb/src/tests/Prism.Core.Tests/CiMiniGoldenTests.cs` (message only),
`test/datasets/CiMini/expected-manifest.json` (read-only, untouched).

## Reverted 2026-08-07 — this ticket was misdiagnosed and should not have been implemented

**User verdict (2026-08-07): a bad ticket. Close as superseded, not as Approved.**

**It treated a symptom and named the wrong culprit.** The ticket blamed det compaction. Compaction was
never the problem — it only renumbers, and it renumbered exactly what it was handed. The problem was
one step earlier, in the Order stage: the fix gave an image with *no phenotype at all* an anchor of
2.5, which placed it **ahead of a real det3 winner in the slot order**. That is the actual error.
An image with zero evidence was promoted past an image that had earned its slot.

**And the real defect was never touched.** On CiMini family `90861052`, `CARDIGAN_MAGENTA76_A.jpg` and
`24211507_CARDIGAN_76_MAGENTA_B.jpg` produce **no phenotype**. That is the thing to investigate and
fix. Both filenames carry a usable signal — the trailing `_A` / `_B` is a sequence marker — and PRISM
reads neither. Instead of asking why two ordinary product shots classify to nothing, this ticket
reordered the output so the failure was less visible. The golden then looked right for the wrong
reason.

**What replaced it (2026-08-07):** slot winners come first in slot order; images with no qualifying
phenotype go to the end of the family in filename order. `DetOrderAxis`,
`AssignmentRecord.AxisPosition`, `AnchorOverflow`, `ResolveHintSlot`, `OnModelRank` and the whole
`overflowPolicy` config block are deleted.

**Consequence, stated plainly:** family `90861052` now reads DETAIL, B, A — the detail crop leads.
That is not the desired output. It is the *honest* output: it is what a family looks like when two of
its three images carry no evidence, and it stays wrong until the missing phenotypes are fixed in
[[T-5120]]. The old behaviour hid the same failure behind a lucky ordering.

**Golden impact measured 2026-08-07:** `CiMini_Manifest_MatchesCommittedGolden` goes from 20 mismatched
fields to 84 (baseline measured by shelving only the ordering files and re-running). The golden has
**not** been re-blessed — that decision is still open and belongs with [[T-4980]].

**Update 2026-08-06:** CiMini's content was replaced wholesale (CiGolden + JBComplete merged in,
`expected-manifest.json` recaptured fresh — see [[T-4980]]'s update note). "Read-only, untouched" above
describes this ticket's own change, not the file's whole history: the CARDIGAN family `90861052` rows
this ticket fixed were verified to still match byte-for-byte in the new capture, and the Pareo family
`94613033` rows below were hand-restored to these exact pre-merge values rather than left at whatever
the still-buggy live pipeline produced, so this ticket's "must stay red" intent survives the merge.

### T-4960 · Alpha-derived box should retire SubjectGeometry's colour-distance fallback
**Status:** Obsolete (2026-08-05) | **Profile:** P1-feature-worker
**Found by:** [[T-4800]] completion pass, 2026-07-28

**Closed Obsolete 2026-08-05, no code landed and none was supposed to.** Closure reason: [[T-5030]]
deleted `AlphaSubjectCapture` and the entire alpha path, so the alpha-derived box this ticket was
meant to wire up does not exist anywhere in the system and `Producer = "alpha"` can never occur. The
ticket's premise — "exact geometry sits unused on the same record" — is false at HEAD; there is no
second producer to prefer. The linked todo in `Analyzer_SubjectGeometry.md` was retired to that
file's new "Retired" section in the same pass, per the todo lifecycle. The reviewer gate is not
applicable: an obsoleted ticket has no diff to review.

> **Obsoleted 2026-08-04 by [[T-5030]] — do not start this work.** Its entire premise is gone. T-5030
> deleted `AlphaSubjectCapture` and the whole alpha path: Import composites every input onto white and
> emits JPG before any analyzer runs, so **no alpha-derived box exists anywhere in the system** and
> `Producer = "alpha"` can never occur. There is nothing left for `Analyzer_SubjectGeometry` to prefer,
> and its colour-distance fallback is now the only producer for these images rather than the worse of
> two. Confirmed by the T-5030 reviewer pass.
>
> The underlying todo in `Analyzer_SubjectGeometry.md` ("fallback box on transparent-background images
> should use alpha instead of color distance") is likewise unactionable — there are no
> transparent-background images downstream of Import any more. Retire the todo per the todo lifecycle
> rather than implementing it.
>
> Original ticket text kept below for history.

`Analyzer_SubjectGeometry.md` carries an open todo: *"Fallback box on transparent-background images should
use alpha instead of color distance."* T-4830's ingress alpha path now captures exactly that — an exact
box and mask from the real transparency channel, before normalization flattens it onto white — and puts it
on the record as `SubjectDetectionResult` with `Producer = "alpha"`. **Cause:** the two pieces were built
for different tickets and never connected. **Effect:** the analyzer still falls back to colour distance on
transparent-background images while exact geometry sits on the same record. **Consequence:** measurably
worse geometry features on precisely the images where the best answer is free. Wire the analyzer to prefer
the alpha subject, then close the todo per the todo lifecycle.

**Files:** `jb/src/core/Services/Matching/Analyzers/Analyzer_SubjectGeometry.cs` + `.md`.

---


### T-5040 · Prune the phenotype set to the ones PRISM actually needs
**Status:** Done (2026-08-05) | **Profile:** P4-critical-architecture
**Landed:** `69b5eba`
**Review:** Approve (2026-08-04) — reviewer verified the 18-phenotype count by parsing both config
files, confirmed ghost/packshot slot-filler parity against `git show HEAD:...DetOrderRules.json`,
read `BuildCandidates`/`ResolveHintSlot` to confirm the `default.det6` "not dead" claim, and checked
that moving `illustration-technical-drawing`/`interior-shot` ahead of the packshots introduces no new
subsumption (the binary flags they gate on are disjoint from the packshot conditions). It noted the
reorder shifts the failure mode from "silent swallow" to "a false-positive binary detector steals a
packshot label" — accepted, and symmetric with the existing `interior-shot`/`closeup-image` tradeoff.

The 21 phenotypes in `ImageRoles.json` were authored by an agent, not derived from what PRISM has to
decide. The set is now **18**, derived from the constraints below rather than trimmed by taste.

**Correction to this ticket's own headline example.** It opened with "at least one of them —
`on-model-with-accessories` — has no identified consumer." That is wrong: it is consumed at footwear
`det8` and bags-accessories `det6`. The two phenotypes that genuinely had no consumer were
`model-detail-closeup` and `lifestyle-context` — independently corroborated by
`phenotype-assignment-validation.md`, which names exactly those two. The underlying point stood; it
named the wrong phenotype.

**Hard constraint, accepted by the user 04/08/26:** a phenotype never encodes the product type. A polo
and a t-shirt are distinct product types — that distinction belongs to the matcher — but both resolve
to the same phenotype. Phenotype describes view and composition only. Product type re-enters one step
later, in `DetOrderRules.json`, which maps phenotype → det slot *per product type*. Any candidate
phenotype that only one product type could satisfy is malformed and must be rejected by the model.

*Checked against the final 18: none names or implies a product type.* The `ghost-*` merge below
actively improves compliance — "ghost mannequin" is a garment-only concept, whereas "packshot"
applies to any product. `interior-shot` was checked specifically and passes: `interior-detected`
measures an enclosed cavity (a bag compartment, a shoe, a box), not a product category, even though
bags-accessories is currently its only det-slot consumer.

## The constraint audit, as measured

**1. Every phenotype must be reachable.** 18 of 21 were. `ghost-front` / `ghost-back` / `ghost-side`
were **provably dead**: once [[T-5030]] removed `clipping-path` — the only condition they carried that
their packshot counterparts did not — their `required` blocks became character-for-character identical
to `front-` / `back-` / `side-packshot`, which sit at indices 7/8/9 against 13/14/15. First-match-wins
meant they could never be assigned to any image.

**2. Every phenotype must be distinguishable.** Three ordering defects, only one of which this ticket
listed:
- `ghost-*` vs `*-packshot` — identical, above.
- `interior-shot` (index 18) sat behind `closeup-image` (index 16, `hero-is-human=FALSE` +
  `intersection-count >= 3`). Shooting down into a bag compartment fills the frame, so every interior
  shot satisfied both and lost. All five of JBComplete's interior images — added specifically to cover
  that phenotype — would have been mislabelled.
- `illustration-technical-drawing` (index 19) sat behind the whole packshot block. A technical drawing
  shot front-on, on white, fully in frame satisfies `front-packshot` (index 7) and loses. Not a strict
  subsumption, but unreachable for the common case, which is the same failure.

`lifestyle-hero` ⊂ `lifestyle-context` is **not** a defect: the more permissive rule sits second and
correctly catches the remainder (`intersection-count >= 2`). Both reachable.

**3. Every phenotype must be consumed.** 19 of 21 were; `model-detail-closeup` and `lifestyle-context`
were not.

**4. Every det slot must be reachable from a phenotype.** All but `default.det6` (`"pack"`), whose
`phenotypes` list is empty because `packaging-visible` was removed in T-4700. **It is not dead code**:
`ImageOrderer.BuildCandidates` can never win it by phenotype, but `ImageOrderer.ResolveHintSlot` still
uses its keyword as an overflow anchor, so a `*_packaging.jpg` still sorts to that position.

## Decisions taken (user, 2026-08-04)

**`ghost-*` → merged into `*-packshot`.** Judged against this ticket's own collision-keeping rule: a
collision survives only when a **named** signal that would separate the two is specified, or
measurable-but-unmeasured. The separating property here is *does the garment hold a worn 3D shape* —
real, and `test/datasets/JBComplete/README.md` §4.3 names three concrete cues (waistband holds an open
rounded form rather than collapsing to two flat edges; legs carry internal volume; shadows *inside*
the garment opening). But **no analyzer is specified and nothing measures any of the three**, so the
collision did not qualify to survive. Deleting cost nothing operationally: every det slot listing a
ghost phenotype also listed its packshot equivalent, so no slot lost its filler and no image changed
slot. `*-packshot` now explicitly covers both the flat lay and the ghost-mannequin shot.

On the ticket's question "whether such a signal is realistically obtainable — establish, do not
assume": partially answered. The cues are real and were used successfully by a human at ~700 px, so
the signal is not fictional. But every ghost rule also gated on `hero-orientation`, which is not
reliable today (74% argmax, thresholds above anything CLIP reaches on real data). Stacking a harder
3D-shape signal on an unreliable orientation gate buys nothing measurable until [[T-2600]] resolves.
Fix orientation first, then revisit.

**Both orphans wired in rather than deleted.** `model-detail-closeup` is the human-branch twin of
`closeup-image`, and every `detail` slot listed only `closeup-image` — so an on-model detail crop
could never win a detail slot and always overflowed. `lifestyle-context` is the natural remainder of
`lifestyle-hero` and the `lifestyle` slots listed only the hero. Each appended behind its existing
sibling, so preference order is unchanged and no existing win is displaced.

**All three structural fixes taken:** `interior-shot` moved ahead of `closeup-image`; `default.det6`
left as a documented keyword-only slot; `interior-detected` and `is-illustration` added to
`ImageFeatures.md` (both already had real producers — `Analyzer_Interior`, `Analyzer_IsIllustration` —
and were declared in `ImageNGP.json`; only the catalog rows were missing).

**Extended beyond the question asked, and flagged:** `illustration-technical-drawing` was moved ahead
of the packshot block too. Same defect as `interior-shot`, same fix; leaving it would have shipped
half the fix.

## The resulting order

Evaluation order **is** precedence. It now runs: the seven human-branch rules → the two specific
content detectors (`illustration-technical-drawing`, `interior-shot`) → the six packshots →
`closeup-image` → `lifestyle-hero` → `lifestyle-context`. Governing principle, now written into
`ImageRoles.json`'s header comment and `imagePhenotypes.md`: **a specific rule must precede a generic
rule it overlaps, or the generic one silently swallows it.**

## Also changed

- `expected-phenotype.json`: 23 rows relabelled from `ghost-*` to their packshot equivalents
  (12 front, 9 back, 2 side). No image lost coverage; 17 of 18 phenotypes still have a positive case,
  only `illustration-technical-drawing` has none. The ghost-vs-flat-lay judgement those rows encoded
  is preserved in `test/datasets/JBComplete/README.md` §4.3, which is now its only record.
- New test `PhenotypeRuleSetTests.Load_EverySlotPhenotypeInDetOrderRulesExists` — asserts
  `DetOrderRules.json` never names a phenotype the taxonomy does not define. This is the check that
  would have caught stale ghost entries left behind after a rule deletion.
- `Load_ValidPath_JsonContains21Phenotypes` → `...Contains18Phenotypes`.

**Verification:** `dotnet build` 0 errors 0 warnings. Matching 230/231, Core 150/150, Transform 77/83,
Generate 10/10, Upscale 17/17. The 1 Matching and 6 Transform failures are pre-existing [[T-5000]] and
[[T-5010]], confirmed untouched via `git diff HEAD` and identical in count and name to before this work.

**Still open, deliberately:** `on-model-with-accessories` overlapping `front-on-model-partial` (all
three JBComplete scarf images satisfy both, earlier wins). It is an overlap, not a subsumption, and no
image in the repo distinguishes them — so it needs data, not a rule edit. Not folded into this ticket.

**Files:** `jb/src/core/config/ImageRoles.json`, `jb/src/core/config/DetOrderRules.json`,
`jb/src/core/config/ImageNGP.json`, `jb/docs/ImageNGP/imagePhenotypes.md`,
`jb/docs/ImageNGP/ImageFeatures.md`, `jb/docs/ImageNGP/PRODUCTTYPES.MD`,
`jb/src/tests/Prism.Services.Matching.Tests/Classify/PhenotypeRuleSetTests.cs`,
`test/datasets/JBComplete/expected-phenotype.json`, `test/datasets/JBComplete/README.md`.

---

### T-5030 · Normalize every input to JPG on white
**Status:** Done (2026-08-05) | **Profile:** P1-feature-worker
**Landed:** `69b5eba`
**Review:** Approve (2026-08-04) — reviewer independently traced `MatchingService.Refine` →
`FeatureAnalysisService.Refine` → `ImageFeatureAnalyzer.AnalyzeBackground` and confirmed the
`clipping-path` unreachability claim at source, plus config/JSON parity in both directions. One
follow-up it raised (a second stale `ghost-back` row in `test/datasets/CiGolden/README.md`) is fixed.

Drop the separate handling path for images with an alpha channel. Every input becomes a JPG
composited onto a white background at import, before any analysis runs.

**Correction to the ticket's premise, found while working it.** Two of the stated facts were wrong,
and they changed what the work actually was:

1. **Compositing onto white was already happening.** `Importer.LoadImageWithExifOrientation` already
   called `context.BackgroundColor(Color.White)` unconditionally and `TryNormalizeToJpeg` already
   wrote JPEG, for every accepted format. Bullet 1 was already true and was not re-implemented.
2. **`clipping-path = true` was already unreachable in production.**
   `ImageFeatureAnalyzer.AnalyzeBackground` computed `hasAlpha` as
   `DecodedImageFormat != JpegFormat.Instance && HasTransparentPixels(...)`, and `Refine` always loads
   `NormalizedJpgPath`, which Import always writes as JPEG. So the first conjunct was always false and
   the feature had **never once been `true`** on a pipeline image. It fired only in unit tests that
   built a PNG in memory. Independently confirmed.
3. **It was three `ghost-*` rules, not six**, and `clipping-path` was an *alternative* inside an
   `anyOf`, not a requirement — `background-type = SOLIDCOLOR` satisfied the same clause. The "six"
   came from `imagePhenotypes.md`, which also listed the alternative on the six `*-packshot` rules
   that `ImageRoles.json` never had. That doc/config drift is now fixed.

So the real work was removing the separate alpha *capture* path, not adding a composite.

**Product decision (user, 2026-08-04): remove `clipping-path` outright.** Rationale in the user's
words — "reduced complexity with identical end-result is more of a boon than a bane". It is literally
identical: the feature had never been `true`. Deleted from `ImageNGP.json`, the three `ghost-*` rules
in `ImageRoles.json`, `ImageFeatureAnalyzer`, `ClassifyConfig.json`, and every doc.

Consequence, handed to [[T-5040]]: with `clipping-path` gone the three ghost rules became
character-for-character identical to their packshot counterparts and provably unreachable. T-5040
merged them into `*-packshot`.

**What was done:**
- [x] Confirmed alpha→white compositing already happens at import; JPG already emitted.
- [x] Deleted `AlphaSubjectCapture.cs` and the `Subject` threading through
      `LoadImageWithExifOrientation` / `TryNormalizeToJpeg` / `NormalizeAndRecord`.
- [x] Removed `ImageRecord_INPUT.Subject`, `MatchingService.PrepareLambda`'s `Subject = source.Subject`,
      the dead `if (lambda.Subject is not null) return;` guard in `FeatureAnalysisService.DetectSubject`,
      and the `Producer == "alpha"` branch in `Analyzer_ShadowPresence`.
- [x] Removed `hasAlpha`, `HasTransparentPixels`, and the `if (hasAlpha) bgType = SOLIDCOLOR` branch
      from `ImageFeatureAnalyzer`. `transparent-background` is now an unconditional `false` —
      diagnostic only, no phenotype rule consumes it.
- [x] Config keys removed on both sides in step (no shadow defaults):
      `AlphaCaptureOpacityThreshold` / `AlphaCaptureEdgeContactFraction` (`PrismConfiguration.cs` ↔
      `Prism_Config.json` `Ingress.AlphaCapture`), `LifestyleBackgroundAlphaConfidence` and
      `ClippingPathConfidence` (`ImageFeatureAnalyzer.Config` ↔ `ClassifyConfig.json`).
- [x] `SubjectDetectionResult.Producer` **kept** — it still separates `classical-cv` from `edge-bleed`
      and leaves room for a segmentation-model producer; that never depended on alpha.
- [x] Tests: deleted `ImporterAlphaSubjectTests.cs`; updated `ImporterFixture`,
      `ImageFeatureAnalyzerTests`, `AnalyzerShadowPresenceTests`, `ClassifyConfigTests`,
      `PhenotypeRuleSetTests`.
- [x] 13 JBComplete PNGs import and match cleanly. FILA94 verified on a 15-PNG sample via a
      throwaway test (the full 12 GB / 1374-file set stalls the multipart runner): 15/15 imported,
      0 KO, correct dimensions, corners composited to white.
- [x] Docs: `PRISM-classify.md`, `ImageFeatures.md`, `imagePhenotypes.md`, `NGP-architecture.md`,
      `PRODUCTTYPES.MD`, `HowToAddAPhenotype.md`, `ideas-on-NGP.md`, `jbtodo.md`,
      `test/datasets/CiGolden/README.md`, `test/datasets/JBComplete/README.md`.

**Dependent tickets re-checked:**
- [[T-4960]] (alpha box vs colour fallback) — **fully obsoleted.** There is no alpha-derived box left
  anywhere in the system, so there is nothing for `Analyzer_SubjectGeometry` to prefer. Close it.
- [[T-4950]] (SubjectMask on the wire) — **partly obsoleted.** Its "both producers encode a mask"
  framing is stale (only `classical-cv` does now), but the actual keep / `[JsonIgnore]` / config-gate
  decision is untouched and still open. Alpha's removal only shrinks the unread payload.

**What no longer exists, stated plainly:** once alpha is gone there is **no signal that distinguishes
"cut out against a flat background" from "shot on a seamless white sweep."** Both present identically
downstream — flat JPEG, uniform near-white corners, `background-type = SOLIDCOLOR`,
`white-background = true`. Reviving `clipping-path` would require a genuinely new measurement, not a
repurposing of anything alpha provided.

**Verification:** `dotnet build` 0 errors 0 warnings. Matching 230/231, Core 150/150, Transform 77/83,
Generate 10/10, Upscale 17/17. The 1 Matching and 6 Transform failures are pre-existing [[T-5000]] and
[[T-5010]], confirmed untouched via `git diff HEAD`.

**Files:** `jb/src/core/lib/Ingress/Importer.cs`, `jb/src/core/lib/Ingress/AlphaSubjectCapture.cs`
(deleted), `jb/src/core/Models/ImageRecord_INPUT.cs`, `jb/src/core/Services/Matching/MatchingService.cs`,
`jb/src/core/Services/Matching/FeatureAnalysisService.cs`,
`jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`,
`jb/src/core/Services/Matching/Analyzers/Analyzer_ShadowPresence.cs`,
`jb/src/core/Services/Matching/SubjectDetectionResult.cs`, `jb/src/core/config/PrismConfiguration.cs`,
`jb/src/core/config/Prism_Config.json`, `jb/src/core/config/ClassifyConfig.json`,
`jb/src/core/config/ImageNGP.json`, `jb/src/core/config/ImageRoles.json`.

---

### T-5020 · Folder names never reach the matcher
**Status:** Done (2026-08-05) | **Profile:** P1-feature-worker
**Review:** Approve (2026-08-04)
**Landed:** `69b5eba`

An image's folder is often the only thing that identifies it — `1.jpg` inside `26182-Denim-801/` is
useless on its own and unambiguous with its folder. `FolderNameEnricher` exists to borrow that folder
name. It could never do so through the normal job path, and even when reached it failed on one of the
two folder shapes in `test/datasets/JBComplete/`.

Three separate causes, all now fixed.

**1. The test runner threw the folder away before upload.** — FIXED
`test/test-scripts/PrismJobRunner.psm1`. `Get-PrismJobInputFiles` de-duplicated by *leaf filename*
(`$seen.Add($file.Name)`), so `26182-Denim-801/1.jpg` and `foldercontainsID99984905/1.jpg` collided on
`1.jpg` and the second was silently dropped; `Submit-PrismJob` then uploaded as
`[Path]::GetFileName($path)` and packed ZIP entries the same way.

Now: a new `Get-PrismRelativePath` helper computes each file's path relative to the submitted root
(and, for ZIP-expanded files, relative to that ZIP's own expansion dir, so the archive's internal
structure survives but the `zip0/` scaffolding does not). That relative path is the de-dup key, the
ZIP entry name, and the multipart part filename. `Get-PrismJobInputFiles` now returns
`{FullName, RelativePath}` objects; `Submit-PrismJob`'s `-Files` widened to `[object[]]`.
`Invoke-CiPipeline.ps1` and every `Run_*.ps1` pass the value through opaquely and needed no change.

**2. The core ZIP reader stripped member folders.** — FIXED
`ZipHandler.cs` set `originalFileName = Path.GetFileName(memberPath)`. Now a new
`BuildOriginalFileName` helper returns the full in-archive path with `\` normalised to `/`.
`memberPath` itself is untouched everywhere it is used for KO records, safe-extraction-path building
and the encrypted-entry lookup. No `Importer.cs` change was needed: the widened value flows unchanged
into `InitialFullName`, and `BuildNormalizedFileName` is safe with a `/` in its input because
`Path.GetFileNameWithoutExtension` strips the directory part before the invalid-char filter runs.
A nested-ZIP member keeps its own innermost-archive-relative path; archive provenance stays separate
on `ArchivePath`.

**3. `MeaningfulTokens` kept a mixed letter+digit run whole and skipped the split.** — FIXED
`FolderNameEnricher.cs`. The `continue` after adding a whole mixed run skipped the letter↔digit split
below it. Now a `CollectRunTokens` helper emits the split pieces **in addition to** the whole run,
through the same length/noise/bare-number filters. (Extracted to a helper because inline it pushed
Sonar cognitive complexity to 19 against a 15 ceiling.)

The optional digit-run concatenation (`26182-801` → `26182801`, ticket bullet 4) was **declined**:
it is not needed for JBComplete (`26182` alone already carries that folder), and it manufactures a
token present in neither the folder name nor the Excel — in exactly the shape of a real 8-digit
FamilyID, so it could collide with an unrelated family with no textual basis.

**Probe, re-run 2026-08-04** — real `FolderNameEnricher`, real `MatchingConfig.json` tuning, real
`Brackets-Complete.xlsx` rows (34 families parsed):

| Folder | Alias assigned? |
|---|---|
| `26182-Denim-801/` | **yes**, all 3 files |
| `foldercontainsID99984905/` | **yes**, all 4 files — was **no** |
| `99984901/` | no — correct, the filenames already carry the ID |

One correction to the original ticket text: the alphabetic split piece is `foldercontainsid`, not
`foldercontains`. `MeaningfulTokens` lowercases before tokenising, which destroys the `s`→`I` case
boundary, and `AlphaDigitBoundaryPattern` splits only at letter↔digit transitions. Irrelevant to the
outcome — `99984905` is the piece that has to reach the vocabulary, and it does.

**What was done:**
- [x] Carry the path relative to the submitted root through runner de-dup key, multipart part name,
      ZIP entry name, and `ZipHandler`'s `originalFileName`.
- [x] Duplicate decision now keys on the full relative path, not the leaf name.
- [x] `MeaningfulTokens` emits the letter↔digit split in addition to the whole run.
      `MatcherUpgradeTests.FolderNameEnricher_MeaninglessFileInMeaningfulFolder_BorrowsFolderName`
      still passes; 3 new tests pin the split, the whole-run survival, and the digit-tail-only case.
- [x] Digit-run concatenation considered and declined, with reasoning above.
- [x] Probe re-run; 9 subfolder images added to `expected-match.json` (90 → 99 entries, 20 → 22
      rejections, 77 matches across 26 families, ordinal sort order preserved).
- [x] `ImporterZipTests.ZipMemberInSubfolder_InitialFullNamePreservesFolderPath` added.
- [x] `test/datasets/JBComplete/README.md` §2.3, §4.1, §4.2, §4.4, §5 updated to the measured result.

**Verification:** `dotnet build` 0 errors. Matching 230/231, Core 150/150, Transform 77/83,
Generate 10/10, Upscale 17/17. The 1 Matching failure is `AnalyzerConfigTests` expecting
`Filename.OrientationConfidence` 0.75 against a shipped 0.60 — confirmed untouched by this work via
`git diff HEAD`, belongs to [[T-5000]]. The 6 Transform failures are [[T-5010]]'s known stale routing
fixtures, same six as before this work.

**Not verified:** whether a multipart part *filename* containing `/` survives ASP.NET Core's form
parser end to end. Source reading says yes — `PrismProcessIngressReader.AddUploadedInputRecords` sets
`InitialFullName = file.FileName` verbatim with no `GetFileName` stripping, and `/` needs no escaping
in a `Content-Disposition` quoted string — but no live HTTP round-trip was run. It matters little:
only the single seed image travels loose, every other image goes through the ZIP path, which is
covered by cause 2's test.

**Files:** `test/test-scripts/PrismJobRunner.psm1`, `jb/src/core/lib/Zip/ZipHandler.cs`,
`jb/src/core/lib/Zip/ZipExtractedMember.cs`, `jb/src/core/lib/Zip/ZipMemberKoRecord.cs`,
`jb/src/core/Services/Matching/Match/FolderNameEnricher.cs`,
`jb/src/tests/Prism.Core.Tests/Ingest/ImporterZipTests.cs`,
`jb/src/tests/Prism.Services.Matching.Tests/Match/MatcherUpgradeTests.cs`,
`test/datasets/JBComplete/expected-match.json`, `test/datasets/JBComplete/README.md`.

---

### T-4970 · Phenotype assignment validation (first + second pass)
**Status:** Done (2026-08-03) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-31)
**Found by:** [[T-2600]] rewrite, 2026-07-29

**Reviewer verdict (2026-07-31): Approve, one non-blocking warning.** Both attack points this ticket
named were checked against the code, not taken on faith.

*Attack point 1 — the `occlusion-level` → `intersection-count` substitution is behaviour-identical in
12 of the 13 rules, and the 13th was never claimed to be.* Scored against the deleted
`DeriveOcclusionLevel` mapping (0→full-product, 1→mostly-visible, 2→partially-occluded, ≥3→closeup):
nine rules stating a bare `full-product` became `intersection-count = 0`; `side-on-model` and one other
stating `anyOf[full-product, mostly-visible]` became `max:1`; `closeup-image`'s old
`intersection-count min:1` AND `occlusion-level = closeup` became `min:3`, which is the same set
because the `min:1` clause was already dead under that AND. The 13th, `model-detail-closeup`, is a
deliberate narrowing from ≥2 to ≥3, disclosed in both the commit message and `imagePhenotypes.md`.

*Attack point 2 — the bottomwear det5 insertion is safe for families that don't contain the new
phenotype, and intentionally changes filenames for those that do.* `ImageOrderer.CompactDetOrder`
(`ImageOrderer.cs:41-51`) renumbers only the images actually present in a family, by relative order.
A bottomwear family with no back-cropped-model shot never claims slot 5, so `front-on-model-partial`(4)
< `lifestyle-hero`(6) < `label`(7) < `material`(8) compacts exactly as before — byte-identical output
names. A family that *does* contain one gets the new image at det5 and everything after it shifts,
which is the whole point of adding a phenotype that covers 48% of the catalogue. Real, intentional,
user-decided — worth confirming customers were told, not a defect.

Also verified: `back-on-model-partial`'s det slots match this ticket across all five product types; it
cannot shadow or be shadowed by `back-on-model-full-product` (`body-visible` ∈ {three-quarter, half,
bust} vs `full` are mutually exclusive); no phenotype became unreachable or duplicated;
`OcclusionLevelConfidence` was removed from `ClassifyConfig.json` *and* its `Config` record *and* its
validation line, so no shadow default was left behind; `DeriveOcclusionLevel` is deleted, not
commented out; and `97326fe` shares no file with `07c886b`, so the threshold tuning does not muddy
this diff.

⚠️ **Warning, does not block Done.** `PhenotypeRuleSetTests.cs` asserts only that the phenotype count
went 20 → 21 (`Load_ValidPath_JsonContains21Phenotypes`). No direct `ruleSet.Assign(...)` test
exercises `back-on-model-partial` itself — neither a positive case nor a negative one — while every
other materially-rewritten rule in this diff has at least one. The SPACINI29 measurement covers it end
to end, so this is a coverage gap rather than a correctness risk. Close it before the rule is trusted
unattended.

**Measured 2026-07-30, twice. Full write-up: `jb/docs/ImageNGP/phenotype-assignment-validation.md`**
(indexed in `PRISM-index.md`); HTML report + raw dumps on the Desktop under `T-4970-phenotype-evidence/`.
The rule replay reproduces the shipped pipeline **86/86 exactly**, so the rule engine itself is sound.

**First pass said "7% coverage, remedy is two config thresholds". That was tested and is wrong.**
Lowering `hero-orientation` alone changes nothing — every rule reading orientation also reads
`body-visible`, UNKNOWN on 72/86 at its own bar. Lowering all three raises coverage 7% → 62% and the extra
assignments are mostly incorrect; at the then-shipped config 5 images were assigned and **all 5 wrong**.

**Three findings that superseded the threshold question, and the decisions taken on them (user,
2026-07-30, implemented in `07c886b`):**
1. `model-detail-closeup` over-fired on any edge-cropped model shot (55/86 read `partially-occluded`
   simply for touching a frame edge, while **zero** images are honestly detail crops) → **narrowed to
   `intersection-count >= 3`**.
2. `occlusion-level` was `intersection-count` in disguise (producer derived it 0/1/2/3+ →
   full-product/mostly-visible/partially-occluded/closeup) → **deleted from the taxonomy**; all 13 rules
   now state `intersection-count` directly. To return later as a real measurement.
3. A back view cut by a frame edge is 38/86 (44%) and had no correct label at any threshold →
   **`back-on-model-partial` added** (21 phenotypes now). Det slots per user: topwear det1 *ahead of*
   `back-on-model-full-product`; bottomwear new det5 (lifestyle/label/material shift one later); footwear
   new det7 before lifestyle; bags-accessories + default share the existing back slot behind
   `back-packshot`, so a real back packshot always wins.

**Measured effect of the rule changes alone, at a 0.30 bar, against hand-verified labels over all 86
images: correct 15 → 33, wrong 38 → 13.** All 13 survivors are upstream — 9 CLIP orientation errors, 4
detector under-counts — none fixable in `ImageRoles.json`. Suites green: Matching 229/229, Core 154/154,
Transform 83/83, Generate 10/10, Upscale 17/17.

**Ground truth now exists** at `test/datasets/SPACINI29/RAW IMAGES/dataset notes.md` (user-authored,
do-not-edit). It corrected this ticket's own scoring — **no SPACINI29 image is fully in frame**, so the
earlier "3 front-full + 3 back-full" labels were wrong — and gave the first accuracy figures:
`intersection-count` 65/86 = 76% (every error an under-count), `hero-is-human` 85/86.

**Spun off:** [[T-4990]] (detector under-counts intersections), [[T-5000]] (filename orientation analyzer),
[[T-4980]] (CiMini golden red), [[T-5010]] (centre-and-stretch unreachable). [[T-4955]] reclassified from
cleanup to prerequisite.

**Structural rule findings, flagged not changed:** `ghost-side` is unreachable on SOLIDCOLOR
(`side-packshot` is identical bar the background clause and evaluates first); `ghost-back` silently catches
*intersecting* back packshots because `back-packshot` requires `intersection-count=0` and `ghost-back`
carries no intersection condition.

**Coverage limits of this measurement.** Only 3 of the then-20 phenotypes have a positive case anywhere in
SPACINI29 (`front-on-model-partial` 42, plus 3+3 full-product) — the rest are absent, not failing. The
non-solid-background half was **not** measured: MMERO26 is the only such dataset and a 60-image subset
KO'd 59/60 on `MATCHES_MULTIPLE_FAMILYIDS` (a KO'd image never reaches `Refine`,
`MatchingService.cs:315`) — partly a subsetting artifact, so it proves nothing about the full 2024-image
set, but `lifestyle-hero`/`lifestyle-context` remain unmeasured. An image-by-image spec for a purpose-built
dataset is in the **root `jbtodo.md`**; the same asset serves [[T-4945]] and [[T-4948]].

**Ordering impact: phenotypes contributed nothing at measurement time.** 100% of OK images left Ordered
with `IsOverflow: true`, and `model-detail-closeup`/`lifestyle-context` appear in no det slot for any of
the 5 product types. Output filenames are fine — `Exporter.Run` calls `ImageOrderer.CompactDetOrder`
(checked; archived T-2830's symptom stays fixed).

**Open decisions this measurement deliberately did not take** (full list at the end of the doc): whether
`model-detail-closeup`/`lifestyle-context` deserve det slots; whether `ghost-back`'s intersection gap is a
defect; whether `Analyzer_MultipleProducts` should write `false`; whether CLIP's dead `hero-is-human` path
(bar 0.90, max 0.867 — YOLO covers it) should be re-barred.

**Both pre-Done items are now closed:**
1. **Reviewer verdict.** ✅ `Approve (2026-07-31)`, recorded above.
2. **Re-measure at the shipped thresholds.** ✅ Done 2026-08-03 — see below.

**Re-measure at shipped thresholds (2026-08-03).** Config confirmed at HEAD to match the `97326fe` diff
exactly (`hero-orientation` 0.33, `head-visible` 0.25, `body-visible` 0.10, `OrientationConfidence` 0.60).
Evidence harness run twice over SPACINI29 (86) + CiMini (14); report + raw dumps at
`Desktop/T-4970-remeasure/`.

*The brief was to verify the measurement apparatus is reliable, not to chase a green number — red or green
were equally acceptable.* It is reliable, on six independent checks: two full runs (fresh process, fresh
ONNX load each) produced **byte-for-byte identical** JSON for both datasets; output is substantive, not
vacuous (86/86 OK, 0 KO, 86/86 carry influential CLIP tags, confidences span a real range rather than a
constant); every feature records value + confidence + **producing source** (`clip`/`yolo`/`heuristic`) with
the CLIP score appearing verbatim; both a positive (`23211018_56_A.jpg` → `front-on-model-partial`) and a
negative (`20213024_46_B.jpg` → none, fails `back-on-model-full-product` solely on `head-visible = FULL` vs
the rule's `anyOf[NONE, PARTIAL]`) were **re-derived by hand from the dump plus `ImageRoles.json` alone**;
the threshold gate is exact — 37 images score below the 0.33 bar and exactly 37 read UNKNOWN; and the
worktree was left clean (harness deleted).

*Numbers produced.* **Coverage 37.2% (32/86)** on SPACINI29, 28.6% (4/14) on CiMini. Assignments:
`back-on-model-partial` 18, `front-on-model-partial` 11, `front-on-model-full-product` 3 — i.e. the
phenotype this ticket added is the single largest assignment and fires on the case it was created for. Of
the 54 unassigned, 11 matched no candidate rule at all; 43 matched candidates but no rule won outright.

*Downstream delta.* This ticket previously recorded 100% of OK images leaving Ordered with
`IsOverflow: true`. At the shipped thresholds that is no longer true: **27 of 86 now get a real
phenotype-driven det slot** (slots in use: 0, 1, 5, 8, 9, 10), 59 still overflow. The threshold change had
a real ordering effect, not just a coverage-counter effect.

*Ceiling explained, not just asserted.* Highest `hero-orientation` confidence observed anywhere on this
dataset is 0.5817, so the pre-`97326fe` bar of 0.60 sat above every score CLIP can produce here — this is
the mechanism behind the original "7% coverage" finding.

*Deliberately NOT scored:* whether those 32 assignments are **correct**. That needs the hand-labelled ground
truth in `dataset notes.md` and is the accuracy question, separate from the reliability question this run
answered. Non-solid-background coverage also remains unmeasured (MMERO26 still KO-heavy) — unchanged from
the body above.

**Files:** `jb/docs/ImageNGP/phenotype-assignment-validation.md`, `jb/src/core/config/ImageRoles.json`,
`jb/src/core/config/ImageNGP.json`, `jb/src/core/config/DetOrderRules.json`,
`jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`.

---

### T-4900 · ESRGAN toggle + unified final-size upscale (epic)
**Status:** Done (2026-07-30) | **Profile:** P0-orchestrator
**Found by:** 2026-07-28 upscale-perf investigation (see `memory/project_transform_upscale_bottleneck.md`)

**All five children are Done with reviewer Approve (T-4905/T-4930/T-4940 on 2026-07-29; T-4910/T-4920 on
2026-07-30).** Decisions in `jb/docs/PRISM-transform-generate.md` → "Unified upscale"; API field in
`PRISM-api.md`. This unblocks [[T-4970]].

**Two dormant defects the reviews surfaced, both keyed to the same future event.** Neither is reachable
while `BypassPhenotypes = true`, and both become live the moment it is flipped — so they are a checklist
for that flip, not open work now:
1. `FinalOutputSize.RoutesToCenterAndStretch` omits the `SelectedPhenotype is null` half of
   `SelectTransformer`'s Step 1 ([[T-4910]]).
2. `Tx_ProblemImageProcessor` derives its output metadata from the unscaled original-resolution field
   ([[T-4920]]).

**Three defects the epic uncovered and fixed along the way** (user decisions, 2026-07-29 — all three were
blocking the epic's own premise, not scope creep):
1. **The bounding box was never rescaled after upscale.** `UpscaleAsync` enlarged the bytes while
   `lambda.BoundingBox` stayed in original-image pixels, so `Tx_CenterAndStretch` cropped an
   original-coordinate rect out of an enlarged image — wrong region, and the canvas was still sized off the
   un-scaled bbox, so the output never reached 800px anyway. The ON path was paying full ESRGAN cost for an
   output that met neither the crop nor the size it claimed. Geometry now scales with the pixels.
2. **`Tx_CropSquare.Transform` never applied its crop.** It recorded a `CropRectangle` on the OutputRecord
   without touching `ProcessedBytes`, and Export ships `ProcessedBytes` — so the exported file was the whole
   frame while the manifest claimed a square. Under `BypassPhenotypes = true` that is the route every
   intersecting image takes. It now crops the bytes.
3. **Upscale sized against the pre-promotion box.** Subject promotion and shadow accounting ran in
   `ImageTransformer` *after* preprocessing, so upscale measured a box Transform then replaced. Promotion +
   shadow accounting moved into `ImageTransformer.FinalizeGeometry`, called from `PreprocessAsync` before the
   upscale decision.

Tracking ticket. **Problem:** the upscale stage (Real-ESRGAN, in `ImagePreProcessor.UpscaleAsync`) is the
pipeline's dominant cost — measured **122.9s per 800×800 image on the GPU** with the old fixed-64 model,
and even after the dynamic-model fix (T-4905) it's ~**10s/image** of genuine Real-ESRGAN compute. On a
~1900-image set that is still hours, and desktop users without a capable GPU will not tolerate it.
**Goal:** make ESRGAN opt-in. Add a user-set toggle (**default OFF**); when OFF, upscale with plain
Lanczos, and only *as little as needed* to clear the final-image 800px bar (capped at +33%). When ON,
ESRGAN runs (now fast via the dynamic model). Both paths target the **same** exact final-output-size bar
(unified — user decision 2026-07-28).

**Settled decisions (user, 2026-07-28):** (1) shortfall — if the applicable cap can't reach the bar,
**KO the image** (fail-loud, like today's upscale-exceeded KO); (2) targeting — **unified**: ON and OFF
both target final ≥ bar (ON caps at the existing ESRGAN `MaxUpScaleFactor`, OFF caps at the new
Lanczos-only cap); (3) scope — **includes the workbench UI** toggle; (4) bleed images — target the output
dimension **directly, no margin term** (only zero-intersection images get the `×(1+2·margin)` discount);
(5) **exactly one upscale location** is mandatory — the final size is *exactly* computable pre-transform
from the already-known bbox + intersection state + margin config (reuse each routing's canvas-size
formula), so upscale stays where it is (`ImagePreProcessor.UpscaleAsync`) with an exact final-size calc —
no post-transform move, no split, no prediction/approximation.

**All values from config, never hardcoded** (no-shadow-defaults rule): reuse `MinOutputWidth` (800) as the
FINAL-image bar; new Lanczos-only cap key (proposed `Output.Images.Resize.MAXIMUM_UpScale_LanczosOnly` =
1.33 → `PrismConfiguration.MaxLanczosOnlyUpScaleFactor`); margin from `CropTransformSettings.WhiteSpaceMargin`
(0.042, transform_Config — note the cross-config read). Children: T-4905 (done, review pending), T-4910,
T-4920, T-4930, T-4940. Index ticket, not a unit of work.

**Files:** `AGENT-TICKETS.md`, `memory/project_transform_upscale_bottleneck.md`.

---

### T-4905 · Dynamic-shape ESRGAN export + even-dimension padding
**Status:** Done (2026-07-29) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-29)
**Found by:** [[T-4900]]

**Reviewer verdict (2026-07-29): Approve, no defects.** The review did not take the ticket's prose on faith —
it loaded both `.onnx` files and hashed all 702 initializers in each: identical SHA256, identical 1226-node
graph, the sole difference being the declared input (`[batch_size,3,64,64]` → `[batch,3,height,width]`). The
even-padding math was traced by hand for the dynamic branch: `overlap=0`/`discard=0` forces exactly one tile,
every in-bounds pixel gets weight 1.0 so `NormalizeAccumulator` never divides by zero, and the bounds checks
drop precisely the padded-then-doubled rows — a top-left crop to `src×2` with no off-by-one. Fixed-64 tiling
confirmed untouched. Upscale suite run in the foreground: 17/17. One non-blocking observation: the new test is
black-box at `Upscaler.Upscale` level, so it would also pass on the old tiling path — not a gap for the
shipped config, but a more surgical `RunTiled`/`RoundUpToEven` unit test would be sharper.

**Implemented 2026-07-28.** The committed `Real-ESRGAN_x2plus.onnx`
had a fixed `[1,3,64,64]` input, so an 800px image was upscaled as **625 serialized 64×64 tile Runs**
(~0.2s DirectML dispatch overhead each = 122.9s). The RRDBNet is already spatially size-agnostic
internally (pixel_unshuffle derives shape from `Shape(input)`; both Resize use scales `[1,1,2,2]`); only
the declared input shape pinned it to 64. A **metadata-only** edit (input dims → dynamic `height`/`width`,
weights untouched, bit-identical output) makes it accept whole images in one Run. Proven on the GPU:
**122.9s → 10.19s, ~12×**, correct 1600×1600 output. Changes landed: `Prism_Config.json`
`Models.Upscale.Path` → `Real-ESRGAN_x2plus_dynamic.onnx`; `Upscaler.RunTiled` rounds the whole-image
(dynamic) tile up to even H/W — the `pixel_unshuffle(2)` rejects odd dims and the existing pad+accumulator
clips the ×2 overshoot back; new `UpscalerTests.Upscale_OddSizedImage_ProducesExactlyDoubledOutput` (401×399
→ 802×798 real inference). Whole-image single-pass is the chosen mode; a configurable capped tile (e.g.
512) is the documented fallback if a large image ever OOMs the GPU. Acceptance: reviewer confirms the
metadata-only diff is lossless and the even-padding math; Upscale suite green (17/17). The dynamic `.onnx`
is gitignored (too big for git) and lives in the source tree next to the fixed-64 backup.

**Files:** `jb/src/core/config/Prism_Config.json`,
`jb/src/core/Services/Upscale/Engine/Upscaler.cs`,
`jb/src/tests/Prism.Services.Upscale.Tests/Upscale/UpscalerTests.cs`.

---

### T-4910 · Exact final-output-size calculator (shared helper)
**Status:** Done (2026-07-30) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-30)
**Found by:** [[T-4900]]

**Reviewer verdict (2026-07-30): Approve, no defects.** The forward/inverse pair was not taken on faith —
the reviewer re-implemented `CenterAndStretchCanvasSize`/`RequiredBboxLongestSide` in a throwaway console
app and brute-forced the true minimum against it across margins 0.0001–0.1999 (step 0.0001) × targets
1–2000: **zero mismatches**, worst-case **3** iterations, matching the "≤3 passes, never a pixel short"
claim exactly. Single-source-of-truth confirmed by grep — no surviving copy of the canvas formula or the
routing predicate anywhere in `jb/src/core`.

The ordering change was traced through its one real edge case rather than assumed safe. `PreprocessAsync`
has a **pre-existing** early-return (`ReadNormalizedJpg` null, or `colorMat.Empty()`) that returns
`(null, null)` *without* setting `lambda.IsKo`, so `TransformService`'s `if (lambda.IsKo)` guard misses it
and `TransformImage` still runs with `FinalizeGeometry` never having executed. Not a regression:
`lambda.BoundingBox` is written in exactly two places (`PreprocessAsync` and `FinalizeGeometry`'s
promotion), so skipping both leaves it null and `SelectTransformer`'s first guard routes to
`Tx_ProblemImageProcessor`. The reviewer's read is that the move made this edge case **safer** — under the
old code promotion ran unconditionally inside `TransformImage`, so a populated `lambda.Subject` could
promote a box and route to a real crop strategy against a null `colorMat`.

**Three non-blocking findings, worth knowing:**
1. **`RoutesToCenterAndStretch` encodes 2 of `SelectTransformer`'s 3 branch conditions** — bbox-null and
   edge-intersect, but not the `SelectedPhenotype is null` half of Step 1. Harmless today because
   `BypassPhenotypes = true` collapses Step 1 to the bbox-null check, so predicate and routing agree. **Flip
   `BypassPhenotypes` without revisiting this and they diverge** for a bbox-present/no-intersect/
   phenotype-null record: the predicate says centre-and-stretch, the real routing says
   `Tx_ProblemImageProcessor`. Attach this to the flip decision in [[T-2600]]/[[T-4970]].
2. **Two of the 15 assertions are re-derivations, not literals** (`FinalOutputSizeTests.cs:51` and `:68` call
   `CenterAndStretchCanvasSize`/`LongestDimension` back on the scaled result). Both sit inside facts that
   also carry a properly-pinned literal, so coverage stands — but the claim below that the suite pins
   literals throughout was overstated. Corrected: **9 facts / 15 assertions**, not "10 assertions across 8".
3. `ImageRecord_LAMBDA.cs` is a `Prism.Core.Contracts` file and wasn't in the original spec's file list. The
   addition is disclosed in the note below and purely additive; flagged for the record, not as a defect.

**Implemented 2026-07-29.** New `FinalOutputSize`
(`jb/src/core/Services/Transform/FinalOutputSize.cs`, compiled into the `Prism.Services.Transform` Engine
assembly so `Tx_CenterAndStretch` can reach it; `Prism.Core` references that assembly, so `ImagePreProcessor`
can too). It owns four things: `HasEdgeIntersect`, `RoutesToCenterAndStretch` (the routing predicate, now
also used by `ImageTransformer.SelectTransformer` and `ApplyShadowAccounting` — one predicate, no copies),
`CenterAndStretchCanvasSize` (which `Tx_CenterAndStretch.CropResizeAndStretch` now calls instead of holding
its own copy of the formula), and the forward/inverse pair `LongestDimension` / `MinimalScaleToReach`.

The inverse is not solved algebraically: it takes the continuous inverse of the canvas formula — provably
never above the answer, since floor/even/trim only ever shrink the canvas — and steps up against the forward
function until the bar is cleared. Converges in ≤3 passes and cannot land a pixel short the way hand-derived
algebra can.

**Scope grew past "no behavior change yet"** because two of the three defects listed on [[T-4900]] sit inside
this ticket's remit: geometry promotion had to move ahead of upscale (new `ImageTransformer.FinalizeGeometry`,
called from `PreprocessAsync`; `TransformSeed.Resolve` moved above the preprocess call in `TransformService`;
promotion result now recorded on `ImageRecord_LAMBDA.SubjectGeometryPromoted` so the evidence line survives
the move), and `Tx_CenterAndStretch` had to be made to read the shared helper for the "single source of
truth" acceptance to mean anything.

**Acceptance met.** `FinalOutputSizeTests` (15 assertions across 9 facts — count corrected by the review;
13 pin literal pixel counts, 2 re-derive, see finding 2 above): the 1800→1948 worked example, the bleed case (`min(W,H)`, no margin
term), the 740/739 boundary from both sides, minimality at 741 (no scale) vs 739 (scale), and the routing
predicate's three cases. Transform suite 83/83.

Original spec follows.

Extract a single deterministic function that, given the salient bbox + intersection state + margin, returns
the **exact** final-output longest dimension the pipeline will produce — reusing each routing's own
canvas-size formula so upscale and the Transform stage never disagree. Two branches (user decision 4):
**zero-intersection** → `Tx_CenterAndStretch` canvas geometry: `canvasSize = (floor(bbox_longest·(1+2·margin))`
`made even) − 2`; **bleed/intersection** → the bleed routing's output longest dim, **no margin term**. The
routing split (zero-intersection vs bleed) must use the *same* predicate as `ImageTransformer.SelectTransformer`
so the calc matches the routing that will actually run. Both the upscale-scale logic (T-4920) and, ideally,
the Tx stage reference this one helper. Cross-stage note: the calc lives where upscale runs
(`ImagePreProcessor`, preprocess) but encodes Transform-stage geometry — keep it a pure function of
(bbox, intersection, margin, routing-config) with no side effects. Acceptance: unit tests pin exact sizes
against `Tx_CenterAndStretch`'s worked example (bbox 1800, margin 0.042 → canvas 1948) and a bleed case;
helper is the single source of truth. No behavior change yet.

**Files:** `jb/src/core/Services/Matching/ImagePreProcessor.cs` (or a new shared geometry helper class),
`jb/src/core/Services/Transform/Engine/Tx_CenterAndStretch.cs`,
`jb/src/core/Services/Transform/ImageTransformer.cs`.

---

### T-4920 · Unified upscale-scale + ESRGAN/Lanczos gate + KO
**Status:** Done (2026-07-30) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-30)
**Found by:** [[T-4900]]

**Reviewer verdict (2026-07-30): Approve.** The three numeric claims below were re-derived by hand rather
than read and accepted, and all three hold exactly:
- **Geometry-follows-pixels.** Because `box.Width <= origW` and rounding is monotonic,
  `w = round(box.Width·scale) <= round(origW·scale) = scaledW` always — so `scaledW − w >= 0`, the origin
  clamp can never go negative, and `x + w <= scaledW` holds even away from the touching-edge case. Traced
  on a non-edge example with a non-clean scale factor.
- **The 740/739 boundary.** `CenterAndStretchCanvasSize(740, 0.042)` = floor(802.16)=802 → even 802 → −2 =
  **800**; at 739 = floor(801.076)=801 → even 800 → −2 = **798**.
- **The bleed-route KO window is exact, not approximate.** `800/601 = 1.331` KOs, `800/602 = 1.329` does
  not — "shorter side under 602px" is the precise statement.

`Tx_CropSquare` confirmed to decode `ProcessedBytes`, crop against the decoded image's own dimensions, and
record the *same* rectangle object it passed to `Mutate` — so the manifest and the bytes cannot drift. The
OFF→zero-ESRGAN-calls assertion is real: `RecordingUpscaleService` is injected in the OFF test too and the
toggle branch is taken before `remoteUpscale` is ever touched, so a wrong branch would flip the call count.
Core 154/154 and Transform 83/83, both foreground. (Core is 154, not the 153 claimed below — commit
`5e06f54` added one test after this work; not a regression, just a stale count.)

**One finding, fixed 2026-07-30.** `MaxLanczosOnlyUpScaleFactor` was declared `{ get; private set; }` to
match its ~40 legacy siblings, but CLAUDE.md's no-shadow-defaults rule binds *new or touched* config code
regardless of the surrounding class. A missing key already failed loud via `RequireDouble`, so this was
never a live silent-default risk — but the compiler wasn't enforcing it. Now `required … { get; init; }`.
Note `private set` **cannot** carry `required` (CS9032: the setter would be less visible than the type), so
this is an `init` accessor, not a one-word change; it works because `ParseAndValidate` is the class's only
construction site and already uses an object initializer. Solution builds clean, Core 154/154.

**Non-blocking, and dormant rather than live:** `Tx_ProblemImageProcessor` (untouched here) computes its
`OutputWidth`/`OutputHeight` metadata from `InputImage.Width`/`Height` — the deliberately-unscaled
original-resolution field — while its actual resize reads real decoded dimensions. It cannot bite today:
that route is only selected when `BoundingBox` is null, and a null bbox short-circuits `UpscaleAsync`
before any scaling happens. It becomes reachable if `BypassPhenotypes` is flipped, so it belongs with the
same flip checklist as [[T-4910]]'s routing-predicate gap.

**Also unclosed, pre-existing:** no test exercises the new `MaxLanczosOnly > MaxUpScale` invariant or a
missing-key load failure, because there is no `PrismConfiguration` test file anywhere in the repo. Not a
hole this work opened.

**Implemented 2026-07-29.** `UpscaleAsync` rewritten to the unified model:
minimal scale from `FinalOutputSize.MinimalScaleToReach(MinOutputWidth, …)`, then the toggle picks resampler
and cap only — ESRGAN (local session or the remote host) to `MaxUpScaleFactor`, local Lanczos4 to the new
`MaxLanczosOnlyUpScaleFactor`. Past the applicable cap → `PREPROCESS_UPSCALE_EXCEEDED`, and the OFF message
appends "Enable ESRGAN upscaling to process this image." The too-small KO is retained and now measures the
promoted box. New config key `Output.Images.Resize.MAXIMUM_UpScale_LanczosOnly` = 1.33, `RequireDouble` +
`AssertPositive` + a new invariant that it may not exceed `MAXIMUM_UpScale`.

**Also here (T-4900 defects 1 and 2):** `ScaleGeometryToUpscaledImage` moves `BoundingBox` and
`LegacySalientBox` into the enlarged space and the BGR `Mat` handed downstream is re-decoded from the new
bytes; width and height are scaled first and never clamped so the longest side lands on exactly the pixel
count the scale was derived from, with the origin absorbing the ≤1px rounding overhang. `Tx_CropSquare` now
writes its cropped bytes and crops against the decoded image's own dimensions. Deliberately not scaled:
`ImageRecord_Base.Width`/`Height` (the original-resolution contract Export's upscale-manifest todo depends
on) and `lambda.Subject` (pre-upscale evidence, self-consistent with its own mask).

**Two consequences worth knowing before tuning any of these numbers:**
- **740, not 800, is the pass-through threshold** on the centre-and-stretch route. Images with a 740–800px
  bbox used to be upscaled and now are not — that is the "reduces ESRGAN work" effect, and it is why a
  re-run's KO/upscale counts will not match older evidence.
- **The Lanczos-only cap is unreachable on the centre-and-stretch route at current config values.** A bbox at
  the 570px input floor needs 740/570 = 1.30×, already inside 1.33×. The OFF-mode KO can only fire on the
  bleed route, for images whose *shorter side* is under 602px. This falls out of the numbers; it is not a
  designed guarantee, and changing `MinInputSizeInPixels`, `MinOutputWidth`, `WhiteSpaceMargin` or either cap
  changes it. Documented in `PRISM-transform-generate.md` and asserted by the test comments.

**Acceptance met.** `UpscaleGateTests` (8 facts, `jb/src/tests/Prism.Core.Tests/Services/`) — no-upscale when
already clear, OFF→Lanczos locally with zero calls to the ESRGAN service, ON→ESRGAN service reached, OFF cap
KO with the toggle named, ON processing the same image, ON past 1.42 KO'ing without the remedy sentence,
too-small KO retained, and geometry-follows-pixels measured against the returned image rather than against
the computed scale. Geometry is pinned by putting an exact `SubjectDetection` on the record rather than by
crafting pixels the detector has to rediscover, so the tests are deterministic and GPU-free. `RemoteUpscale
RoutingTests` updated to the new bar and now also asserts the final size clears it. Core 153/153 (incl. 10
CiMini pipeline-integration), Transform 83/83, Matching 230/230, Upscale 17/17, Generate 10/10.

Original spec follows.

Rewrite `ImagePreProcessor.UpscaleAsync` to the unified model. Using T-4910's exact final-size calc,
compute the **minimal** scale `s ≥ 1.0` such that the computed final output ≥ `MinOutputWidth` (as little as
possible to cross the bar). Then branch on the toggle: **ON** → ESRGAN (dynamic model), cap `s ≤`
`MaxUpScaleFactor` (existing, 1.42); **OFF (default)** → Lanczos, cap `s ≤ MaxLanczosOnlyUpScaleFactor`
(new config, 1.33). If the required `s` exceeds the applicable cap → **KO** (reuse `PREPROCESS_UPSCALE_EXCEEDED`;
OFF message names the toggle: "enable ESRGAN upscaling to process this image"). Retain the existing
too-small KO (`largest < MinInputSizeInPixels`). Add the new config key following no-shadow-defaults
(`required`, no in-code default). Note the current ON path targets the *bbox* reaching `MinOutputWidth`;
unifying moves it to the *final-image* bar (margin-aware for zero-intersection), which reduces ESRGAN work.
Acceptance: unit tests for OFF (Lanczos, +33% cap, KO past it, margin discount on zero-intersection, direct
on bleed), ON (ESRGAN, 1.42 cap), and the minimal-scale property; the Lanczos path uses the same resampler
family as the existing top-up. Lanczos-only default keeps a full run's upscale cost near-zero.

**Files:** `jb/src/core/Services/Matching/ImagePreProcessor.cs`,
`jb/src/core/config/Prism_Config.json`,
`jb/src/core/config/` (new `MaxLanczosOnlyUpScaleFactor` binding + its config class),
`jb/src/tests/Prism.Services.Matching.Tests/` (or the suite owning ImagePreProcessor).

---

### T-4930 · ESRGAN toggle plumbing (per-job parameter, default OFF)
**Status:** Done (2026-07-29) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-29)
**Found by:** [[T-4900]]

**Reviewer verdict (2026-07-29): Approve.** Default-off was traced end to end and the casing lines up
(workbench sends camelCase, `PrismProcessIngressReader` reads with `PropertyNameCaseInsensitive = true`).
The distributed path was verified concretely rather than assumed: `HttpTransformService` POSTs the whole
`MatchingResult` via `ServiceHttp.Json` (`PropertyNamingPolicy = null`) and the ServiceHost's
`ConfigureHttpJsonOptions` sets the same, so the two ends agree. The get-only-collection trap from the
microservices split does **not** apply — `IngestResult.Parameters` is a required scalar record, not a
collection.

The reviewer went further than accepting the deviation as defensible and checked whether `transformEnabled`
/`headcut` are ever supplied independently of `matched.Ingest.Parameters` at any call site — if they were,
the asymmetry would be a real risk. They are not: `PrismService` and the ServiceHost route both derive those
"explicit arguments" from the same object one frame up, so reading `AllowEsrganUpscale` a frame deeper is
bit-identical behaviour with no signature churn.

**One finding, fixed 2026-07-29.** `ProcessingParametersRoundTripTests` claimed to use "the same web defaults
the ServiceHost routes use", but `JsonSerializerDefaults.Web` is camelCase whereas the ServiceHost overrides
the naming policy to null. Self-consistent, so it passed either way — it just wasn't a proxy for anything
real. Rewritten to exercise the actual configurations: serialize with the real `ServiceHttp.Json` object,
deserialize with the ServiceHost's, pin the literal PascalCase wire text, and assert omitted-means-false
under both that and the API ingress reader's options. 4/4 green.

**Known gap, accepted:** the `PrismProcessRequest` → `PrismProcessingParameters` mapping is untested (no
`Prism.Api` test project, internal record). The reviewer confirmed this is pre-existing — `Rename`,
`Transform`, `Generation`, `Format`, `ReturnOriginalImages` and `SkipClassification` all share it for the
same reason — so it is not debt this work introduced. Mapping verified by hand.

**Implemented 2026-07-29.** `PrismProcessingParameters.AllowEsrganUpscale`
(no initializer, so an omitted field is false), `PrismProcessRequest.AllowEsrganUpscale`, mapped in
`PrismProcessIngressReader`, read once in `TransformService` and passed to `PreprocessAsync`.

**Deviation from the spec, deliberate:** the flag is read off `matched.Ingest.Parameters` inside
`TransformService` rather than threaded as a method argument like `headcut`. The parameters already ride
inside `MatchingResult` across the matching→transform HTTP boundary — the ServiceHost route reads `Transform`
and `Headcut` exactly this way — so one read cannot be dropped at a call site, and the alternative was
signature churn across `ITransformService`, `Pipeline`, `PrismService`, the ServiceHost route and the HTTP
client for a boolean already on the record.

`PreprocessAsync` has only two call sites (`TransformService` and `RemoteUpscaleRoutingTests`); the parameter
is required, not defaulted, so a new call site cannot silently inherit the wrong mode. Match-stage usage
checked: there is none.

**Acceptance met** except one item that has no home: `ProcessingParametersRoundTripTests` covers the
service-boundary round-trip under `JsonSerializerDefaults.Web`, omitted-means-false, and explicit-true. The
get-only-dict trap does not apply — these are `bool { get; init; }`. **Not covered:** the
`PrismProcessRequest` → `PrismProcessingParameters` mapping itself, because there is no `Prism.Api` test
project and the request record is `internal`. Follow-up ticket territory, not a defect in this work.

Original spec follows.

Add a per-job boolean (proposed `AllowEsrganUpscale`, **default false**) to `PrismProcessingParameters`,
accept it on the `POST /PRISM/process` multipart request, and thread it through `TransformService` →
`ImagePreProcessor.PreprocessAsync`/`UpscaleAsync` so the T-4920 gate can read it. Confirm every call site
of `PreprocessAsync` (at least `TransformService`; verify Match-stage usage) receives it. Default-off means
an omitted field yields Lanczos-only. Acceptance: request round-trips the flag; default-off verified when
absent; a job with the flag on routes to ESRGAN; service-boundary round-trip test (mind the get-only-dict
trap from the microservices split — `[JsonConstructor]` if needed). Scope: plumbing only; the OFF/ON
behavior is T-4920.

**Files:** `jb/src/core/Models/PrismProcessingParameters.cs` (or wherever job params live),
`jb/src/api/` (process endpoint), `jb/src/core/Services/Transform/TransformService.cs`,
`jb/src/core/Services/Matching/ImagePreProcessor.cs`.

---

### T-4940 · Workbench UI toggle for ESRGAN upscaling
**Status:** Done (2026-07-29) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-29)
**Found by:** [[T-4900]]

**Reviewer verdict (2026-07-29): Approve, no defects.** Unchecked-by-default confirmed by tracing
`defaultParameters` state into the checkbox's `checked` prop, not just by reading the literal. Renders
through the shared `binaryParameterFields` map rather than a parallel control; the TS field is required, not
optional, so there is no silent-undefined path; both request builders are wired (the match-lite builder's
hardcoded `false` sits alongside its other disabled options, so it is consistent rather than an oversight).
`npm run typecheck` and `dotnet build` clean. No web test framework exists in this repo, so there is no
component-test gap introduced — matches the ticket's own acceptance bar and prior workbench precedent.

**Implemented 2026-07-29.** Added as a fifth entry in
`JobParameterPanel`'s `binaryParameterFields` ("High-quality upscaling (ESRGAN — slower)",
`request.allowEsrganUpscale`), so it renders through the same checkbox path as the existing four rather than
introducing a parallel control. `allowEsrganUpscale` added to the `PrismProcessingParameters` TS interface,
to `defaultParameters` in `WorkbenchShell` as `false`, and to both request builders in `prismApiClient`
(the match-lite builder hardcodes `false` alongside its other disabled options). `npm run typecheck` and
`npm run build` both green.

Note: Headcut is on `PrismProcessingParameters` server-side but is not on `PrismProcessRequest` and has no UI
control — it can't be set by any caller today. Out of scope here; worth its own ticket.

Original spec follows.

Surface the toggle in the Next.js workbench (`jb/src/workbench/web`) as an unchecked-by-default checkbox
(e.g. "High-quality upscaling (ESRGAN — slower)"), wired to the T-4930 request field. Match existing
process-option controls (Transform/Headcut). Acceptance: unchecked by default; submitting checked sends the
flag on; `npm run typecheck` + `npm run build` green. Scope: UI + request wiring only.

**Files:** `jb/src/workbench/web/` (process-options component + API client).

---

### T-4805 · Unify Transform/Process entry points (fix latent divergence)
**Status:** Done (2026-07-28) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-28)
**Found by:** [[T-4800]]

**Reviewed 2026-07-28:** genuinely one shared core (`CropResizeAndStretch`), not a duplicated patch;
all four Tx classes independently re-read rather than trusted from the jbtodo claim (`Tx_DetailCropper`
already honoured the lambda; `Tx_CropSquare`/`Tx_ProblemImageProcessor` never read a bbox at all, so the
divergence cannot occur in them); dead `Enhance` gone with zero remaining references, standalone CLAHE
`Process` utility retained and unaffected. One non-blocking note: no failure-path test for a null
`BoundingBox` reaching `Process` — follow-up material, not a merge blocker.

`Tx_CenterAndStretch.Process` (and the other Tx `Process` methods, per the "precedent" comment in
`Tx_DetailCropper`) ignore the `lambda` parameter and always crop to `FullImageBounds(arr)`, violating
the `IImageTransformation` contract (reuse the lambda's BoundingBox when provided). Not live today — the
deployed transform service routes through `Transform(lambda)` — but a future per-image webservice on
`Process` would diverge from pipeline behavior and ignore the persisted SubjectBox from T-4810.
Acceptance: `Transform(lambda)` and `Process(...,lambda)` funnel through one shared core so identical
geometry → identical output; `Process` reuses the lambda's box when present; all four Tx classes audited;
dead `Tx_LowContrastEnhancement.Enhance` removed (CLAHE moves upstream via T-4830), standalone
`Tx_LowContrastEnhancement.Process` utility retained; build + tests green. Scope: no new transform
behavior — pure de-duplication of the two paths.

**Files:** `jb/src/core/Services/Transform/Engine/IImageTransformation.cs`,
`jb/src/core/Services/Transform/Engine/Tx_*.cs`,
`jb/src/core/Services/Transform/Engine/Utils/Tx_LowContrastEnhancement.cs`.

---
### T-4810 · Persisted subject mask/box contract
**Status:** Done (2026-07-28) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-28)
**Found by:** [[T-4800]]

**Reviewed 2026-07-28:** contract, `ISubjectDetector` seam, and round-trip test all sound. The seam is
genuinely swappable — Transform consumes `SubjectDetection` generically, never `SubjectDetector`-specific
state, so a SAM3/yolo-seg producer needs no Transform-side change. Round-trip test exercises the real
risk (a non-empty `MaskPng` byte array across the boundary); the historical get-only-dictionary trap does
not apply here as neither type has a get-only dictionary property. **Answered planner question:** the
reviewer asked whether the no-shadow-defaults rule extends from config classes to runtime data contracts,
since `SubjectDetection.Producer` carries `= string.Empty`. It does not — the rule is about config that
loads from JSON and must fail loud on a missing key. `SubjectDetection` is a data-carrying contract and
follows sibling convention (`ImageRecord_LAMBDA`, `BoundingBox`), which is what landed. No change needed;
the ticket's "following the no-shadow-defaults rule" phrasing was loose.

Add a persisted `SubjectMask` + `SubjectBox` (+ per-edge intersect flags) to the image record, produced
upstream and read by Transform. Define the pluggable-producer seam so a segmentation producer
(SAM3 / yolo26s-seg, [[T-2600]]) can replace the v1 classical-CV producer later without touching
Transform. Acceptance: contract types added following the no-shadow-defaults rule; producer interface
defined; round-trips across the service HTTP boundary (get-only dict trap — mirror the microservices-split
`[JsonConstructor]` + round-trip test); no behavior change until a producer populates it. Scope: contract
+ plumbing only, not the detector (T-4830).

**Files:** `jb/src/core/Models/ImageRecord_LAMBDA.cs`, `jb/src/core/Models/ImageRecord_Base.cs`,
`jb/src/core/Services/Matching/ImagePreProcessor.cs`.

---
### T-4820 · Seeding access in Transform
**Status:** Done (2026-07-28) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-28)
**Found by:** [[T-4800]]

**Reviewed 2026-07-28:** `TransformSeed` is genuinely data-access-only — every signal read from the
already-populated feature snapshot, nothing recomputed. A missing `FamilyIDRecord` is modelled as a
first-class absent case (unmatched image, or a job with no Excel), not a defensive null-coalesce and not
a throw. `background-type` compares against `SOLIDCOLOR` only, matching the current T-4700 taxonomy.
Non-blocking naming note: the ticket named `product-type-label` but the seed surfaces `ProductTypeId`
(the resolved Excel-authoritative slug) — the better signal, and consistent with "product = Excel + CLIP".

Thread the already-measured features `product-color`, `background-type`, `background-color`,
`product-type-label` to Transform, and give each lambda access to its `FamilyIDRecord` (today only the
`Family` id string + `ProductTypeId` reach the record). `background-type` is already settled by T-4700
(`SOLIDCOLOR`/`REALLIFE`/`UNKNOWN`) — "flat" = `SOLIDCOLOR`, no reconciliation. (Product-type ids are
being collapsed to 5 by [[T-4710]]; seeding is slug-agnostic.) Acceptance: the four signals +
FamilyIDRecord reachable inside Transform without recomputation; no seeding logic yet (that is T-4860).
Scope: data access only.

**Files:** `jb/src/core/Services/Transform/TransformService.cs`,
`jb/src/core/Services/Matching/Classify/ImageFeatureSnapshot.cs`, `jb/src/core/lib/Excel/FamilyIDRecord.cs`,
`jb/src/core/Models/ImageRecord_LAMBDA.cs`.

---
### T-4830 · Port the v1 subject detector (+ ingress alpha path)
**Status:** Done (2026-07-28) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-28) — second pass, after fixes
**Found by:** [[T-4800]]

**Second-pass verdict (2026-07-28):** Approve. The `Marshal.Copy` fix is correct (a freshly allocated Mat
is continuous, the byte count matches, and it mirrors the existing idiom in `ImagePreProcessor`), the
three missing test scenarios are present and substantive, and the SPACINI29 delta was genuinely run —
the reviewer corroborated the +18.3s/+11.7% claim against the literal `matching-testlogs.txt` entry rather
than taking it on trust. Ingress alpha ordering verified as `AutoOrient` → capture → white composite.

**First pass (2026-07-28):** the C# port is algorithmically faithful — lightness verified as never a
threshold criterion, background genuinely fitted as a least-squares plane (same 500-sample cutoff as the
reference), Canny confirmed corroboration-only. Mat disposal is well managed; the one finding
(`CanvasContacts`'s four undisposed `Mat.Row`/`Mat.Col` views) is refcounted views over one buffer, so a
one-line fix rather than a real leak. **Blocking:** three of the four mandated test scenarios were
missing — white-on-white/texture-only, cast-shadow exclusion (the algorithm's single defining invariant),
and gradient background (every test used a uniform backdrop, so the plane-fit coefficients were trivially
zero and the plane fit was never exercised). Also the mandated SPACINI29 perf/quality delta was never
run, which matters because `MaxAnalysisSize` was set to 1024 against the reference's 2400 with an explicit
reference-author warning that fabric weave disappears when this is low. Both being closed this session.

Port the vendored `jb/docs/reference/process_images.py` detector to C#/OpenCvSharp4 in the upstream
producer, one named helper per step (recipe-readable, K&R). Populate `SubjectMask`/`SubjectBox`/intersects/
candidate-shadow evidence. Chroma-plane + texture + shadow-strip-by-shape + Canny corroboration; lightness
never a criterion (shadow exclusion). Add the ingress alpha path: real alpha → build+persist box/mask
before jpg normalization, skip the heuristic path. New detector config follows no-shadow-defaults.
Acceptance: producer populates the contract; unit tests on white-on-white, cast-shadow, gradient
background, bleed-off cases; classify-stage perf delta measured on SPACINI29 vs the 156.5s baseline.

**Files:** `jb/src/core/Services/Matching/ImagePreProcessor.cs` (+ new detector class/config under
Classify), `jb/src/core/lib/Ingress/Importer.cs` (ingress alpha capture, pre-normalization),
`jb/src/core/config/analyzer_Config.json` or `ClassifyConfig.json`.

---
### T-4850 · Consume subject mask/box in Transform
**Status:** Done (2026-07-28) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-28) — second pass, after fixes
**Found by:** [[T-4800]]

**Second-pass verdict (2026-07-28):** Approve. Both blocking findings are genuinely fixed, each with a
positive and a negative test. Confirmed on real data: of 86 SPACINI29 images, 71 promote and the 15 that
do not are exactly those below the 0.35 confidence floor — the gate does real work rather than being
decoration.

**First pass (2026-07-28):** routing correctly sees the detector's signals (promotion runs before
`SelectTransformer`, writes the same feature keys in the same format), and the fill tier is untouched as
required. **Blocking:** (1) `PreferSubjectGeometry` claims in both its comment and the design doc to
promote a *confident* subject but never reads `Subject.Confidence` — it gates only on the whole-frame
flag, so a 0.1-confidence sparse-blob detection overrides the legacy bbox unconditionally, including
where a null legacy bbox previously routed safely to `Tx_ProblemImageProcessor`. Now gated on a
config-driven floor. (2) Promotion overwrote the legacy salient bbox with no copy retained, making this
ticket's own A/B acceptance bar unverifiable from a run's evidence — the pre-promotion box is now kept on
the record and emitted as evidence. Non-blocking follow-up: `intersection-count`/`fully-in-frame`/
`occlusion-level` still reflect the old heuristic after promotion (harmless today, nothing in Transform
reads them, but a trap for later phenotype-driven routing).

Center/stretch/detail-crop geometry operates on the real SubjectMask/SubjectBox instead of the salient
rectangle; routing (`ImageTransformer.SelectTransformer`) uses the detector's cleaner intersect signals.
Fill stays the existing `Tx_util_BgStretch` (unchanged), just fed the better geometry. Acceptance: routing
+ geometry read the persisted mask/box; A/B vs the current salient box shows equal-or-better centering on
the test set; no fill-tier changes.

**Files:** `jb/src/core/Services/Transform/ImageTransformer.cs`,
`jb/src/core/Services/Transform/Engine/Tx_CenterAndStretch.cs`,
`jb/src/core/Services/Transform/Engine/Tx_DetailCropper.cs`.

---
### T-4860 · Behavior toggles + shadow wiring
**Status:** Done (2026-07-28) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-28) — second pass, after fixes
**Found by:** [[T-4800]]

**Second-pass verdict (2026-07-28):** Approve. Both blocking findings fixed with tests, and toggles (a)/(b)
implemented per the settled design. Real-data outcome: toggle (a) fires on 19/86, the shadow toggle on
23/86 after calibration. Toggle (b) fires on 0/86 because SPACINI29 is entirely `SOLIDCOLOR` — the
`HeroDetectionOnSteroids` path therefore has no real-data coverage yet, tracked in [[T-4945]].

**First pass (2026-07-28):** the shadow shrink math is correct and its fraction is `required`/validated with
no in-code default. **Blocking:** (1) `background-type = UNKNOWN` was normalised to null and therefore
read as *flat*, identical to a known `SOLIDCOLOR` — inverting the spec, since UNKNOWN is precisely not
SOLIDCOLOR. (2) The shrink was applied unconditionally before routing, so it also perturbed
`Tx_CropSquare`/`Tx_DetailCropper`/`Tx_ProblemImageProcessor` inputs, where the ticket scopes it to
`Tx_CenterAndStretch`. Both fixed with tests. Note on toggle (a): the colour comparison is exact
string equality on categorical palette names, because that is the only product/background colour data
that reaches Transform — coarse (misses "ivory" vs "white") but the best available from today's signals.

**Seeding behaviour settled (user, 2026-07-28)** — see `Services/Transform/Engine/jbtodo.md`:
- **(a) product-color ≈ background-color → this is where CLAHE belongs.** When the product colour is
  clearly distinct from the background, CLAHE is superfluous and is skipped; it earns its cost only when
  the two nearly match and the weave has to be lifted clear of the noise floor.
- **(b) background not flat → a second discrimination step decides the treatment.** B1 = soft gradients
  plus minor noise/dust (a photo-studio sweep) gets one treatment; B2 = a real-life background triggers
  `HeroDetectionOnSteroids` — the documented everything-we-have escalation path (prior evidence, yolo26n,
  saliency, whatever helps) for accurate hero detection. Deliberately not built out fully now; the method
  exists and is named so the escalation path is explicit rather than implied.

Implement the three seeding toggles: (a) product-color ≈ background-color → harder isolation;
(b) background-type not `SOLIDCOLOR` → more hero-detection effort (skip when `SOLIDCOLOR`, for speed);
(c) detector candidate-shadow evidence → shadow-accounting, driving the existing `Tx_CenterAndStretch`
shrink (`shadow-present` was removed by T-4700, so read the detector evidence off the record directly).
Optionally re-declare a detector-measured `shadow-present` feature via `HowToAddAPhenotype.md`. All
thresholds config-driven (no shadow defaults). Acceptance: each toggle unit-tested on a positive and a
negative case; evidence-harness run confirms real behavior, not just green tests.

**Files:** `jb/src/core/Services/Transform/ImageTransformer.cs`,
`jb/src/core/Services/Transform/Engine/Tx_CenterAndStretch.cs`, `jb/src/core/config/transform_Config.json`,
`jb/src/core/Services/Transform/Admin/TransformParameters.cs`.

---
### T-4870 · Populate the transform-evidence carrier (detection/toggle evidence)
**Status:** Done (2026-07-28) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-28) — second pass, against the re-scoped acceptance below
**Found by:** [[T-4800]]

**Re-scoped (user, 2026-07-28).** Originally worded as "the transform manifest carries the new evidence",
which the reviewer correctly failed: `transform-manifest.json` does not exist anywhere in the codebase,
and `Exporter.cs`/`Prism_Config.json` (both named in this ticket's own file list) were untouched. What
landed is the *carrier* — detection/mask/box/signal/toggle evidence written into
`OutputRecord.SafeSummaryText`, which is exactly the approach Export Todo 4 settled on, with the pixel
mask correctly kept out of the text field. Rather than absorb Todo 4's file-emission work (a `Manifests`
config section, the `manifest.json` → `prism-manifest.json` rename, and all seven `Tx_*` classes writing
their full runtime parameter sets) into this epic, the ticket is re-scoped to what it actually is.

**Acceptance (revised):** `OutputRecord.SafeSummaryText` carries the detection evidence (producer, box,
confidence, per-edge intersects, hard-shadow flag, whole-frame flag), the three toggle states, and — added
this session — the pre-promotion legacy salient box plus whether promotion fired, in a stable parseable
`key=value;` encoding. No parallel evidence store. Emission of `transform-manifest.json` itself stays with
Export Todo 4, which owns the `Manifests` config section and the per-Tx parameter capture; this ticket
leaves it a pure serialization job with the data already in place.

**Files:** `jb/src/core/Services/Transform/ImageTransformer.cs`, `jb/src/core/Models/ImageRecord_OUTPUT.cs`,
`jb/src/core/lib/Export/jbtodo.md` (Todo 4 note that the carrier is now populated).

**Files:** `jb/src/core/Services/Transform/Engine/Tx_*.cs`, `jb/src/core/Models/ImageRecord_OUTPUT.cs`,
`jb/src/core/lib/Export/Exporter.cs`, `jb/src/core/config/Prism_Config.json`.

---
### T-4800 · Model-aware subject isolation for Transform (epic)
**Status:** Done (2026-07-28) | **Profile:** P0-orchestrator
**Found by:** [[T-4700]] follow-up; folds in the removed root note `TRANSFORM-SUBJECT-ISOLATION-NOTE.md`

Tracking ticket. Design lives in `jb/src/core/Services/Transform/Engine/jbtodo.md` ("Subject Isolation &
Model-Aware Transformation"). Goal: give Transform a real subject mask/box (shadow- and
background-excluded) produced upstream and consumed as pure geometry+fill, plus Excel+CLIP seeding that
steers transform behavior. v1 ports the vendored classical-CV prototype
`jb/docs/reference/process_images.py`; ONNX stays upstream (Transform stays deterministic). Children:
T-4805, T-4810, T-4820 (Wave 0); T-4830 (Wave 1); T-4850, T-4860 (Wave 2); T-4870 (Wave 3). T-4840
(vendor the reference script) is already done. This ticket is an index, not a unit of work.

**Board sync (2026-07-28, second pass):** all seven children reviewed. **Approve:** T-4805, T-4810,
T-4820. **Request Changes:** T-4830 (missing test scenarios + unrun SPACINI29 delta), T-4850 (confidence
gate claimed but not implemented; legacy bbox destroyed), T-4860 (UNKNOWN background read as flat; shadow
shrink leaked into unscoped routes). T-4870 re-scoped to the evidence *carrier*, with manifest emission
left to Export Todo 4 where it belongs. Fixes for all four are in progress this session.

**Both deferrals pulled into scope (user, 2026-07-28)** — they are no longer deferred:
- **Ingress alpha capture** — build it: capture the alpha-derived box/mask before
  `Importer.LoadImageWithExifOrientation` flattens transparency onto white, and prefer it over the
  heuristic producer.
- **Seed-aware detection** — the stated blocker ("seed resolves after preprocessing") turned out to be an
  ordering accident, not a constraint: `TransformSeed.Resolve` sits seven lines below the
  `PreprocessAsync` call in the same method. The real constraint was different and is settled below.

**Completion state (2026-07-28).** All blocking findings closed; both pulled-in deferrals built; CI gate
restored. Full suite **469 green** (Core 142, Matching 226, Transform 74, Upscale 17, Generate 10) — was
425 at the start of the pass. Release build with `-warnaserror:SA1402,SA1649,S109,SA1101` is at 0 errors.
SPACINI29 runs 86/86 OK. **Remaining gate before any child can go Done: a reviewer Approve on *this*
session's changes** (the stage move, seeding, alpha capture, and the four fixes) — the recorded Approve
verdicts cover only the original commit. That review was not run because the subagent budget was
exhausted mid-session.

**Detector stage move (user decision, 2026-07-28).** `SubjectDetector` moves out of
`ImagePreProcessor.PreprocessAsync` (Transform stage) into `ImageFeatureAnalyzer.Refine` wave 3, directly
before `FinalizePhenotype`. That is the only point in the pipeline where every precondition holds at once:
the FamilyIDRecord is resolved (Excel seed available), `Analyzer_ProductColor`/`Analyzer_BackgroundColor`
have just run two lines earlier (the toggle-(a) seed), the image is already decoded and shared across the
analyzer chain (no second decode from disk), and the phenotype has not yet been assigned — which is what
makes a detector-measured `shadow-present` a *usable* feature instead of one that is always UNKNOWN when
the rules evaluate. Transform then only reads `lambda.Subject` and detects nothing. This also settles the
jbtodo's open shadow-present sub-decision: **re-declare it as a real ImageNGP feature** (user choice).

**Files:** `jb/src/core/Services/Transform/Engine/jbtodo.md`, `AGENT-TICKETS.md`.

---

### T-4710 · Collapse DetOrderRules/ProductTypeMap to 5 product types; expose WinningPhenotype
**Status:** Done (2026-07-27) | **Profile:** P1-feature-worker
**Found by:** [[T-4700]] — direct follow-up, same "subtract, then get a reliable catch-all
working" effort.
**Review:** Approve (2026-07-27) — verified `topwear`'s synonym list is byte-identical to the
old `clothing-tops`, `bottomwear`'s list is the clean union of `clothing-bottoms`+
`clothing-dresses` with no cross-group term collisions, and all 13 retired groups' raw terms are
fully gone (not just renamed). `DetOrderRules.json` diffed against git history: `topwear`/
`bottomwear` tables are byte-for-byte the old `clothing-tops`/`clothing-bottoms` content under
new keys, confirming the user's tie-break choices landed correctly. `ImageTransformer`'s
`IsDetailCropperDetSlotExcluded` fix re-derived as correct and confirmed still dead code (gated
behind `BypassPhenotypes=true`, same limitation the existing test file already documents — no
coverage was lost). `WinningPhenotype` export gated identically to `DetOrder`, both new
`ExporterTests.cs` cases non-vacuous (positive + KO-null). Build 0 errors, full suite 417/417.
Two non-blocking doc nits (a wrong ticket-number attribution, a stale `headphone`/
`electronics-small` example) fixed same session. Commit `fd894aa`.

`DetOrderRules.json`/`ProductTypeMap.json` had 19 product types (`default` + 18 bespoke ones),
none validated in production. Per user direction: subtract down to `default` + 4 categories that
are actually in scope right now (`topwear`, `bottomwear`, `footwear`, `bags-accessories`); the
other 13 (`clothing-outerwear`, `fmcg-*`, `beauty-cosmetics`, `electronics-*`, `homeware-*`,
`toys-children`, `diy-tools`, `gardening`, `sports-equipment`, `furniture`) fall back to
`default`. `clothing-tops`→`topwear` (unchanged synonym list); `clothing-bottoms`+
`clothing-dresses`→`bottomwear` (merged per explicit user tie-breaks: allow back/side-packshot
fallback at det1/det2, and rank `front-on-model-partial` ahead of `lifestyle-hero` at det4 —
both resolved in `clothing-bottoms`' favor, so the merged table is `clothing-bottoms`' content
verbatim under the new id). Also exposes `OrderEvidence.WinningPhenotype` (computed by
`ImageOrderer` but never surfaced) on the export manifest, so a downstream consumer can see
*why* an image landed in a given det slot instead of inferring it from position alone.

**What to do:** rename/merge `ProductTypeMap.json` groups and `DetOrderRules.json` tables per
above; fix `ImageTransformer.IsDetailCropperDetSlotExcluded`'s `StartsWith("clothing-")` check
to match the renamed ids (`topwear`/`bottomwear`) — note this method is currently unreachable
dead code while `BypassPhenotypes = true` gates the whole `DetailCropper` branch off, so the fix
is a correctness-for-later change, not something testable end-to-end today; add
`ManifestImageRow.WinningPhenotype`, wire it in `Exporter.ToManifestRow`; update
`ImageOrdererTests.cs`, `ProductTypeResolverTests.cs`, and `ExporterTests.cs` for the
renamed/removed ids and the new field.

**Acceptance:** `dotnet build jb/src/PRISM.sln` and `dotnet test jb/src/PRISM.sln` green;
`DetOrderConfig.Load` reports exactly 5 product types (`default`, `topwear`, `bottomwear`,
`footwear`, `bags-accessories`); no dangling `clothing-*`/retired-category id anywhere in
production code, tests, or `ProductTypeMap.json`/`DetOrderRules.json`.

**Files:** `jb/src/core/config/ProductTypeMap.json`, `jb/src/core/config/DetOrderRules.json`,
`jb/src/core/Services/Transform/ImageTransformer.cs`, `jb/src/core/lib/Export/ManifestImageRow.cs`,
`jb/src/core/lib/Export/Exporter.cs`, `jb/src/core/Models/ImageRecord_LAMBDA.cs`,
`jb/src/core/Services/Matching/Order/DetOrderConfig.cs`,
`jb/src/core/Services/Matching/Analyzers/Analyzer_ProductType.cs`,
`jb/src/core/Services/Matching/Analyzers/ProductTypeResolver.cs`,
`jb/src/core/Services/Matching/Analyzers/Analyzer_Interior.md`,
`jb/src/tests/Prism.Services.Matching.Tests/Order/ImageOrdererTests.cs`,
`jb/src/tests/Prism.Services.Matching.Tests/Analyzers/ProductTypeResolverTests.cs`,
`jb/src/tests/Prism.Core.Tests/Export/ExporterTests.cs`,
`jb/docs/ImageNGP/PRODUCTTYPES.MD` (flagged stale, not fully rewritten — see note in file),
`jb/docs/ideas-on-NGP.md`.

---

### T-4700 · Remove unimplemented analyzers; trim ImageNGP/ImageRoles/DetOrderRules to real+reachable only
**Status:** Done (2026-07-27) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-27) — verified deletion completeness (all 10 stub `.cs`/`.md` pairs
and their `Prism.Services.Matching.Classify.csproj` `Compile Include` entries gone), zero
dangling references to any of the 23 removed features or 6 removed phenotypes anywhere
(`ImageRoles.json`, `DetOrderRules.json`, `ClipPrompts.json`, `ImageFeatureAnalyzer.cs`, tests),
`ghost-front`'s dead clause removed without reordering (confirmed against
`PhenotypeRuleSetTests.cs`'s new overlap/reachability tests), and every `DetOrderRules.json` slot
that lost its only phenotypes became `[]` rather than being deleted (preserving overflow slot
numbering). Build 0 errors, full suite 415/415 (then 417/417 after T-4710). Two non-blocking doc
nits (a feature-count off-by-one, a doc example citing a just-deleted feature) fixed same session.
Commit `fe9ac38`.

`ImageNGP.json` declares 60 features and 26 phenotypes, but only 11 of 21 analyzer classes are
actually implemented — the other 10 (`Analyzer_FacePose`, `Analyzer_TextPresent`,
`Analyzer_Mannequin`, `Analyzer_LogoPresent`, `Analyzer_CameraAngle`, `Analyzer_IndoorOutdoor`,
`Analyzer_ShadowReflection`, `Analyzer_Packaging`, `Analyzer_MaterialTexture`,
`Analyzer_LightingDetail`) are empty-body stubs. Because `PhenotypeRuleSet` treats `UNKNOWN` as
never satisfying a required condition, every phenotype gated on a stub-only feature is
mathematically unreachable — 6 of 26 phenotypes are dead on arrival, cascading into 13 of 19
`DetOrderRules.json` product-type tables having an inert det-slot. First half of a user-directed
"simplify by subtraction, then re-expand piecemeal" effort (see [[T-4000]], [[T-2600]]); a
follow-up ticket collapses `DetOrderRules.json`/`ProductTypeMap.json` from 19 product types to 5.

**What to do:** delete the 10 stub `.cs`/`.md` pairs and their call sites in
`ImageFeatureAnalyzer.cs`; remove the 23 features they would have produced plus the
structurally-dead `background-type=STUDIO` enum value from `ImageNGP.json` (60→37 features);
remove the 6 now-unreachable phenotypes from `ImageNGP.json`/`ImageRoles.json` (26→20), dropping
`ghost-front`'s dead `contains-mannequin` clause without reordering; strip the 6 dead phenotype
ids from every `DetOrderRules.json` slot; update `Analyzers/jbtodo.md`, `Classify/jbtodo.md`,
`ImageFeatures.md`, `imagePhenotypes.md`, `PRISM-index.md`, and 3 Classify test files
accordingly; write a new `jb/docs/ImageNGP/HowToAddAPhenotype.md` reference doc covering the
full analyzer→feature→phenotype→det-order wiring chain with a worked hero-image example.

**Acceptance:** `dotnet build jb/src/PRISM.sln` and `dotnet test jb/src/PRISM.sln` green; startup
`ImageNgpValidator` passes (no dangling id references across `ImageNGP.json`/`ImageRoles.json`/
`DetOrderRules.json`/`ClipPrompts.json`); no behavior change for any image that previously
exercised a real (non-stub) code path — pure removal of unreachable paths.

**Files:** `jb/src/core/Services/Matching/Analyzers/*.cs`, `jb/src/core/Services/Matching/Analyzers/*.md`,
`jb/src/core/Services/Matching/Analyzers/jbtodo.md`, `jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`,
`jb/src/core/Services/Matching/Classify/jbtodo.md`, `jb/src/core/config/ImageNGP.json`,
`jb/src/core/config/ImageRoles.json`, `jb/src/core/config/DetOrderRules.json`,
`jb/docs/ImageNGP/ImageFeatures.md`, `jb/docs/ImageNGP/imagePhenotypes.md`,
`jb/docs/ImageNGP/HowToAddAPhenotype.md` (new), `jb/docs/PRISM-index.md`,
`jb/src/tests/Prism.Services.Matching.Tests/Classify/ImageFeatureAnalyzerTests.cs`,
`jb/src/tests/Prism.Services.Matching.Tests/Classify/PhenotypeRuleSetTests.cs`,
`jb/src/tests/Prism.Services.Matching.Tests/Classify/ImageFeatureSnapshotTests.cs`,
`AGENT-TICKETS.md`.

---

### T-4400 · Adopt Roslyn analyzers: SA1402/SA1649/SA1101/SA1633/S109 (S109 priority), suppress SA1500/SA1025/SA1503
**Status:** Done (2026-07-24) | **Profile:** P1-feature-worker
**Review (phase 1, 2026-07-20):** Approve. StyleCop.Analyzers/SonarAnalyzer.CSharp wired into every production project, curated root `.editorconfig`, `SonarLint.xml`, SA1402/SA1649 fixed to zero and CI-gated — verified internally consistent (no type compiled twice or dropped across the Prism.Core/Prism.Core.Contracts Include/Remove split), package versions confirmed real/current on nuget.org, SonarLint.xml schema confirmed correct for sonar-dotnet, CI `-warnaserror:SA1402,SA1649` confirmed to actually fail the build on regression. Two non-blocking follow-ups for Planner: (1) `Prism.Tests.Shared` is excluded from analyzer coverage by the `*Tests*` name match even though CLAUDE.md documents it as a non-test fixture classlib — debatable but defensible; (2) the ticket's own "verify the global `none` floor doesn't mute IDE0xxx hints" caveat was never checked, and user has since said to drop it — not pursued. S109/SA1633/SA1101 correctly left warn-only (not silently suppressed, not prematurely gated) pending phases 2-4.
**Phase 2 done (2026-07-23):** S109 triaged to zero across the solution (real baseline was ~163 unique warnings across ~30 files, not the stale 98 estimate — a clean analyzer rebuild had never actually been re-measured since the phase-1 baseline). Nearly everything was structural (file-format magic bytes, RGB/luma/CHW-tensor math, alpha thresholds, pixel-sample strides, switch-pattern case values, config-validation bounds) and got named `private const`s at point of use — zero behavior change. One genuine infra-tuning file (`WetransferClient.cs`) got promoted to `HostRules.json`'s new `weTransferPolling` section instead, per the shadow-defaults rule. Per-feature confidence weights (CLIP/heuristic calibration in `ImageFeatureAnalyzer.cs`, `NumericMatcher.cs`, `SiblingPropagator.cs`, `StringMatcher.cs`) were deliberately named-const'd, **not** moved to config — calibration is an open product question tracked by [[T-2600]]; see `AGENTFEEDBACK.md`'s S109 entry for the standing rule on any newly-discovered confidence literal. `-warnaserror:SA1402,SA1649,S109` gates CI.
**Phase 4 done (2026-07-24):** SA1101 (472+ `this.`-prefix warnings, later re-measured at 878 on a true clean build) fixed solution-wide via `dotnet format jb/src/PRISM.sln analyzers --diagnostics SA1101` — purely mechanical, 94 files, zero behavior change (verified: 799 insertions/799 deletions, identical line counts). `-warnaserror:SA1402,SA1649,S109,SA1101` now gates CI. **SA1633 (phase 3) resolved by permanent suppression, not fix**: per user decision, `dotnet_diagnostic.SA1633.severity = none` in `.editorconfig`, same treatment as SA1500 — this repo's doc-comment convention (class-level `/// <summary>` only, CLAUDE.md) makes a per-file header pure noise, not a real gap. Final verification: clean non-incremental Release build with all 4 gated rules as errors → 0 errors, 11 residual warnings (pre-existing SA0001/CS0414/CS8602/CS8600, outside this ticket's scope); full test suite 408/408 passing.
**Review (2026-07-24):** Approve. Independent reviewer pass against the full phase 2-4 diff (`main..HEAD`, 147 files): reproduced the clean `-warnaserror:SA1402,SA1649,S109,SA1101` build (0 errors) and the full suite (408/408, then 416/416 after closeout fixes) itself rather than trusting reported numbers; spot-checked config-extraction commits for shadow-default violations (none found — every section class `required`-props + `IValidatableConfig`) and the one-type-per-file fold-in exception; confirmed the SA1101 commit is purely mechanical. Two closeout findings raised and both resolved before this Approve: an open `jbtodo.md` block from this same branch closed (decision moved to `PRISM-pipeline-core.md`'s Configuration Lifecycle section per the todo-lifecycle rule), and missing fail-loud test coverage added for the two new config classes this ticket shipped (`OutputConfig`, `ClassifyParameters`), mirroring the existing `AnalyzerConfigTests.cs`/`TransformConfigTests.cs` pattern — verified independently, not just re-run.
**Found by:** 2026-07-12 analyzer baseline trial (StyleCop.Analyzers + SonarAnalyzer.CSharp on Prism.Core: 2,699 unique warnings).

**Problem:** Style/config rules are enforced only at edit time (conventions hook) and by review — nothing compiler-grade catches violations from non-Claude edits or agents that bypass process. The baseline trial measured per-rule cost in Prism.Core: SA1402 (one type per file) = 9, SA1649 (file name matches type) = 1, SA1101 (`this.` prefix) = 472, SA1633 (file header) = 113, SA1025 (whitespace) = 424, SA1503 (braces required) = 320, S109 (magic numbers) = 98.

**Pre-existing state (verified 2026-07-14):** `jb/src/Directory.Build.props` **already exists** (committed in `06e09ca` "First agentic wave") and currently sets `TargetFramework` / `ImplicitUsings` / `Nullable` / `LangVersion` / `Deterministic` for every project under `jb/src/`. This ticket **extends** that file — it does not create it. Nothing else is in place: no `StyleCop.Analyzers` or `SonarAnalyzer.CSharp` package reference exists anywhere in the repo, and there is no `SonarLint.xml`.

**What to do:**
1. Add `StyleCop.Analyzers` (prerelease, for modern C#) + `SonarAnalyzer.CSharp` to all production projects — via the existing `Directory.Build.props` at `jb/src/`, scoped to exclude the test project (S109 on test literals would be pure noise; decide test-project treatment explicitly).
2. Curated severities in the root `.editorconfig`: `dotnet_analyzer_diagnostic.severity = none` as the floor, then explicitly:
   - `warning`: SA1402, SA1649, SA1101, SA1633, **S109 (priority — this is the config-driven-design rule at compiler grade)**. S109 needs `dotnet_diagnostic.S109.severity = warning` (off by default) plus a `SonarLint.xml` AdditionalFile to set its allowed-values parameter (0, 1, -1 at minimum) so structural constants don't drown the empirical ones.
   - `none` **permanently**: SA1500 — it enforces Allman brace placement, the exact opposite of the house K&R rule (`csharp_new_line_before_open_brace = none` in `.editorconfig`). Comment the suppression with this reason.
   - `none` **for now (deferred, not rejected)**: SA1025, SA1503 — enable in a later phase once the 424 + 320 baseline is burned down (large mechanical cleanups; `dotnet format` handles most of SA1025).
   - Caveat to verify: the global `none` floor also mutes IDE analyzer hints (IDE0xxx) in C# Dev Kit — if that proves annoying, replace the floor with per-category StyleCop/Sonar disables.
3. Burn down in phases, gating each finished rule in CI (`-warnaserror:RULE` in the ci.yml build step): phase 1 SA1402 (9) + SA1649 (1); phase 2 S109 triage (98 in core — each is either moved to config per the shadow-defaults rule or explicitly justified as structural); phase 3 SA1633 (113 — decide the header template first; house style is token-lean, so keep it minimal); phase 4 SA1101 (472 mechanical `this.` insertions).
   - SA1101 direction check before phase 4: SA1101 *requires* the `this.` prefix; StyleCop's inverse rule is SX1101 (forbid it). Current code omits `this.` everywhere — confirm with the user that adding 472 prefixes is really the wanted direction, since it contradicts the "short, practical" style line.
4. Keep the conventions hook as-is — it stays the edit-time delta layer (catches new violations instantly, judgment-friendly); the analyzers are the build-time backstop that sees every edit from anyone.

**Acceptance:** Packages active in all production projects; curated `.editorconfig` severities in place with suppression reasons commented; SA1402/SA1649/S109 at zero warnings and CI-gated; SA1633/SA1101 either at zero or split into follow-up tickets; SA1500 suppressed with the K&R rationale; full suite green.

**Files:** `jb/src/Directory.Build.props` (exists — extend), `.editorconfig` (root, exists — extend), `SonarLint.xml` (new), `.github/workflows/ci.yml`, phased cleanup edits across `jb/src`.

---

### T-4110 · Unify ONNX Runtime execution-provider policy across every model-running component in PRISM
**Status:** Done (2026-07-20) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-20) — all acceptance criteria met; two non-blocking warnings: (1) `UpscaleService.Create`'s throw branches have no automated regression test (model assets absent in CI; fast follow-up suggested), (2) session-load failure (vs file existence) still degrades silently for YOLO/CLIP — doc caveat added to `PRISM-model-runtime.md`, code fix out of this ticket's scope.
**Found by:** [[T-4100]] — health-probe investigation surfaced two inconsistencies (version skew + YOLO CPU-only).
**Implemented (2026-07-20):** CPM via new `jb/src/Directory.Packages.props` — single pin for ORT DirectML 1.24.4 plus (user-directed scope extension) ImageSharp/OpenCvSharp4/test packages; new `OnnxSessionFactory` (file-linked like `GpuProbe`) is the sole session-construction path for CLIP/YOLO/Upscale; `RuntimeProviderProbe.SessionProviders()` no longer hardcodes YOLO=CPU; conventions-hook category `onnx-session-bypass` added and verified firing; policy doc `jb/docs/PRISM-model-runtime.md` + index row + classify-doc pointer + AGENTFEEDBACK entry. Build green; full suite failure set byte-identical to HEAD baseline on the Linux CI container (failures = missing model assets + Windows-only OpenCV natives, pre-existing).
**Scope extension (2026-07-20, user):** no algorithm switching on GPU presence — Upscale now loads Real-ESRGAN on every host (CPU EP when no adapter, like CLIP/YOLO). Follow-up user decisions the same day: `Upscaler_c_p_u` (Lanczos fallback) and the `ImageUpscaler` router are **deleted** — single `Upscaler` class; missing/unloadable Real-ESRGAN now fails startup loud (`ValidateModelAssets` + `UpscaleService.Create`), same as YOLO, no silent degradation. Decisions recorded in `PRISM-model-runtime.md`.
**Deferred to dev box (needs model assets + Windows):** CiMini golden 5× re-verify after the 1.20.1→1.24.4 CLIP runtime bump, and live `GET /PRISM/health` `SessionRuntimeProviders` check (expect all three identical: DirectML(GPU) on the GPU box / CPU when no adapter). Do these before /ticket-finish.
**Dev-box verification (2026-07-20):**
1. **Build + tests: PASS.** `dotnet build jb/src/PRISM.sln` — 0 errors. `dotnet test jb/src/PRISM.sln` — fully green: 399/399 (Upscale 15, Generate 10, Transform 51, Matching 193, Core 130).
2. **Health: PASS.** `GET /PRISM/health` → `SessionRuntimeProviders: ["CLIP=DirectML(GPU)","YOLO=DirectML(GPU)","Upscale=DirectML(GPU)"]` — all three identical, all GPU, YOLO no longer CPU-only. Caveat found while investigating step 4: `RuntimeProviderProbe.SessionProviders()` (`jb/src/api/RuntimeProviderProbe.cs:27-30`) derives all three labels from one `Upscaler.IsGpuAvailable` hardware check, not from querying each session's actual bound EP — so this endpoint cannot by itself catch a real per-model provider mismatch (e.g. one session silently falling back to CPU while `IsGpuAvailable` stays true). Not a regression from this ticket and out of the verification scope given here, but worth a follow-up ticket if that guarantee matters.
3. **CiMini golden 5×: PASS, 5/5 byte-identical.** `Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` run 5 consecutive times, no code change between runs — every run exited 0 and reported `Full PASSED: 14 sources match golden, 14 Ok.` against the same committed `expected-manifest.json` (asserts Status/FamilyId/FinalFileName/DetOrder per source), which transitively proves all 5 runs identical to each other post the 1.20.1→1.24.4 bump.
4. **Fail-fast: PASS, with a dev-box gotcha.** First attempt (renaming only `jb/src/core/Services/Upscale/Engine/ONNX/Real-ESRGAN_x2plus.onnx`) did **not** fail — this box has a machine-level `PRISM_ONNX_MODEL_DIR=C:\Users\JefB\prism-ci-assets\models` env var (`ModelAssetLocator`'s documented second-priority override, ahead of the source-tree walk) holding its own independent model copy, so the API started clean and healthy off that copy instead. Renamed the override-dir copy too, retried: startup now threw `Prism.Core.PrismConfigurationException: Real-ESRGAN ONNX model not found at 'Services/Upscale/Engine/ONNX/Real-ESRGAN_x2plus.onnx'...` from `PrismConfiguration.ValidateModelAssets` → `PrismApiConfiguration.Load()`, process exited, no port listener — correct fail-loud behavior, no silent fallback. Both copies restored afterward; re-verified clean healthy startup. Anyone re-running this check on a box with `PRISM_ONNX_MODEL_DIR` set must block that path too, or the test passes vacuously.

**Problem:** PRISM's ONNX/model-running components are inconsistent along three axes that should be uniform:
1. **Package version skew.** Classify (CLIP) pins `Microsoft.ML.OnnxRuntime.DirectML 1.20.1`; Upscale pins `1.24.4`. In the monolith API host both run in-process, so two versions of the same native runtime load into one address space — a latent binding/load-order risk (works today, but fragile).
2. **Provider policy skew.** CLIP (`ImageClassifier.cs:108-111`) and Upscale (`Upscaler_g_p_u.cs:60-62`) append the DirectML EP gated on `GpuProbe.HasHardwareDirectMLAdapter()`; **YOLO (`YoloDetector.cs:65`) appends no EP at all → CPU-only always**, even on a GPU box. No shared session-options factory exists, so each site decides independently.
3. **No mandate for future model code.** Analyzers (e.g. `Analyzer_FacePose`, `Analyzer_TextPresent`, YOLO-based ones) and future transformers (segmentation for coverage-ratio masks, etc.) will also run models, with no single policy to follow.

**Mandate (2026-07-15, user):** every part of PRISM image processing that runs a model MUST use the **same ONNX Runtime DirectML package, the same version, and the same execution-provider policy** — **CPU-only always works (mandatory baseline); GPU (DirectML) used automatically when a hardware adapter is present.** Applies to CLIP, YOLO, Upscale today, and to all future analyzers and transformers. This is a sibling of [[T-3300]] (each separable service/deployable must honor the same policy independently), not of T-3500/T-3600.

**What to do:**
1. **Single version.** Centralize the ONNX Runtime DirectML package + version to one pin (central package management via `Directory.Packages.props`, or the existing `jb/src/Directory.Build.props`). Align the two engine projects to one version. **Re-verify CiMini golden 5× after the bump** — changing CLIP's runtime can shift FP results (guards [[T-2820]]'s determinism).
2. **Single provider policy.** Introduce one shared session-options factory in core (e.g. `OnnxSessionFactory`, reusing `GpuProbe`) that appends the DirectML EP when a hardware adapter is present and falls back to CPU otherwise. Route CLIP, YOLO, and Upscale through it — YOLO gains GPU-when-present; all three become identical. No direct `AppendExecutionProvider_DML` or bare `new InferenceSession` outside the factory.
3. **Make it mandatory + enforced.** Document the policy in `jb/docs/PRISM-classify.md` (or a dedicated model-runtime note) + `AGENTFEEDBACK.md`, and add a conventions-hook category so any new `InferenceSession` not created via the factory fails review. Covers future analyzers/transformers.

**Acceptance:**
- Exactly one ONNX Runtime DirectML package + version referenced repo-wide (grep-proven).
- One shared session-options factory; CLIP/YOLO/Upscale all use it; no bare `InferenceSession`/`AppendExecutionProvider_DML` elsewhere.
- `GET /PRISM/health` `SessionRuntimeProviders` shows all three consistent (all DirectML(GPU) on a GPU box; all CPU on a CPU-only box).
- CPU-only mode fully green (forced no-adapter path); CiMini golden identical across 5 consecutive runs after version unification.
- Documented, enforced mandatory policy for any future model-running code.

**Files:** `jb/src/Directory.Build.props` (or new `Directory.Packages.props`), the three engine `.csproj`, `jb/src/core/Services/Matching/ImageClassifier.cs`, `jb/src/core/Services/Matching/Analyzers/YoloDetector.cs`, `jb/src/core/Services/Upscale/Engine/Upscaler_g_p_u.cs`, `jb/src/core/Services/Matching/GpuProbe.cs`, new `OnnxSessionFactory`, `jb/src/api/RuntimeProviderProbe.cs`, `jb/docs/PRISM-classify.md`, `AGENTFEEDBACK.md`, conventions hook.

---


### T-4600 · SSE progress events carry no per-item counts or blocked state
**Status:** Done (2026-07-20) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-20)
**Found by:** [[T-3400]] review (2026-07-14) — the web StatusPanel requirement that could not be met from the web side.

**Problem:** `PipelineProgressEvent` (`jb/src/core/Pipeline/PipelineProgressEvent.cs`) declares `CompletedCount`/`TotalCount`/`Severity` fields, but the only place any `PipelineProgressEvent` is ever constructed is `StageProgress.EmitStarted` (`jb/src/core/Services/StageProgress.cs:24-31`). It emits exactly one `"Stage {name} started."` event per stage, with `CompletedCount`/`TotalCount` left `null` and `Severity` hardcoded to `"Information"`. No accepted/rejected count, no blocked-vs-running state, and no per-item progress is emitted anywhere in the pipeline.

Consequence: the workbench can only ever display a stage *name*. `PRISM-workbench.md`'s Required Display section mandates "image collection/import state", "output preview", and "KO records" — none of which the SSE stream can currently source. T-3400 was closed on the narrower claim (real stage name replaces placeholder text) precisely because its web-only file scope made this unfixable there.

**What to do:**
1. Decide the progress contract: which stages emit per-item progress, and what an item is (per image? per family?). Import and Export are the two the workbench most needs (accepted/rejected counts).
2. Extend `StageProgress` beyond `EmitStarted` — at minimum an `EmitProgress`/`EmitCompleted` that populates `CompletedCount`/`TotalCount`, and a real `Severity` for blocked/warning states (KO records are the obvious source).
3. Emit from `Importer.cs` and `Exporter.cs` first (accepted/rejected are already computed there — KO records exist), then the remaining stages as warranted.
4. Update `StatusPanel.tsx` to read `severity` (it currently ignores the field entirely — only `StageRouteList.tsx:41` reads it) and render the real counts + blocked-vs-running distinction.

**Acceptance:** a running job's SSE stream carries non-null `CompletedCount`/`TotalCount` for Import and Export, and a non-`Information` `Severity` when items KO; the workbench StatusPanel shows real accepted/rejected counts and a blocked-vs-running distinction sourced from those events (no synthetic labels, per the No-Hidden-Behavior Rule).

**Resolution (2026-07-20):** `StageProgress.EmitCompleted` populates `CompletedCount`/`TotalCount`/`Severity` (Warning when koCount>0). Wired from `IngestService` (Import stage, using `NormalizedImages.Count`/`ImageKoRecords+ZipKoRecords`) and `Pipeline.ExportAsync` (Export stage, using `LambdaRecords` `IsKo` split — the same records `Exporter.BuildZip` packages into OK/KO folders, after a review round caught the first pass using pipeline-wide cumulative KO counts instead of stage-scoped ones). `StatusPanel.tsx` reads `severity` and renders a blocked-state chip. Remaining stages (Classified/Matched/Ordered/Renamed/Generated/Transformed) intentionally left on `EmitStarted`-only per the ticket's own scoping — a candidate follow-up if the workbench needs mid-pipeline KO visibility before the final Export tally.

**Files:** `jb/src/core/Pipeline/PipelineProgressEvent.cs`, `jb/src/core/Services/StageProgress.cs`, `jb/src/core/lib/Ingress/Importer.cs`, `jb/src/core/lib/Export/Exporter.cs`, `jb/src/workbench/web/components/StatusPanel.tsx`, `jb/docs/PRISM-workbench.md`.

---

### T-3300 · Validate and complete the Phase 2 distributed-services seam
**Status:** Done (2026-07-17) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-17)
**Tracks:** `jb/src/core/Services/jbtodo.md` (per-service test suite todo, triaged 2026-07-07).

**Problem:** The physical separation of deployables described as "Phase 2" in `PipelineServices.cs` is largely already built, not merely planned:
- `PipelineServiceFactory.CreateFromEnvironment` already swaps any of Ingest/Matching/Generate/Transform for its HTTP client (`Http*Service` in `jb/src/core/Services/Http/`) when `PRISM_INGEST_URL` / `PRISM_MATCHING_URL` / `PRISM_GENERATE_URL` / `PRISM_TRANSFORM_URL` is set.
- `jb/src/services/Prism.ServiceHost/Program.cs` already exposes each service over HTTP independently via `PRISM_SERVICE=ingest|matching|generate|transform|upscale`.

None of this is validated end-to-end:
1. No test exercises the actual HTTP round trip for any `Http*Service` client against `Prism.ServiceHost` — only in-process paths are tested today.
2. No CI job runs PRISM as actually-separate processes (multiple `Prism.ServiceHost` instances + URL env vars wired per service) — `ci.yml`/`full-pipeline.yml` only run the monolith API.

**Correction (2026-07-11):** this ticket previously claimed the API's in-process pipeline never initializes the GPU upscaler, sourced from `test/ci/README.md`. That's stale — `test/ci/README.md` describes a pre-T-2800 state. `PipelineServiceFactory.CreateFromEnvironment` already calls `EnsureUpscalerReady` before constructing `TransformService` on the same path `Pipeline`'s constructor uses (`jb/src/core/Pipeline.cs:26`), and T-2800 (archived Done) confirms this was fixed and verified via a live CiMini Full run. No upscaler-init fix is needed here; `test/ci/README.md`'s "Full run is currently red" section should be corrected separately (out of scope for this ticket).

**What to do:**
1. Add integration tests that stand up a `Prism.ServiceHost` instance (or in-memory `WebApplicationFactory`) per service and exercise each `Http*Service` client against it — real HTTP, not mocked.
2. Add a CI (or scheduled) job that runs the full pipeline with all four service URLs pointed at separate `Prism.ServiceHost` processes, and asserts it produces the same manifest as the in-process run on CiMini.
3. Only once distributed correctness is proven: split `Prism.Core.Tests` into per-service `.csproj` files along the existing namespace boundaries (`Transform/`, `Match/`, `Classify/`, `ImageNGP/`, `Order/`, `Rename/`, `Generate/`, `Export/`, plus [[T-3200]]'s `Ingest/`). This step only pays off once steps 1-2 make Phase 2 real — do not do it speculatively first.

**Acceptance:**
- `-Mode Full -Dataset CiMini` passes both in-process and fully distributed (4 separate `Prism.ServiceHost` processes), producing identical `expected-manifest.json`.
- Each `Http*Service` has at least one real-HTTP-roundtrip test.
- Test projects physically split, mirroring the proven service boundaries.

**Files:** `jb/src/core/Services/PipelineServiceFactory.cs`, `jb/src/core/Services/Http/*.cs`, `jb/src/services/Prism.ServiceHost/Program.cs`, `jb/src/tests/Prism.Core.Tests/*`, `.github/workflows/ci.yml`.

**Closeout (2026-07-17):** implemented across commits `2f337b6`..`53502f9` (T-3300 branch, merged to main at `1ebd00e`). Reviewer verified distributed correctness via actual CI run `29451640778` showing both in-process and 4-service-host distributed goldens matching on CiMini; all four `Http*Service` clients have real-HTTP roundtrip tests (`jb/src/tests/Prism.Core.Tests/ServiceHost/`); test projects split into `Prism.Services.{Matching,Generate,Transform,Upscale}.Tests` + `Prism.Core.Tests` + `Prism.Tests.Shared`. Two non-blocking follow-ups noted by review, not ticketed separately: (1) `ServiceHostTestHelpers.cs`/`ServiceHostFixture.cs` carry method-level XML doc comments against CLAUDE.md's class-summary-only rule; (2) root `jbtodo.md`'s T-3300 independent-review block (R1-R8) should be closed per the todo lifecycle, with R7 (sync-over-async remote upscale call) ticketed separately if still wanted.

---

### T-3500 · Fuse Import→Match in-process handoff to remove redundant image decode
**Status:** Done (2026-07-15) | **Profile:** P1-feature-worker
**Tracks:** root `jbtodo.md` — Import/Match fusion, triaged 2026-07-10.

**Problem:** `Importer.cs` normalizes each source image and writes it to disk once (`NormalizedJpgPath`, job temp folder). When Matching runs in the same process (today's default in-process mode), `MatchingService.PrepareLambda` (`jb/src/core/Services/Matching/MatchingService.cs:247`) re-reads that same file with `Image.Load<Rgba32>(source.NormalizedJpgPath)` — a second full decode of bytes Import already held in memory moments earlier, for every OK image in the batch.

**Scope decision (2026-07-10):** in-process decode reuse only. `NormalizedJpgPath` stays on disk unchanged — Exporter, KO handling, and the cross-process HTTP contract all still depend on it (see [[T-3600]] for that separate gap). This ticket only removes the redundant decode when Import and Match run in the same process/call.

**What to do:**
1. Extend the Import→Match handoff so the decoded normalized image (or raw normalized bytes) survives past `Importer.cs` into `IngestResult`/`ImageRecord_INPUT` for the in-process path, instead of being decoded, used, and discarded.
2. Update `MatchingService.PrepareLambda` to use the carried-forward image/bytes when present, falling back to `Image.Load(NormalizedJpgPath)` only when absent (i.e., when Matching is invoked without a preceding in-process Import — `HttpMatchingService`/`Prism.ServiceHost`, or any future direct-to-Matching entry point).
3. Confirm the fast-path already-conforming-JPEG case in `Importer.cs` (metadata-only `Image.Identify` + file copy, no full decode) still behaves correctly — that path has no decoded in-memory image to hand forward, so Match still decodes once there, same as today (no regression, just no double-decode to remove).
4. Verify no change to `NormalizedJpgPath`, `NormalizedWidth`/`NormalizedHeight`, or any disk artifact Exporter/KO handling reads — this is an in-memory-only optimization.

**Acceptance:**
- `dotnet build jb/src/PRISM.sln` 0/0.
- `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` produces an identical `expected-manifest.json` to a pre-change run (no behavioral change, only I/O reduction).
- Spot-check (debug counter or log) confirms decode calls against `NormalizedJpgPath` drop from 2 to 1 per image on the in-process path.

**Files:** `jb/src/core/lib/Ingress/Importer.cs`, `jb/src/core/Models/ImageRecord_INPUT.cs`, `jb/src/core/Services/Matching/MatchingService.cs`, `jb/src/core/Services/IngestResult.cs`.

**Closed: measured, not worth it (2026-07-15).** Gate decision per root `jbtodo.md`'s "measure before deciding" (user-approved, dataset SPACINI29 per user — CiMini too small). Temporary Stopwatch probe in `PrepareLambda` split the normalized-JPEG load into file-read vs decode; full pipeline on SPACINI29 (86 source JPEGs ~486 MB total, 86/86 OK, job wall **156.5 s**): file read **1.8 s summed** (~1.2% counted serially, <0.5% wall at the 8-wide `Parallel.For` fan-out — all a bytes-carry saves, since it still decodes from memory), decode **21.3 s summed CPU** (~2–3 s wall — all a decoded-`Image<Rgba32>` carry could save, at ~16 MB/image unbounded RAM spike + pixel drift vs. the JPEG on disk). Neither saving justifies the memory risk the jbtodo flagged. No production code changed; instrumentation reverted. Decision recorded in `PRISM-io-import.md` ("Import→Match Handoff: Disk Is the Contract"); root jbtodo block closed (commit cd4bc59).
**Review:** Approve (2026-07-15)

---

### T-3600 · Matching's HTTP contract silently assumes a shared filesystem with Import
**Status:** Done (2026-07-15) | **Profile:** P4-critical-architecture
**Found by:** Import↔Match fusion scoping ([[T-3500]]), 2026-07-10.

**Problem:** `HttpMatchingService.MatchAsync` (`jb/src/core/Services/Http/HttpMatchingService.cs`) POSTs the full `IngestResult` as JSON to a remote Matching host. `IngestResult`'s per-image records carry `NormalizedJpgPath` as an absolute file path string (`jb/src/core/Models/ImageRecord_INPUT.cs:35`) — not bytes. A genuinely separate/public Matching deployment (per the root `jbtodo.md`'s "keep the matching service open to the public" goal) has no way to read that path unless it happens to share a mounted filesystem with whatever Import instance produced it. This is undocumented today — `PRISM-io-import.md` describes the local-temp-folder lifecycle but doesn't flag that the Matching HTTP client/host pair depends on it being shared with Ingest.

**What to do:**
1. Confirm the gap: check whether `Prism.ServiceHost` (`PRISM_SERVICE=matching`) is ever run against a different machine/container than Ingest in any existing deployment path, or whether it's simply untested today (per [[T-3300]], which already flags no CI job runs the services as truly separate processes).
2. Decide and document the fix: either (a) ship normalized image bytes over the wire in the Match request (bigger payload, but makes Matching truly standalone/public), or (b) formally document and enforce a shared-volume requirement between Ingest and Matching deployables (smaller, but contradicts "open to the public" unless the public entry point is different from the internal Ingest→Match handoff).
3. If (a): update `IngestResult`/`ImageRecord_INPUT` serialization, `HttpIngestService`/`HttpMatchingService`, and `PRISM-io-import.md`'s Zip/temp-folder section to describe the new contract.
4. If (b): document the shared-volume requirement explicitly in `PRISM-io-import.md` and `AGENTFEEDBACK.md`, and add a startup check or clear failure mode when `NormalizedJpgPath` isn't readable from the Matching host.

**Acceptance:**
- A documented, deliberate answer to "can Matching run as a truly independent/public service without sharing a filesystem with Ingest" exists in `jb/docs/`.
- Whichever fix is chosen is implemented and covered by [[T-3300]]'s planned real-HTTP-roundtrip tests.

**Files:** `jb/src/core/Services/Http/HttpIngestService.cs`, `jb/src/core/Services/Http/HttpMatchingService.cs`, `jb/src/core/Models/ImageRecord_INPUT.cs`, `jb/docs/PRISM-io-import.md`, `AGENTFEEDBACK.md`.

**Direction + partial progress (2026-07-15, user):** the central design question is **decided**: Ingress + Matching + Export are **always co-deployed on one physical system**; only Transform/Generate/Upscale vary per public route. This selects **option (b)** — no ship-bytes-over-the-wire work; the core needs no cross-host shared filesystem because ingress and matching run in one process sharing one job temp folder. Confirmed in code that URL ingress is fully implemented (`FetchDispatcher` + Dropbox/WeTransfer/HTTPS fetchers → `SourceKind = RemoteUrl`), and lives in the **API host** (`PrismProcessIngressReader`), not the standalone `Prism.ServiceHost` matching route. First slice of the "documented deliberate answer" acceptance landed: a **Core vs. Features** section added to `jb/docs/PRISM-overview.md` (core = aggregation+normalize+match+order+export fed by URL/upload; features = Transform/Generate/Upscale; ServiceHost split is feature-only). **Remaining for this ticket:** fold the same statement into `PRISM-io-import.md` + `AGENTFEEDBACK.md`, and add the startup check / clear failure mode when a Matching host can't read `NormalizedJpgPath` (covered by [[T-3300]]'s planned real-HTTP tests).

**Completed (2026-07-15):** remaining scope landed — `PRISM-io-import.md` gained a "Co-Deployment Contract" section, `AGENTFEEDBACK.md` a core co-deployment Behavioral Memory bullet, and `MatchingService.MatchAsync` now throws an explicit `InvalidOperationException` (co-deployment message, not `PrismConfigurationException` — deployment topology, not config) when OK images exist but `IngestResult.JobTempFolder` is unreadable, replacing misleading per-image `CLASSIFY_ERROR` KOs. Covered by `Match/MatchingCoDeploymentGuardTests.cs` (Match suite 56/56 green; build 0 errors, only pre-existing warnings). Real-HTTP roundtrip coverage stays with [[T-3300]] as ticketed.
**Review:** Approve (2026-07-15)

---

### T-4100 · Investigate real GPU vs CPU ONNX behavior: health reports CPU-only on a GPU dev machine
**Status:** Done (2026-07-15) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-15)
**Found by:** memory-vs-reality contradiction during the 2026-07-10 full-pipeline test.

**Problem:** `GET /PRISM/health` on the dev machine reported `SupportedRuntimeProviders: ["CPU"]` during the 2026-07-10 test runs, yet project memory (and the Upscale engine's purpose) says the dev machine has a real GPU and the full GPU pipeline is locally testable. Either (a) the current build genuinely runs all ONNX inference (CLIP, YOLO, Real-ESRGAN) on CPU — meaning GPU acceleration silently stopped being used, or (b) the health endpoint's provider probe is wrong/stale and misreports what ONNX Runtime actually uses. Both are worth knowing: (a) costs real wall-clock time on every classify/upscale batch; (b) makes the health endpoint lie about capacity.

**Policy context (`jb/docs/PRISM-classify.md`):** CPU is the required baseline; GPU is a bonus resource only; GPU absence must never fail a job. This ticket is about *knowing* which one is actually in use and restoring GPU use if it regressed — not about making GPU required.

**What to do:**
1. Determine what `SupportedRuntimeProviders` in `PrismApiConfiguration`/health probe actually reflects (queried ONNX Runtime providers vs hardcoded list).
2. Check which ONNX Runtime package(s) the solution references (CPU-only `Microsoft.ML.OnnxRuntime` vs `.Gpu`/DirectML) and which execution providers `InferenceSession` creation actually requests in `ImageClassifier`, YOLO analyzers, and `Upscaler_g_p_u`.
3. Measure: time a CiMini classify batch under the current build; if a GPU provider can be enabled (DirectML on this Windows box), measure again and record the delta.
4. Fix whichever side is wrong: either wire the GPU execution provider back in (keeping CPU fallback per policy) or correct the health probe so it reports the truth; update `project_local_gpu_verification` memory and any stale doc claims.

**Acceptance:**
- A documented answer to "what provider does each ONNX session actually use on this machine" (health endpoint + a log line or doc note).
- If GPU is available and enabled: CiMini classify measurably faster than CPU-only baseline, with CPU-only mode still fully green.
- Health endpoint reflects the real provider list.

**Files:** `jb/src/api/PrismApiConfiguration.cs` (or wherever the provider probe lives), `jb/src/core/Services/Matching/Classify/ImageClassifier.cs`, `jb/src/core/Services/Matching/Analyzers/*.cs` (YOLO session), `jb/src/core/Services/Upscale/Engine/Upscaler_g_p_u.cs`, `jb/src/core/config/Prism_Config.json`.

**Findings + fix (2026-07-15):** the trigger was a **hardcoded lie**, not a GPU regression. The health endpoint set `SupportedRuntimeProviders = ["CPU"]` as a string literal (`api/Program.cs:52`) — it never queried ONNX, so the "CPU-only" report proved nothing. Actual providers (verified by reading the session-creation code): **CLIP** appends the DirectML EP gated on a hardware DX12 adapter (`ImageClassifier.cs:108-111`) → GPU here; **Upscaler** likewise (`Upscaler_g_p_u.cs:60-62`) → GPU here; **YOLO** appends no EP (`YoloDetector.cs:65`) → always CPU. **Fix:** `SupportedRuntimeProviders` now = `OrtEnv.Instance().GetAvailableProviders()`, plus a new `SessionRuntimeProviders` field reporting per-session usage (`api/RuntimeProviderProbe.cs`, reusing public `ImageUpscaler.IsGpuAvailable`). **Verified live** on the dev box: `SupportedRuntimeProviders = [DmlExecutionProvider, CPUExecutionProvider]`, `SessionRuntimeProviders = [CLIP=DirectML(GPU), YOLO=CPU, Upscale=DirectML(GPU)]`. Memory `project_local_gpu_verification` updated. Build 0 errors, 370 tests pass.

**Surfaced, NOT changed (deliberate — need own follow-up):** (1) **ONNX version skew** — Classify pins `Microsoft.ML.OnnxRuntime.DirectML 1.20.1`, Upscale pins `1.24.4` (two ORT runtimes in one process); aligning must be paired with a full CiMini re-verify since it can perturb CLIP numerics (relevant to [[T-2820]]). (2) **YOLO CPU-only** — deliberate per baseline policy, but a possible GPU-speed opportunity. A formal CPU-vs-GPU classify timing delta was not measured (GPU use is confirmed active; forcing CPU to benchmark is follow-up work). Recommend a small follow-up ticket for the version alignment specifically.

---


### T-3700 · Align project/assembly names, solution structure, and test namespaces with the Services/ restructure
**Status:** Done (2026-07-15) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-15)
**Found by:** namespacing audit, 2026-07-10.

**Problem:** The 2026-07-08 core restructure renamed folders and C# namespaces to `Services/`/`Prism.Services.*` + `lib/`/`Prism.Lib.*`, but several identifiers were never updated, so the same project now answers to 2-3 different names depending on where you look. Confirmed by direct inspection of every `namespace` declaration, every `.csproj`, and `PRISM.sln`:

1. **Project/assembly identity mismatch.** `Prism.Core.Images.Classify.csproj`, `Prism.Core.Images.Transform.csproj`, `Prism.Core.Images.Upscale.csproj` never had their file name/assembly name updated. Each now has three different names for the same project: assembly `Prism.Core.Images.Transform.dll`, namespace `Prism.Services.Transform`, folder `Services/Transform/Engine/` (same pattern for Classify → `Prism.Services.Matching`, Upscale → `Prism.Services.Upscale`). Neither `<AssemblyName>` nor `<RootNamespace>` is set explicitly in any of the three, so both default to the stale file name.
2. **Stale solution-folder hierarchy.** `PRISM.sln` still nests these three projects under solution folder `core > Images > Transform` / `core > Images > Classify` — a Visual Studio artifact left over from the pre-restructure `jb/src/core/Images/` layout, not the real `Services/` layout.
3. **Upscale invisible in the solution.** `Prism.Core.Images.Upscale.csproj` has no `Project(...)` entry in `PRISM.sln` at all — it only builds because `Prism.Core.csproj` references it directly via `<ProjectReference>`. Its sibling engine projects (Classify, Transform) do have solution entries; Upscale doesn't, for no documented reason.
4. **CLAUDE.md's project list is stale.** The solution-project list in CLAUDE.md's Architecture section names Contracts/Core/Images.Classify/Images.Transform/Api (Workbench.Wpf was removed 2026-07-10 along with the WPF workbench itself), but omits `Prism.Core.Images.Upscale`, `Prism.Core.Tests`, and `Prism.ServiceHost` — all three real and already part of the tree (Tests and ServiceHost even have `PRISM.sln` entries). `jb/docs/PRISM-transform-generate.md` also has one stale example path (`Images/Upscale/ONNX/...` instead of the actual `Services/Upscale/Engine/ONNX/...` used by `Prism_Config.json` and `PrismConfigLocator`).

**Correction (2026-07-14):** this ticket previously carried a fifth item — the Analyzers test-namespace break (`namespace Prism.Core.Tests.Analyzers;` instead of `PrismCoreTests.Analyzers`), which made `--filter "FullyQualifiedName~PrismCoreTests.Analyzers"` match zero tests. **That item is already fixed** and is no longer part of this ticket: all four files in `jb/src/tests/Prism.Core.Tests/Analyzers/` (`YoloDetectorTests.cs`, `VisualAnalyzerTests.cs`, `ProductTypeResolverTests.cs`, plus `AnalyzerConfigTests.cs` added by T-4300) now declare `namespace PrismCoreTests.Analyzers;`. Fixed incidentally by commit `c16ec50` ("align tests with namespace refactoring"), not by this ticket. The bug no longer reproduces — do not "re-fix" it. What remains here is the pure project/solution rename.

**Confirmed blast radius (re-measured 2026-07-14):** 5 files repo-wide contain the literal string `Prism.Core.Images` (excluding the ticket board itself) — `Prism.Core.csproj` (3 `<ProjectReference>` paths), `PRISM.sln`, `CLAUDE.md`, `jb/docs/PRISM-transform-generate.md`, plus 1 doc-comment in `Tx_DetailCropper.cs` that references the project name descriptively. `CropTransformSettings.cs` no longer mentions it — T-4530 rewrote that file during the ConfigLoader migration, so it has dropped out of scope. No CI workflow, PowerShell script, or test infra hardcodes these names (checked `.github/workflows/*.yml`, `test/ci/`). Model-asset resolution (`PrismConfigLocator.FindModelAsset`, `Prism_Config.json`'s `Models` section, `PrismConfiguration.cs`, `FeatureAnalysisService.cs`) already uses the correct `Services/...` paths — not part of this bug, already fixed in the original restructure. This is a build-graph/text rename with no runtime behavior change.

**What to do:**
1. Rename the three engine `.csproj` files to match their real namespace: `Prism.Core.Images.Classify.csproj` → `Prism.Services.Matching.Classify.csproj`, `Prism.Core.Images.Transform.csproj` → `Prism.Services.Transform.csproj`, `Prism.Core.Images.Upscale.csproj` → `Prism.Services.Upscale.csproj`. Update the 3 `<ProjectReference>` paths in `Prism.Core.csproj` accordingly.
2. Update `PRISM.sln`: rename the 3 project entries to their new names/paths, add the missing Upscale project entry, and replace the stale `Images` solution folder with one that mirrors the real `Services/` layout.
3. Update the doc-comment mention in `Tx_DetailCropper.cs` to the new project name.
4. Update CLAUDE.md's Architecture/Solution project list to name every project actually in the tree (add Upscale, Tests, ServiceHost), and fix the one stale path example in `PRISM-transform-generate.md`.
5. Do **not** touch `Prism.Contracts`-namespaced files that live outside `Models/` (e.g. `OrderEvidence.cs`, `MatchEvidence.cs`, `ImageFeatureSnapshot.cs`) — that cross-folder namespace is deliberate (`Prism.Core.Contracts.csproj` cherry-picks files by relative path regardless of physical location). Don't "fix" these into folder-matching namespaces.

**Verification:**
- `dotnet build jb/src/PRISM.sln` → 0 errors / 0 warnings, same as before the rename.
- `dotnet sln jb/src/PRISM.sln list` shows all real projects, including the 3 renamed ones and the previously-missing Upscale entry.
- Full existing suite (`dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj`) has the same pass count before and after — pure identity rename, nothing should newly pass or fail.
- `git grep -n "Prism.Core.Images"` returns zero hits repo-wide.
- Open `PRISM.sln` (Visual Studio or `dotnet sln list`) and confirm the solution-folder hierarchy matches the physical `Services/`/`lib/` layout — no leftover `Images` grouping.
- `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` still produces the existing `expected-manifest.json` unchanged (proves the rename didn't alter runtime behavior).

**Files:** `jb/src/core/Services/Matching/Classify/Prism.Core.Images.Classify.csproj`, `jb/src/core/Services/Transform/Engine/Prism.Core.Images.Transform.csproj`, `jb/src/core/Services/Upscale/Engine/Prism.Core.Images.Upscale.csproj`, `jb/src/core/Prism.Core.csproj`, `jb/src/PRISM.sln`, `jb/src/core/Services/Transform/Engine/Tx_DetailCropper.cs`, `CLAUDE.md`, `jb/docs/PRISM-transform-generate.md`.

**Done (2026-07-15):** all four items implemented. (1) Three engine `.csproj` renamed via `git mv` → `Prism.Services.Matching.Classify.csproj` / `Prism.Services.Transform.csproj` / `Prism.Services.Upscale.csproj`; the 3 `<ProjectReference>` paths in `Prism.Core.csproj` updated. (2) `PRISM.sln`: 3 project entries renamed, missing **Upscale project entry added** (with config-platforms + nesting), stale `Images` solution folder replaced by `Services` mirroring the real layout (`Services > Matching/Transform/Upscale`). (3) `Tx_DetailCropper.cs` doc-comment updated. (4) `CLAUDE.md` project list now names all 8 projects (added Upscale/Tests/ServiceHost, dropped the stale "not in .sln" caveat); `PRISM-transform-generate.md` `Prism.Core.Images.Upscale` mention fixed. **Verification:** `dotnet build jb/src/PRISM.sln` 0 errors / 2 pre-existing warnings; `dotnet sln list` shows all 8 incl. renamed 3 + Upscale; `git grep "Prism.Core.Images"` = 0 in code/config/docs (ticket board excepted); **370 tests pass** (same as before); CiMini Full still produces the golden manifest unchanged. Pure identity rename, no runtime change. **Ready for review.**

---


### T-2830 · `_det#` numbering starts at det8 instead of the documented zero-based det0
**Status:** Done (2026-07-15) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-15)
**Found by:** [[T-2800]] end-to-end verification (2026-07-02).

**Problem:** CLAUDE.md's domain vocabulary states: "`_det#` — Zero-based image ordering suffix within a FamilyID (e.g. `_det0`, `_det1`)." The captured Full-mode CiMini manifest (2026-07-02) instead showed every family's first image landing on `_det8` — e.g. family `90861025` → `90861025_det8.jpg`, family `90861026` → `90861026_det8.jpg`, family `94613033`'s three images → det8/det9/det10. No family in the fixture ever produced `_det0` through `_det7`. This strongly suggests `DetOrderRules.json`'s per-product-type slot list is indexed against some fixed ordered list of ImageRoles, and slot 8 happens to be the first role CiMini's images actually match, rather than det numbering restarting at 0 per family as documented.

**Current vs. target behavior:**
- Current: the first assigned image in a family gets `_det8` (or higher); no image is ever `_det0`–`_det7`.
- Target (per CLAUDE.md domain vocabulary): det-slot numbering is zero-based *per family* — the first image in any family's det order should be `_det0`.

**What to do:**
1. Read `DetOrderRules.json` (`jb/src/core/config/`) and `ImageOrderer.Run`/`DetSlotRule.cs`/`CandidateDetOrder.cs` (`jb/src/core/Services/Matching/Order/`) to find where the numeric det index is derived from ImageRole precedence.
2. Determine whether this is a genuine off-by-N indexing bug (e.g. enumerating a role list that includes roles never present in CiMini, with matched roles landing at index 8+) or a deliberate-but-undocumented convention — resolve via `jb/docs/PRISM-order-rename.md` (documented owner of `_det#`/ordering rules) and the `jbtodo.md` process if intent isn't already decided.
3. Fix the indexing (or correct the documentation, whichever is actually wrong) so det numbering matches the agreed convention.
4. Sequence after [[T-2820]] — recapturing `expected-manifest.json` to verify this fix needs deterministic det-slot assignment first.

**Acceptance:**
- First image in every family's det order is `_det0` (or CLAUDE.md's vocabulary is corrected to match the actual intended behavior, if that's the real resolution) — confirmed on CiMini.
- `jb/docs/PRISM-order-rename.md` and CLAUDE.md agree with implemented behavior.

**Files:** `jb/src/core/config/DetOrderRules.json`, `jb/src/core/Services/Matching/ImageOrderer.cs`, `jb/src/core/Services/Matching/Order/*.cs`, `jb/docs/PRISM-order-rename.md`, `CLAUDE.md`.

**Note (2026-07-14, [[T-4560]] verification):** appears **already fixed**. The CiMini evidence run numbered from zero — `90861025_det0.jpg`, and family `94613033` → det0/det1/det2 — which is exactly the documented target. Confirm on a fresh run before closing; nothing in this repo's history explicitly claims the fix.

**Direction (2026-07-14, user):** ordering also depends on **phenotypes**, which are still only half implemented — so the det index that comes out of the spec'd ordering pass can legitimately leave gaps while that work is incomplete. Consider a **final collapse pass**: after ordering runs per spec, renumber each family's assigned slots down to a contiguous `det0..detN` with no gaps. Make it **toggle-able** via config — the pre-collapse numbering is the one that carries ImageRole/slot meaning, and we may want to see it raw.

**Resolution (2026-07-15):** the requested toggle-able collapse pass **already exists and is verified** — no new code needed. `ImageOrderer.CompactDetOrder` (`ImageOrderer.cs:44`) renumbers each family to contiguous `det0..detN` (renumber only, never reorder), called from `Exporter.Run` + the MatchLite/MatchOnly `PrismService` paths, gated by the toggle `Output.DET-ORDER-GAPS-ALLOWED` (`Prism_Config.json`, currently `false` = collapse on). Fresh CiMini run confirms det0-based per-family numbering (golden already encodes it, e.g. family `94613033` → det0/det1/det2). Docs reconciled: `jb/docs/PRISM-order-rename.md` now names the method and adds the phenotype caveat (contiguous numbering reflects overflow order until phenotypes fire — not an ordering bug); `CLAUDE.md` already agreed. `Order/jbtodo.md` removed (decision moved to the doc, file empty). **Ready to close.**

---


### T-2820 · Ordered stage assigns non-deterministic det-slots for tied images within a family
**Status:** Done (2026-07-15) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-15)
**Found by:** [[T-2800]] end-to-end verification (2026-07-02).

**Problem:** Running `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` three times back-to-back against the same unchanged build produced three different det-slot assignments for images tied within a family. Family `94613033` (`Pareo Exotica.jpg`, `Pareo_exotica_F1.jpg`, `Pareo_exotica_F2.jpg`) assigned `Pareo_exotica_F1.jpg` to det10, then det8, then det9 across three consecutive runs with no code change in between; family `90861083` (`23211008_02_A.jpg`/`_B.jpg`) flip-flopped between det8/det9. This makes any `expected-manifest.json` golden-file test unsafe for a family with more than one image sharing the same ImageRole/precedence tier, since there is no single correct "expected" det-slot to pin.

**Evidence:** 3 consecutive runs (2026-07-02), same build, same input, same API process — det-slot assignment for tied images changed every run. The `Ordered` stage runs before `Transformed` in the immutable pipeline order (Imported → Classified → Matched → **Ordered** → Renamed → Generated → **Transformed** → Exported), which rules out [[T-2800]]'s Transform/Upscale fix as the cause — `Ordered` output cannot depend on a later stage's behavior or timing.

**Root cause (untriaged):** `MatchingService.BuildLambda`'s `Parallel.For` results are explicitly re-aggregated in original input order ("Aggregate into ordered collections (single-threaded; preserves input order for deterministic matching)"), so `LambdaRecords` itself is deterministic. The non-determinism most likely enters via `ImageOrderer.Run` (`jb/src/core/Services/Matching/ImageOrderer.cs`) or upstream CLIP classification confidence — if GPU/DirectML inference has run-to-run floating-point variance for near-identical images, and the det-slot ranking for same-role candidates doesn't fall back to a fully deterministic secondary key (e.g. filename or original list order) when scores are equal/near-equal, ties resolve arbitrarily.

**What to do:**
1. Read `ImageOrderer.Run` and `jb/src/core/Services/Matching/Order/*.cs` (`DetSlotRule.cs`, `CandidateDetOrder.cs`, `DetOrderConfig.cs`) to find where same-role candidates are ranked/tie-broken.
2. Confirm or rule out CLIP/GPU floating-point non-determinism as the trigger.
3. Add a fully deterministic secondary/tertiary tie-break so equal/near-equal candidates always resolve the same way, every run.
4. Re-run `Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` at least 5x consecutively and confirm identical det-slot assignment every time before recapturing `expected-manifest.json`.

**Acceptance:**
- 5 consecutive `-Mode Full -Dataset CiMini` runs (no code change between them) produce byte-identical `FinalFileName`/`DetOrder` for every image.
- `expected-manifest.json` can be captured once and trusted as a stable golden file.

**Files:** `jb/src/core/Services/Matching/ImageOrderer.cs`, `jb/src/core/Services/Matching/Order/*.cs`, `jb/src/core/Services/Matching/MatchingService.cs` (if root-caused to classification confidence).

**Note (2026-07-14, [[T-4560]] verification):** did **not** reproduce. Three consecutive `-Mode Full -Dataset CiMini` runs on an unchanged tree produced byte-identical `DetOrder`/`FinalFileName`, all matching golden. Three runs cannot prove an intermittent race is gone — a coin can land the same way three times — so the ticket stays open, but re-verify (5+ runs, per Acceptance) before spending effort on a fix.

**Direction (2026-07-14, user):** the fix likely lives in **CLIP refinement**, not in a tie-break hack. If two images in a family score near-identically, that is the classifier failing to distinguish them; a deterministic secondary key would only freeze an arbitrary answer in place. Look at (a) the model side, (b) the CLIP prompts (`ClipPrompts.json`), and (c) the PRISM config values — thresholds in particular — before adding tie-break machinery.

**Verification (2026-07-15):** ran **5 consecutive `-Mode Full -Dataset CiMini` runs on an unchanged build** — all 5 byte-identical to golden (14/14 Ok every run, incl. tied families `94613033` and `90861083`). The ticket's Acceptance bar (5 consecutive identical runs) is **met**; the bug does **not reproduce** on the current build. Confirmed why: `ImageOrderer.CompareCandidates` (`ImageOrderer.cs:253`) already tie-breaks on `string.CompareOrdinal(Filename)` before `SourceIndex`, so exact ties are deterministic and input-order-independent. The residual theoretical risk (CLIP/NGP confidences differing by GPU float noise → *near*-ties that flip ordering before the filename key engages) did not manifest across 5 runs. Note this is now consistent with the T-4100 finding that CLIP genuinely runs on DirectML/GPU here. **Recommended disposition:** close as "acceptance met, not reproducing" OR keep as a low-priority watch; pursue the CLIP-refinement direction only if/when it recurs. Orchestrator/user decision.

**Closed (2026-07-15, user):** closing as acceptance-met / not-reproducing. User will signal to refine (CLIP-refinement direction) in the future if it recurs — no watch kept for now.

---



### T-4500 · Master: generic ConfigLoader + Transform cleanup (waves T-4510…T-4560)
**Status:** Done (2026-07-14) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-14) — index ticket, no diff of its own. Every child was individually reviewed: T-4510/T-4520/T-4530/T-4540 Approve (2026-07-12), T-4550 Approve (2026-07-14), T-4560 Approve (2026-07-14).

Master/index ticket for the approved 2026-07-12 plan: replace the per-config `Load()` pattern AND `PrismConfigLocator` with one generic section-aware **ConfigLoader**, clean up the Transform folder layout, delete `BackgroundType`, and fold `ImageTransformationResult` into the record lifecycle (`Base → INPUT → LAMBDA → OUTPUT`).

All six children Done:
- Wave 1: [[T-4510]] ConfigLoader core ∥ [[T-4520]] Transform layout + dead code
- Wave 2: [[T-4530]] Transform adoption ∥ [[T-4540]] Analyzers adoption
- Wave 3: [[T-4550]] OUTPUT record merge (commit `d5c2727`)
- Wave 4: [[T-4560]] rest-of-PRISM migration + retire PrismConfigLocator/ConfigCache (commit `5e98be0`)

**Master-level gate — all passed (2026-07-14, final state = `5e98be0`):**
- `dotnet build jb/src/PRISM.sln` → 0 errors. (2 warnings, `CS0414 MatchingService._disposed` + `CS8602`, are pre-existing at HEAD in untouched code — not introduced by this work. Worth a follow-up.)
- Full suite: **370 passed / 0 failed.**
- API startup fail-loud check: misspelling `FeatherPx` in `transform_Config.json` stops startup with `Prism.Core.PrismConfigurationException: Cannot load section 'BgStretch' … missing required properties including: 'FeatherPx'`.
- `pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini` → PASSED, 14 sources match golden, 14 Ok.
- Evidence run (non-vacuous transformed output): **14/14 manifest rows** carry `TransformerType` + `TransformationStatus` sourced from `ImageRecord_OUTPUT` — 9× `Tx_CropSquare`, 5× `Tx_CenterAndStretch`, all `Ok`.

**Incidental finding worth keeping:** [[T-2820]] (non-deterministic det-slots for tied images) **did not reproduce**. Three consecutive `-Mode Full -Dataset CiMini` runs on an unchanged build produced byte-identical `DetOrder`/`FinalFileName`, matching the golden every time. That made the T-4560 identity check a strict golden match rather than a fuzzy diff. Three runs cannot prove an intermittent race is gone — but T-2820's stated repro no longer reproduces, so re-verify before spending effort on it. Related: `_det` numbering now starts at `det0` (see the evidence table above), which is also what [[T-2830]] asks for — re-check that ticket too before working it.

**Files:** index only — see child tickets.

---


### T-4560 · Migrate remaining PRISM to ConfigLoader; retire PrismConfigLocator + ConfigCache
**Status:** Done (2026-07-14) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-14)

Migrated all 23 `PrismConfigLocator` and all 9 `ConfigCache` call sites to `ConfigLoader.RequireFile`/`Section<T>`/`Root<T>` and `ModelAssetLocator.Find`; deleted `PrismConfigLocator.cs` and `ConfigCache.cs`. `git grep` returns zero source hits. Commit `5e98be0`.

**Decision — ConfigCache deleted with NO replacement (do not re-add a config cache).** It memoized the hand-written `Load(path)` parsers, but the memoization was measured and found worthless: all config JSON in the project totals **62 KB**, and every one of those sites fires **once per job, never per image** (`ImageMatcher.Run` is a static per-job method; `MatchingService` constructs its sub-services once per job; `TransformService` bundles once per stage run). Config parsing is on the order of **0.01%** of a job that runs CLIP + YOLO per image plus Real-ESRGAN. Those sites now call their parser directly. Recorded in `jb/docs/PRISM-pipeline-core.md`.
`ConfigLoader`'s **own** internal cache stays — a different thing. The two fixed-signature engine webservice entry points (`Tx_util_BgStretch.Process`, `Tx_LowContrastEnhancement`) self-load per call, and that one *is* the per-image path.

**Scope widened mid-ticket (user-approved): one exception type for config.** `PrismConfigurationException` is now the single fail-loud type for every config failure — `ConfigLoader`'s own throws plus ~45 across every section class's `Validate()` and every hand-written parser (Excel, Analyzers, Classify, Match, Order, Transform/Admin, Upscale). It derives from `InvalidOperationException`, so `catch` sites are unaffected — **but xUnit's `Assert.Throws<T>` is exact-type**, so config tests now assert the precise type (this is what caught the change; 8 tests failed until updated). Non-config runtime failures deliberately keep `InvalidOperationException`: image-too-small, HTTP/WeTransfer fetch, `ServiceHttp`, the `Upscaler_g_p_u.Initialize()` lifecycle guard, and `ExcelFileHandler`'s user-workbook parsing (a bad user workbook is not a deployment fault).

**Also:** `Prism.Core.Images.Upscale.csproj` had **no** `ProjectReference` to `Prism.Core.Contracts` and only compiled transitively — it now references it directly (no cycle; Contracts has no outbound references). `Prism.Config` added to the 4 GlobalUsings shims. `PrismConfiguration.FileName` const replaces the repeated `"Prism_Config.json"` literal.

**Acceptance — all met:** zero `git grep` source hits; build 0 errors; suite 370 passed / 0 failed (same count — identity migration); CiMini Full byte-identical to the pre-change 3-run baseline including `DetOrder`.

**Files:** `jb/src/core/config/*`, all call sites, ~20 config classes, 3 engine csprojs, 4 GlobalUsings, `CLAUDE.md`, `jb/docs/PRISM-pipeline-core.md`, `jb/docs/PRISM-transform-generate.md`, `test/ci/README.md`.

---


### T-4550 · Fold ImageTransformationResult into ImageRecord_OUTPUT (Base→INPUT→LAMBDA→OUTPUT)
**Status:** Done (2026-07-14) | **Profile:** P4-critical-architecture
**Review:** Approve (2026-07-14)

Folded `ImageTransformationResult` into `ImageRecord_OUTPUT`, completing the record lifecycle. Commit `d5c2727`.

`ImageRecord_OUTPUT` now carries a **transform block** and an **export block**. Transform creates the record and fills the transform block; Export enriches that same instance with the export block and re-copies the identity fields (`CompactDetOrder` may have renumbered `_det` since Transform ran). Deleted `Engine/ImageTransformationResult.cs`, its Contracts csproj link, and `ImageRecord_LAMBDA.TransformationResult`.

**Design decisions (all reviewer-confirmed):**
- Property is `TransformStatus`, not `Status` — the record already inherits `ImportStatus` and carries `ExportStatus`, so a bare `Status` is ambiguous. It is **nullable**, so "transform never evaluated this image" stays distinguishable from the enum's `NotEvaluated` default; this preserves `Exporter.BuildTransformStep`'s `?? (IsKo ? "Skipped" : "Ok")` fallback exactly.
- Props are `get; set;` not `init` — two stages write the record now.
- `Tx_*` classes do **not** set the identity fields; Export owns them. Verified by tracing every reader: nothing reads identity off a KO-at-transform record (`ManifestImageRow` takes identity from `lambda.*`, `ImageJourneyItem.Output` is null for KO).
- Field initializers kept (`string.Empty`, `1.0`, `[]`) — carried over verbatim. The no-shadow-defaults rule scopes to **config classes**, not contract/model records; changing these would change manifest output.

**Acceptance — met:** build 0/0; suite 370 passed / 0 failed (incl. 2 new Export tests covering the two-writer contract — the regression this fold could silently introduce); CiMini evidence run confirms 14/14 manifest rows carry `TransformerType` + `TransformationStatus` from `OutputRecord` (9× `Tx_CropSquare`, 5× `Tx_CenterAndStretch`, all Ok).

**Files:** `jb/src/core/Models/ImageRecord_OUTPUT.cs`, `ImageRecord_LAMBDA.cs`, `Prism.Core.Contracts.csproj`, `Services/Transform/**`, `lib/Export/Exporter.cs`, Transform/Export tests, `jb/docs/{GLOSSARY,PRISM-models,PRISM-index,PRISM-knowledge-base,PRISM-transform-generate,PRISM-workbench}.md`.

---


### T-3400 · Web workbench: dark mode, layout compaction, import/export feedback
**Status:** Done (2026-07-14) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-14)
**Tracks:** root `jbtodo.md` — web-workbench refinement, triaged 2026-07-10.

**Outcome (2026-07-14):** implemented in `403ed16`; review found the dark theme shipped three theme-bypassing hardcoded colors, fixed in `f9df410`:
- `.workbench-shell-dragging .drop-zone` hardcoded `#fff5e7`, so the drag-over title (inheriting near-white `--prism-color-ink`) rendered at ~1:1 contrast in dark mode — effectively invisible. Now `var(--prism-color-surface-strong)`: 13.5:1 light / 8.9:1 dark.
- `.drop-zone` grid pattern hardcoded the *light* accent teal at 8% opacity, rendering the grid invisible on the dark surface. Now `var(--prism-color-line)`.
- `.error-detail` hardcoded `rgba(255,255,255,0.64)`. Now `var(--prism-color-surface)`.

The dark *palette* itself was complete throughout — all 15 semantic tokens are mirrored across the light `:root`, `@media (prefers-color-scheme: dark)`, and both `[data-theme]` blocks. The bugs were purely values that escaped the variable system.

**Accepted as-is (user decision, 2026-07-14):** `.primary-button`/`.action-button` use `color: white` on `--prism-color-accent` (pink-500 `#d43d78`) = 4.43:1 in dark mode, marginally under the 4.5:1 AA bar for normal text. Judged close enough; the accent is a brand color.

**Scope narrowed (user decision, 2026-07-14):** item 4's "accepted/rejected counts, blocked-vs-running" requirement is **not** met and was **not** achievable in this ticket. `StageProgress.EmitStarted` is the only place a `PipelineProgressEvent` is ever constructed, and it leaves `CompletedCount`/`TotalCount` null — so the SSE stream carries no such data, and T-3400's file list is web-only. T-3400 closes on the achievable bar (real stage name replaces the placeholder chips); the backend gap is now [[T-4600]].

**Delivered:** dark palette + `@media (prefers-color-scheme: dark)` + `[data-theme]` override pair in `PRISM-theme.css`; tri-state (auto/light/dark) header toggle in `WorkbenchShell.tsx` persisting to `localStorage` (auto correctly *removes* the attribute); `ResultSection` reordered above `RouteSection` and `RouteSection` bounded to one row per stage via `StageRouteList.tsx`; `StatusPanel.tsx` placeholder chips replaced with the real SSE stage name. No Upscale toggle added (negative constraint honored). `npm run typecheck` + `npm run build` green.

**Files:** `jb/src/workbench/web/styles/PRISM-theme.css`, `.../styles/workbench.css`, `.../sections/WorkbenchShell.tsx`, `.../components/StatusPanel.tsx`, `.../sections/StageRouteList.tsx`, `.../sections/UploadSection.tsx`.

---


### T-3900 · Order: `DetermineTieBreaker` rescan can mislabel the deciding tiebreaker
**Status:** Done (2026-07-13) | **Profile:** P1-feature-worker
**Tracks:** `jb/src/core/Services/Matching/Order/jbtodo.md` (triaged 2026-07-11).
**Review:** Approve (2026-07-13)

**Problem:** After a winning image is assigned a det slot, `ImageOrderer.DetermineTieBreaker` rescans the *entire* candidate list for the family to find competitors (same slot, same phenotype rank) and reports the first tiebreaker level where *any* competitor differs from the winner. With 3+ competitors losing for different reasons, this can name the wrong tiebreaker as the deciding one — e.g. it reports "ngp-confidence" because a clearly-losing competitor differs on confidence, when the real closest competitor actually lost on the filename-hint tiebreaker instead. Does not affect the actual `DetOrder` assigned — only the `OrderEvidence.TieBreakerWon` diagnostic text, so this is a manifest-readability/debugging issue, not an output-correctness bug.

**Resolution (2026-07-13):** `DetermineTieBreaker(candidates, winnerIndex, imageAssigned)` scans forward from the winner within its contiguous slot+phenotype-rank block and compares it against the first still-unassigned rival — the immediate runner-up — walking the same level chain as `CompareCandidates`. The full-list rescan is gone as a side effect. Two labels the old chain could not express were added: `filename-ordinal` (the sort compares filenames before source index since T-2820, but the labeller jumped straight to `source-index`), and `none` for a slot whose only other candidates already hold an earlier slot (they left the race when assigned; the old rescan still reported them as beaten rivals). Decision documented in `jb/docs/PRISM-order-rename.md` (Step 4 + "Which tie-breaker the evidence names"); source todo block removed.

**Acceptance:**
- `OrderEvidence.TieBreakerWon` names the tiebreaker that actually decided against the true closest competitor, verified against the counter-example in the source `jbtodo.md` (winner NgpConfidence=5/HintScore=1 vs. a tied-confidence/lower-hint true competitor plus an unrelated lower-confidence non-competitor). ✅ `Run_TieBreaker_NamesTheLevelThatBeatTheClosestRival_NotAFarBehindCompetitor` — reports `filename-hint`; the pre-fix code reports `ngp-confidence`.
- `DetOrder` output unchanged (this is a diagnostic-only fix) — confirm via existing `ImageOrdererTests.cs`. ✅ Full suite 367 passed / 0 failed; every pre-existing DetOrder assertion holds.
- Verification beyond the ticket: reverting `ImageOrderer.cs` to HEAD with the new tests in place fails exactly the 3 tests that encode the bug and passes the 3 that encode already-correct labelling; deleting the `imageAssigned` guard fails exactly `Run_TieBreaker_AlreadyAssignedRivalInsideTheBlock_IsSkippedNotReported` (added after review found that branch uncovered).

**Files:** `jb/src/core/Services/Matching/ImageOrderer.cs`, `jb/src/core/Services/Matching/Order/OrderEvidence.cs`, `jb/src/core/Services/Matching/Order/jbtodo.md`, `jb/src/tests/Prism.Core.Tests/Order/ImageOrdererTests.cs`, `jb/docs/PRISM-order-rename.md`.

---

### T-4540 · Analyzers adopt ConfigLoader; root AnalyzerConfig dissolves
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Found by:** [[T-4500]] (Wave 2, parallel with [[T-4530]]) — unblocked, [[T-4510]] reviewed Approve 2026-07-12.

`FeatureAnalysisService` loads `analyzer_Config.json` sections via `ConfigLoader.Section<T>` instead of `AnalyzerConfig.Load` + `PrismConfigLocator`/`ConfigCache`. Per-section validation moves from `AnalyzerConfig.Validate` into the 9 section `*Config.cs` classes as `IValidatableConfig`; root `AnalyzerConfig.cs` dissolves; `PrismApiConfiguration.Load()` startup validation updated likewise. `analyzer_Config.json` content unchanged.

**Design decision (user, 2026-07-12) — same two-phase shape as [[T-4530]].** `AnalyzerConfig` was three things fused: a deserialization target, a validator, and the parameter bundle threaded into `ImageFeatureAnalyzer.Analyze/Refine`. The ticket dissolves the first two; the third stays, rebuilt as a *composed* type — `AnalyzerParameters` (new, `Analyzers/AnalyzerParameters.cs`), built by `AnalyzerParameters.FromConfig()` (phase 1: `ConfigLoader.Section<T>` per section, each self-validating; phase 2: compose). `FeatureAnalysisService` builds it once in its constructor (so a bad config still kills the host at startup, not mid-job) and passes it down; `ImageFeatureAnalyzer`'s signatures keep their existing arity (`Analyze` 3 params, `Refine` 7) with `AnalyzerParameters` in place of `AnalyzerConfig`. Rejected alternatives: threading the 8 sections as individual parameters (would blow `Refine` out to 12 params), and having `ImageFeatureAnalyzer` self-load each section at its call site (hides the dependency; puts two syscalls per section inside the per-image path). `AnalyzerParameters` is not an `AnalyzerConfig` rename: not JSON-bound, owns no loading and no validation, and every section stays independently loadable without it.

**Acceptance:** build + full suite green (incl. `AnalyzerConfigTests` reworked to per-section loading); startup fail-loud check on a misspelled analyzer key.

**Verified 2026-07-12:** `dotnet build jb/src/PRISM.sln` clean (2 pre-existing warnings, untouched files). Full suite **364 passed / 0 failed**. Startup fail-loud: misspelling `HeroPersonMinArea` → API refuses to boot with *"Cannot load section 'Yolo' of …/analyzer_Config.json: JSON deserialization for type 'Prism.Services.Matching.YoloAnalyzerConfig' was missing required properties including: 'HeroPersonMinArea'"*; restored, `analyzer_Config.json` byte-identical to HEAD.

**Review: Approve (2026-07-12)** — commit `cab930e`. Reviewer diffed all 23 predicates of the deleted root `AnalyzerConfig.Validate()` against the 8 new section `Validate()` methods: every bound, message, and field preserved 1:1, no checks dropped, and the previously-unvalidated leaf fields (`IsIllustration.WhiteChannelMin`, `MinClusterPopulation`, `SubjectGeometry.MinForegroundFraction`/`FallbackConfidence`, the `*.Confidence` fields) remain unvalidated — no checks invented. Fail-fast confirmed at both hosts; `analyzer_Config.json` byte-identical. One non-blocking warning, fixed in follow-up commit: `Analyzers/jbtodo.md` still carried an OPEN todo proposing to centralize the `*Config.cs` classes into a single `AnalyzerConfig.cs` — the exact architecture this ticket removed. Todo closed: decision written to `jb/docs/PRISM-pipeline-core.md` (Configuration Lifecycle → "Loading is two phases"), block removed.

**Files:** `jb/src/core/Services/Matching/FeatureAnalysisService.cs`, `jb/src/core/Services/Matching/Analyzers/*Config.cs`, `jb/src/core/Services/Matching/Analyzers/AnalyzerConfig.cs`, `jb/src/api/PrismApiConfiguration.cs`, `jb/src/tests/Prism.Core.Tests/Analyzers/*`.

---


### T-4530 · Transform adopts ConfigLoader; delete Configure() push-in; migrate CropTransformSettings
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Found by:** [[T-4500]] (Wave 2, parallel with [[T-4540]]) — unblocked, [[T-4510]] reviewed Approve 2026-07-12.

- `TransformService` drops `PrismConfigLocator`/`ConfigCache`/`TransformConfig.Load`; consumers get sections via `ConfigLoader.Section<T>("transform_Config.json", "…")`.
- `Tx_util_BgStretch` / `Tx_LowContrastEnhancement` self-load their section lazily inside the engine (now reachable) → delete `Configure()`, `ResetConfigureForTests()`, `TxConfigureGateTests` (current form), `[Collection("TxStaticConfig")]` on `PipelineIntegrationTests`, and the temporal-coupling landmine note in `Engine/jbtodo.md`.
- **CropTransformSettings migration:** its 4 values move from `Prism_Config.json` (`Transformation.Positioning/Cropping`) into a new `"Crop"` section of `transform_Config.json`; `CropTransformSettings` becomes a `required`-props section class implementing `IValidatableConfig` (ranges from `PrismConfiguration.cs:265-268`); remove the 4 properties + parsing + asserts from `PrismConfiguration.cs` and the keys from `Prism_Config.json`.
- Root `TransformConfig.cs` dissolves (sections load independently; its per-section `Validate` checks move into each section class); `PrismApiConfiguration.Load()` validates each transform section explicitly (fail-fast preserved).

**Design decision (user, 2026-07-12) — load and bundle are two phases, not one.** `TransformConfig` was three things fused: a deserialization target, a validator, and the parameter bundle carried into `ImageTransformer`. The ticket dissolves the first two; the third stays, rebuilt as a *composed* type. So: **phase 1** `ConfigLoader.Section<T>` loads each section independently, each self-validating via `IValidatableConfig`; **phase 2** the loaded sections are composed into `TransformParameters` (new, `Engine/TransformParameters.cs`) via `TransformParameters.FromConfig()`. `TransformService` builds the bundle once per stage run and passes it to `ImageTransformer.TransformImage(lambda, colorMat, headcut, parameters)`; `PrismApiConfiguration.Load()` calls `FromConfig()` as its startup gate. Rejected alternative: having `ImageTransformer` self-load each section at its call site — that hides the dependency and puts two syscalls per section inside the per-image `Parallel.ForEach`. Self-load survives **only** in the two fixed-signature webservice `Process(byte[], int, float)` entry points (`Tx_util_BgStretch`, `Tx_LowContrastEnhancement`), which have no parameter to pass config through — the original reason `Configure()` existed. `TransformParameters` is not a `TransformConfig` rename: it is not JSON-bound, owns no loading and no validation, and every section remains independently loadable without it (what T-4560 and per-section service hosts need). [[T-4540]] mirrors this shape.

**Acceptance:** build + full suite green (no `[Collection]` serialization needed); startup fail-loud check — misspell a key in `transform_Config.json`, `PrismApiConfiguration.Load()` throws naming it, restore; prism-evidence-report transform run shows real transformed output, not vacuous KOs.

**Verified 2026-07-12:** `dotnet build jb/src/PRISM.sln` clean (2 pre-existing warnings, untouched files). Full suite **361 passed / 0 failed**. Startup fail-loud: misspelling `FeatherPx` → API refuses to boot with *"Cannot load section 'BgStretch' of …/transform_Config.json: JSON deserialization for type 'Prism.Services.Transform.BgStretchConfig' was missing required properties including: 'FeatherPx'"*; restored. prism-evidence-report (CiMini, `transform`): **14/14 images Succeeded, 0 KO, 0 failed, 0 warnings** — 5× `Tx_CenterAndStretch` (background-stretch fill, scale 0.988–0.992 driven by the migrated `Crop.WhiteSpaceMargin`=0.042) and 9× `Tx_CropSquare`. Not vacuous. `Tx_DetailCropper` stays uncovered — `BypassPhenotypes` PoC gate, pre-existing.

**Review: Approve (2026-07-12)** — commit `4380cea`. Reviewer confirmed byte-exact preservation of the moved range checks (incl. `AssertInRange`'s inclusive bounds and the 0.49 margin cap), the migrated `Crop` values, no shadow defaults, and no unauthorized contract changes. Two non-blocking findings, both fixed in follow-up commit: (1) `jb/docs/PRISM-knowledge-base.md` still listed the deleted `Transformation.*` keys as live `Prism_Config.json` paths; (2) `Tx_LowContrastEnhancement.ApplyClahe` self-loaded its section and was shared by the dormant internal `Enhance()` — wiring `Enhance()` into `Tx_CenterAndStretch` (as its own doc comment invites) would have reintroduced per-image config loading inside the `Parallel.ForEach`, the exact anti-pattern this ticket's design decision rejects. The self-load is now confined to the webservice `Process()` body; `Enhance()` and `ApplyClahe` take config from the caller.

**Files:** `jb/src/core/Services/Transform/TransformService.cs`, `jb/src/core/Services/Transform/ImageTransformer.cs`, `jb/src/core/Services/Transform/Engine/*.cs`, `jb/src/core/config/transform_Config.json`, `jb/src/core/config/Prism_Config.json`, `jb/src/core/config/PrismConfiguration.cs`, `jb/src/api/PrismApiConfiguration.cs`, `jb/src/tests/Prism.Core.Tests/Transform/*`.

---


### T-4520 · Transform layout cleanup + delete dead BackgroundType
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Found by:** [[T-4500]] (Wave 1, parallel with [[T-4510]]).
**Review:** Approve (2026-07-12)

- Move `Engine/TransformationStatus.cs` → `Transform/Enum/TransformationStatus.cs` (fix its `<Compile Link>` in `Prism.Core.Contracts.csproj`).
- Move `Engine/processingtools/Tx_LowContrastEnhancement.cs` → `Engine/Utils/` (fix `Prism.Core.Images.Transform.csproj`; delete empty `processingtools/`).
- `Tx_CenterAndStretch`/`Tx_CropSquare`/`Tx_DetailCropper`/`Tx_ProblemImageProcessor`/`Tx_util_BgStretch`/`Tx_util_HeadCutter` stay in `Engine/` (key human-developer files).
- Delete `Engine/BackgroundType.cs` + its Contracts csproj link. Verified dead: only references are the enum, the csproj link, and a test *method name*; runtime background typing already flows as the `"background-type"` feature-snapshot string (`ImageFeatureAnalyzer.AnalyzeBackground` → `Analyzer_Exposure`). Pure deletion, no rewiring needed.
- Delete `Services/Transform/DUMMY FOLDER/` — its goal.md content is captured in [[T-4500]].

**Acceptance:** build + full suite green; `git grep BackgroundType` returns only the unrelated test method name (rename it while there); no orphan csproj links.

**Files:** `jb/src/core/Services/Transform/Engine/TransformationStatus.cs`, `jb/src/core/Services/Transform/Engine/processingtools/Tx_LowContrastEnhancement.cs`, `jb/src/core/Services/Transform/Engine/BackgroundType.cs`, `jb/src/core/Models/Prism.Core.Contracts.csproj`, `jb/src/core/Services/Transform/Engine/Prism.Core.Images.Transform.csproj`.

---


### T-4510 · ConfigLoader core: section-aware JSON loading in the shared Contracts assembly
**Status:** Done (2026-07-12) | **Profile:** P4-critical-architecture
**Found by:** [[T-4500]] (Wave 1, parallel with [[T-4520]]).
**Review:** Approve (2026-07-12)

Create, in `jb/src/core/config/` (one type per file), namespace `Prism.Config`, compiled into `Prism.Core.Contracts.csproj` via `<Compile Link>`:
- **ConfigLoader.cs** — `T Section<T>(string configFileName, string sectionName)` (parses file once, deserializes ONLY that top-level section; missing section throws naming file + section + the sections that DO exist), `T Root<T>(string configFileName)`, `string RequireFile(string configFileName)` (discovery; throws listing every searched path). Discovery order ports `PrismConfigLocator`: `AppContext.BaseDirectory/config`, `AppContext.BaseDirectory`, cwd variants, source-tree walk-up to `jb/src/core/config/`. Serializer: `PropertyNameCaseInsensitive`, `ReadCommentHandling.Skip`, `required`-member enforcement (no-shadow-defaults core rule). Internal cache keyed `(type, path, section, LastWriteTimeUtc)` — absorbs `ConfigCache` semantics.
- **IValidatableConfig.cs** — `void Validate();` called by the loader after deserialize when implemented.
- **ModelAssetLocator.cs** — ports `FindModelAsset` (beside-config → `PRISM_ONNX_MODEL_DIR` → source-tree walk-up).

**Scope boundary:** NO adoption — no existing call site changes in this ticket. Replace the empty untracked `ConfigLoader.cs` placeholder with the real implementation.

**Acceptance:** new `ConfigLoaderTests` suite (`PrismCoreTests.Services`) covering: missing file lists searched paths; missing section names existing sections; misspelled key throws; comments + case-insensitivity accepted; unchanged file returns cached instance; touched timestamp re-parses; source-tree walk-up works; `IValidatableConfig.Validate` invoked and failures propagate. Build + full suite green.

**Files:** `jb/src/core/config/ConfigLoader.cs`, `jb/src/core/config/IValidatableConfig.cs`, `jb/src/core/config/ModelAssetLocator.cs`, `jb/src/core/Models/Prism.Core.Contracts.csproj`, `jb/src/tests/Prism.Core.Tests/Services/ConfigLoaderTests.cs`.

---


### T-4300 · Strip shadow defaults from Analyzer config classes: required keys, analyzer_Config.json is the only source
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-12) — strip complete (verified by grep), analyzer_Config.json untouched, test values match JSON exactly; full suite 349/349 with both changesets
**Found by:** [[T-4200]] shadow-defaults policy decision (2026-07-12).

**Problem:** The Analyzer config classes carry in-code property initializers ("defaults mirror the previously hard-coded constants"). A missing or misspelled key in `analyzer_Config.json` silently falls back to the in-code value — two sources of truth, and the losing one wins silently. The shadow-defaults core rule (CLAUDE.md, Configuration-driven design) now forbids this for Transform and Analyzers.

**Done:** Every property in all 9 Analyzer config classes (root sections and Palette included) is `required` with zero initializers; `analyzer_Config.json` unchanged (already carried every key); Palette's OrdinalIgnoreCase comparer removal verified behavior-neutral (sole consumer enumerates only). `AnalyzerConfigTests` added: shipped-value fidelity, missing-file, missing-key, out-of-range. Implementation commit 7fbe938.

**Files:** `jb/src/core/Services/Matching/Analyzers/*Config.cs`, `jb/src/tests/Prism.Core.Tests/Analyzers/AnalyzerConfigTests.cs` (new), 3 Analyzers + 1 Classify test files (constructor call sites).

---


### T-4200 · Transform engine config retrofit: extract Tx_* empirical tunables to transform_Config.json
**Status:** Done (2026-07-12) | **Profile:** P1-feature-worker
**Review:** Approve (2026-07-12, 2nd round; 1st round Request Changes) — gate tests added, DetailCropper re-diff minimal, Configure() retained on assembly-boundary grounds (reviewer's lazy-load recommendation withdrawn after csproj verification); xUnit cross-collection race closed via shared [Collection("TxStaticConfig")]
**Found by:** 2026-07-11 config-rule audit (review-gap discussion) — Transform never got the config extraction the Analyzers got.

**Done:** All 11 empirical tunables moved to `transform_Config.json` (values byte-for-byte); 6 new `required`/no-default config classes; wired via ConfigCache like AnalyzerConfig + API startup validation; `Configure()` gate on the two fixed-signature webservice entry points is boundary-forced (Engine references only Contracts — self-load via Prism.Core types would be circular) and documented in `Engine/jbtodo.md` for the future webservice host. `TransformConfigTests` + `TxConfigureGateTests` added. Full suite 349/349. Implementation commit c0b1b42.

**Files:** `jb/src/core/Services/Transform/Engine/` (Tx_* + 6 config classes + AssemblyInfo), `jb/src/core/config/transform_Config.json`, `jb/src/core/Prism.Core.csproj`, `jb/src/api/PrismApiConfiguration.cs`, Transform test suite + `PipelineIntegrationTests.cs`.

---


### T-3200 · Close Services test coverage gaps: `IIngestService` IO/import path + `IArtifactStore`
**Status:** Done (2026-07-10) | **Profile:** P1-feature-worker
**Tracks:** `jb/src/core/Services/jbtodo.md` (per-service test suite todo, triaged 2026-07-07).

**Problem:** Existing test folders already mirror stage boundaries by namespace (`PrismCoreTests.Transform`, `.Match`, `.Classify`, etc.), so per-stage isolation already works today via `dotnet test --filter "FullyQualifiedName~PrismCoreTests.<Stage>"` — no restructuring needed for that. But two service interfaces have no real coverage:
1. `IIngestService` — `Excel/` tests only exercise Excel parsing/IEM building (`ModelBuilder*Tests.cs`). Nothing tests the IO/import side documented in `jb/docs/PRISM-io-import.md` and implemented in `jb/src/core/IO/Import/Importer.cs` — multipart, ZIP, URL, and stream ingestion paths.
2. `IArtifactStore` — `LocalArtifactStore` (`jb/src/core/Services/LocalArtifactStore.cs`) has no direct unit tests; it's only exercised indirectly through `Export/ExporterTests.cs`.

**What to do:**
1. Add a `jb/src/tests/Prism.Core.Tests/Ingest/` folder (namespace `PrismCoreTests.Ingest`) covering `Importer.cs`'s multipart, ZIP, URL, and stream code paths — success and malformed-input cases for each.
2. Add direct unit tests for `LocalArtifactStore`: put/get roundtrip, missing-key behavior, concurrent writes if applicable.
3. Keep the existing per-folder namespace convention consistent (`PrismCoreTests.<Folder>`).

**Acceptance:**
- New tests fail if the corresponding production code is reverted (real behavioral coverage, not vacuous passes).
- `dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj` green.

**Done.** Added `jb/src/tests/Prism.Core.Tests/Ingest/` (`ImporterFixture.cs`, `ImporterDirectImageTests.cs`, `ImporterZipTests.cs`, `ImporterExcelRoutingTests.cs`, `LoopbackHttpServer.cs`, `FetcherTests.cs`) covering multipart/ZIP/URL/stream ingestion, and `Services/LocalArtifactStoreTests.cs` for direct `IArtifactStore` coverage. Closed the per-service test-suite `jbtodo.md`.

**Files:** `jb/src/tests/Prism.Core.Tests/Ingest/*.cs`, `jb/src/tests/Prism.Core.Tests/Services/LocalArtifactStoreTests.cs`, `jb/src/core/IO/Import/Importer.cs`, `jb/src/core/Services/LocalArtifactStore.cs`.

---


### T-3100 · Bracket 4 (SemanticMatcher) perf: skip without CLIP tags; index its string scoring
**Done.** `ImageMatcher.RunWaterfall` skips `RunBracket4` entirely when no record has an influential CLIP tag. `StringMatcher.ScoreCandidatesByStringTokens` rewritten to reuse Bracket 3's inverted token index instead of an un-indexed per-family scan. 18 tests. Verified identical `FamilyId` assignments with/without `--skip-classification` on real TinyTest data.

**Files:** `jb/src/core/Images/ImageMatcher.cs`, `jb/src/core/Images/Match/SemanticMatcher.cs`, `jb/src/core/Images/Match/StringMatcher.cs`

---

### T-3000 · Parallelize image import normalization
**Done.** Both image loops now normalize via `Parallel.ForEach` capped at `Environment.ProcessorCount`; result accumulation moved to `ConcurrentBag<T>`; filename-uniqueness index moved to a job-scoped `Interlocked` counter. Already-conforming JPEGs are copied unchanged instead of decoded/re-encoded. `jb/src/core/IO/Import/jbtodo.md` closed and removed.

**Files:** `jb/src/core/IO/Import/Importer.cs`

---

### T-2810 · PipelineIntegrationTests hard-depend on an uncommitted dataset
**Done.** `ResolveTestFixturePath()` rewritten to walk up to `test/datasets` keyed by the committed `CiMini` folder (no hardcoded path). All fixture references (`SPACINI29/TINY`, `SPACINI29-INPUTS.xlsx`, `SmallTest/*`) repointed to CiMini. CI `--filter` exclusion removed from `ci.yml`. Post-T-2800: all 12 `PipelineIntegrationTests` methods green with `Transform=true` against real CiMini fixture.

**Files:** `jb/src/tests/Prism.Core.Tests/PipelineIntegrationTests.cs`, `.github/workflows/ci.yml`

---

### T-2800 · API/in-process pipeline never initializes the GPU Real-ESRGAN upscaler
**Done.** `PipelineServiceFactory.CreateInProcess`/`CreateFromEnvironment` now call `UpscaleService.Create(configuration)` once (mirrors MatchingService/CLIP eager-init); missing model asset degrades to CPU. `Upscaler_g_p_u.Initialize` made idempotent, thread-safe (`_sessionLock`, serializes `session.Run()`) and non-throwing (`IsReady`); `ImageUpscaler.Upscale` routes to GPU only when hardware present *and* session loaded. Fix exposed second bug: committed model has fixed `[1,3,64,64]` input — added overlapping-tile inference (`RunTiled`/`RunSingleTile`, 8px border discard, shape from `session.InputMetadata`). 224/224 tests green (was 9 failing); live CiMini Full run via API completes with real GPU-tiled output. `expected-manifest.json` not committed — non-determinism filed as T-2820, det8 numbering as T-2830.

**Files:** `jb/src/core/Services/PipelineServiceFactory.cs`, `jb/src/core/Images/ImageUpscaler.cs`, `jb/src/core/Images/Upscale/Upscaler_g_p_u.cs`, `jb/src/tests/Prism.Core.Tests/Upscaler_g_p_uTests.cs`

---

### T-2700 · Wire fetcher strategies into API ingress
**Done.** `FetchDispatcher` created — ordered strategy list with `CanHandle`/`FetchAsync`. `AddRemoteInputRecords` made async; routes via dispatcher first (content-type based), falls back to URL extension. Dropbox folder ZIPs routed to `zipFiles`. `PrismApiConfiguration` carries `FetchDispatcher` instance.

---

### T-2500 · GPU upscaler (Real-ESRGAN via DirectML)
**Done.** `Upscaler_g_p_u.RunRealEsrgan` implemented: JPEG decode → BGR float32 NCHW [1,3,H,W] → `InferenceSession.Run` with DML EP → output [1,3,H×2,W×2] → clamp [0,1] → BGR uint8 → JPEG bytes. Model path from `Prism_Config.json Upscale.ModelPath`.

---

### T-2400 · Cross-bracket tie accumulator
**Done.** `RunWaterfall` maintains `crossBracketCandidates` (per-image `HashSet<string>`). Brackets 1+2 populate from `tiedCandidates`; Bracket 3 adds candidates rejected by duplicate-phenotype guard. `KoUnmatched` emits `MATCHES_MULTIPLE_FAMILYIDS` (≥2 candidates) vs `MATCH_NOT_FOUND` (0). Two `AccumulateCandidates` overloads added.

---

### T-2300 · User decisions: detail crop saliency, headcut, greedy crop
**Done.** Three product decisions recorded in jbtodo.md: BoundingBox from ImagePreProcessor is the sole saliency anchor; Headcut controlled by a bool threaded through the pipeline (from `has-human`); greedy crop aligns bbox center to canvas center with `Tx_util_BgStretch` background fill.

**Files:** `jb/src/core/Images/Transform/jbtodo.md`

---

### T-2200 · Spec and implement Tx_util_HeadCutter
**Done.** Algorithm B (full-image Haar face search, centroid Y < 50%, pick face furthest from top, cutY = face.Y + 0.75×face.Height) implemented. Algorithm A (anatomy-ratio guided search) deferred — jbtodo recorded.

**Files:** `jb/src/core/Images/Transform/Tx_util_HeadCutter.cs`

---

### T-2100 · Implement Tx_DetailCropper pixel flow
**Done.** Full 6-branch decision tree covering every bbox edge-intersection pattern. Crop-sizing driven by `Transformation.Cropping` config via new `CropTransformSettings` struct. 29 tests, including regression tests for two coordinate-shift bugs found during implementation. Verified against real TinyTest fixture image.

**Files:** `jb/src/core/Images/Transform/Tx_DetailCropper.cs`, `CropTransformSettings.cs`, `IImageTransformation.cs`, `ImageTransformer.cs`, `jb/src/core/Services/TransformService.cs`, `jb/src/core/config/PrismConfiguration.cs`

---

### T-2000 · Implement Tx_CenterAndStretch pixel flow
**Done.** Full `Transform()` + `Process()` pixel flow implemented and build clean. Headcut via `Tx_util_HeadCutter` when requested; background fill via `Tx_util_BgStretch.Stretch()`. Canvas math amended after T-2100/T-3100 verification: crop to bbox, resize to margin-adjusted target size preserving aspect ratio, center on canvas, then stretch background (guarantees non-negative placement offset).

**Files:** `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs`

---

### T-1900 · Tx_LowContrastEnhancement
**Done.** CLAHE (Contrast Limited Adaptive Histogram Equalization) via OpenCVSharp4, applied to full image. Dual-interface signature `Process(byte[] arr, int stride, float upscale_factor)`.

---

### T-1800 · ProductTypeId write to ImageRecord_LAMBDA
**Done.** `lambda.ProductTypeId = productTypeId;` added in `ImageOrderer.ProcessFamily` write-back loop. `ResolveProductType()` reads from Excel IEM dynamic columns and normalizes to kebab-case against `DetOrderRules.json`.

---

### T-1700 · Tx_util_BgStretch
**Done.** Tiered background fill: ≤125% edge clamp, ≤142% content-aware extension, >142% INPAINT_TELEA, >250% solid white. Seam feathering after tiers 1 and 2. `Process(byte[] arr, int stride, float upscale_factor)` dual-interface signature.

---

### T-1600 · SD-8: ImageRecord_OUTPUT Width/Height/Checksum
**Done — not a bug.** Fields are declared on `ImageRecord_Base` and inherited by all `ImageRecord*` types. No code changes.

---

### T-1500 · Split StageShells.cs
**Done.** `StageShells.cs` deleted. Eight `ShellStage_Xyz.cs` files created in `jb/src/core/Pipeline/` (one per stage). `Prism.cs` call sites updated to new class names.

---

### T-1400 · Fetch_DropBox
**Done.** Public shared links (`dropbox.com/s/...?dl=0`) normalized to `?dl=1` and delegated to `Fetch_HTTPS_DirectFile`. `dl.dropboxusercontent.com` URLs pass through unchanged. Private OAuth deferred (out of scope V1).

---

### T-1300 · Fetch_HTTPS_DirectFile
**Done.** `Fetch_HTTPS_DirectFile.cs` streams direct HTTPS downloads to `%TEMP%/prism/{jobID}/`, validates URL against `HostRules.json` (scheme, blocked hosts, redirect limit, timeout), returns `ImageRecord_INPUT`.

---

### ONNX Singleton (M5 gate item)
**Done (2026-06-29).** `InferenceSession` hoisted from per-job to application-scoped singleton on `MatchingService`. `ClassificationService` now borrows the shared `ImageClassifier` (no longer owns/disposes it). `_clipLock` on `MatchingService` serializes all `Run()` calls (required for DML). Disposal chain: `MatchingService` → `Pipeline` → `PrismService` (all now implement `IDisposable`). PRISM-classify.md updated. Verified: two TinyTest jobs, CLIP tags in Lambda documents, probe fired once at startup.

---
