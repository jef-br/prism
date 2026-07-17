# Matching: What the Code Does vs. What NGP-Architecture.md Says

*Plain-language comparison. Read `currentMatching.md` first to understand what matching actually does.*

---

## First, a Clarification

`jb/docs/ImageNGP/NGP-architecture.md` is **not about matching**.

It's about a different part of the pipeline: how images are **classified** (what type of image is it?) and **ordered** (which det-slot should it go in?). Specifically it covers:
- **Layer A**: measuring image features (orientation, background, human presence, etc.) → deciding what "phenotype" (image type) each photo is
- **Layer B**: using product type + image type to pick the right `_det0`, `_det1`, `_det2` slot
- **Part 3**: a workbench visualization concept for debugging those decisions

Matching (connecting a photo to its FamilyID) is specified in `PRISM-match.md`, not in NGP-architecture.md.

**So why compare them at all?**

Because the two stages are neighbors in the pipeline, and NGP-architecture.md makes four assumptions about matching — either explicitly or by implication. Where the code does something different, those differences matter.

---

## Connection 1 — The Phenotype Guard in Bracket 3

**What NGP-architecture.md says (implied):**

NGP-arch describes a world where each image gets a phenotype (e.g., "hero front view", "packshot", "detail closeup"). Within a FamilyID, each det-slot holds a *different* phenotype. The whole assignment model assumes that no two images of the same type end up in the same product family.

That means: when matching a photo to a FamilyID, you should check whether that family already has a confirmed photo of the *same type*. If yes, don't accept the match — it would create a conflict.

**What the spec says (`PRISM-match.md`):**

Bracket 3 (the word-matching step) should only accept a match if:
1. Exactly one FamilyID has matching words, **AND**
2. That FamilyID does **not** already have a confirmed photo of the same image type.

**What the code does:**

Condition 1 is implemented. Condition 2 is **not**. There is no phenotype check in `StringMatcher.TryMatch()`. Two "hero front" photos can both match the same FamilyID through Bracket 3, and neither will be rejected.

**What breaks:**

The Ordering stage will later receive two images claiming the same phenotype for the same FamilyID. It has to resolve this itself, which it can do — but it means the problem lands in a later stage instead of being caught early where it's easiest to understand and fix.

**Status:** Gap. Tracked in `jb/src/core/Services/Matching/Match/jbtodo.md`.

---

## Connection 2 — What CLIP Is For

**What NGP-architecture.md says:**

CLIP is a gated, last-resort classifier. It only runs when cheaper detectors (geometry, background color, edge analysis) can't make a confident decision. Its job is to produce two image features:
- `product-type-label` — what type of product is in the photo
- orientation tie-break — helps pick between "front" and "side" when geometry alone can't decide

Those outputs feed the phenotype scoring (Layer A). CLIP doesn't touch matching at all.

**What the code does:**

CLIP is used in **two** ways:
1. Correctly, in the Classify stage: as a gated classifier that produces tags which feed into phenotype detection. This matches NGP-arch exactly.
2. Additionally, in the Matching stage (Bracket 4 / `ImageLabelingMatcher`): the CLIP tags from Classify are checked against the product's color, type, and material columns in the catalog. If they match, a "label evidence" note is added to the MatchEvidence record.

The second use is an **extension** — NGP-arch neither requires it nor prohibits it. It doesn't change which FamilyID the photo is assigned to; it just adds supporting evidence that the match makes sense (e.g., "AI tagged this photo as 'blue', and the matched product is listed as Blue").

**What breaks:**

Nothing. There is no conflict. But this second use of CLIP isn't documented anywhere in the architecture — it's an implicit design decision that lives only in the code.

**Status:** Partial alignment. No action needed, but worth noting for anyone working on the workbench visualization or trying to understand why MatchEvidence has CLIP data in it.

---

## Connection 3 — The Workbench Needs a Complete Evidence Trail

**What NGP-architecture.md says (Part 3):**

The recommended workbench visualization is a **layered factor graph**: a diagram that lets an operator click on any det-slot and trace *exactly* why that image won it — which features fired, which phenotype was scored, which evidence outweighed which. The document explicitly says this graph is "rendered from the same `MatchEvidence` / `ImageRecord_LAMBDA` data the pipeline already emits."

For the matching part of that trace, you'd need to show:
- Why a candidate was *rejected* (was it below the scoring threshold? was it a near-tie that lost?)
- Which matchers ran and what weight each one's evidence carried
- What the runner-up candidates were and why they lost

**What the code does:**

`MatchEvidence.cs` exists and has good basic fields (FinalFamilyId, FinalScore, AcceptedMatcherName, token evidence lists). But it is missing three fields that the factor graph would need:

| Missing field | What it would store |
|---|---|
| `ThresholdStatus` | Whether this candidate passed or failed the scoring threshold, and by how much |
| `RejectedNearTieEvidence` | The runner-up candidates and their scores (so you can see "family A won at 0.85, family B lost at 0.83") |
| Per-matcher weights | The relative weight of each matcher's evidence in the final decision |

**What breaks:**

The workbench factor graph (if/when built) won't be able to show the full matching trace. An operator can see *that* an image matched family A but not *why* family B lost by a small margin. Debugging mismatches requires reading code instead of clicking the UI.

**Status:** Partial gap. Tracked in `jb/src/core/Services/Matching/Match/jbtodo.md`.

---

## Connection 4 — Soft Phenotype Scores

**What NGP-architecture.md says:**

The recommended model is "soft internally, deterministic externally." Each image gets a full score vector — a number between 0 and 1 for *every* phenotype. For example: `{hero_front: 0.91, packshot: 0.72, detail_closeup: 0.04, ...}`. This vector is kept in memory and fed into a min-cost assignment solver that picks the best image for each det-slot across the whole FamilyID.

The point is: instead of saying "this image IS a hero front", you say "this image is 91% likely to be a hero front". The solver can then make globally optimal slot assignments when two images both score 0.80 on the same phenotype.

**What the code does:**

The Classify stage assigns each image a **single hard phenotype label** — one winner, no score vector. For example: `SelectedPhenotype = "hero_front"`. There are no runner-up scores stored.

The Matching stage never sees phenotype scores at all — it only sees the winning label (and only if explicitly looking at it). The Ordering stage also works from hard labels and a priority-ranked list from `DetOrderRules.json`.

**What breaks:**

Nothing at the moment, because the soft-score model hasn't been ratified or implemented anywhere. The hard label works for the existing pipeline. But if a future version wants to implement the NGP-arch min-cost solver for slot assignment, it will need the full score vector — which means changing the Classify stage to output it, and changing the Ordering stage to consume it.

**Status:** NGP-arch proposal, not yet ratified. The current hard-label design is intentional, not a bug. Any decision to move toward soft scores belongs in `jbtodo.md` and requires an explicit product decision.

---

## Summary

| NGP-arch assumption | What spec requires | What code does today | Status |
|---|---|---|---|
| Phenotype guard in Bracket 3 | Reject match if target family already has same image type | No check exists | **Gap** (jbtodo) |
| CLIP used only for phenotype features | CLIP → image features → Layer A phenotype scoring | Also used as label evidence in matching | **Extension** (no conflict) |
| Full MatchEvidence trace for workbench | ThresholdStatus, near-tie evidence, per-matcher weights | Missing 3 fields | **Partial gap** (jbtodo) |
| Soft phenotype score vector | Soft 0–1 score per phenotype, fed to min-cost solver | Hard single-label assignment | **Deferred proposal** (not a bug) |
