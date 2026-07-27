# Ideas on NGP Classification
Simple-language insights for understanding what NGP classification is and why it works the way it does.
Each insight is written to be explainable to a 10-year-old, then followed by a practical note for developers.

---

## Insight 1 — What is a "phenotype"?

**Simple version:**
Think of how two photos of the same dog can look very different: one shows the dog sitting straight-on with its whole body visible, and another is a close-up of just its paw. Both pictures are of the same dog, but they show it in completely different *ways*. A **phenotype** is the name we give to that *way of showing something* — the combination of angle, zoom, setting, and how visible the subject is.

In PRISM, a phenotype like `front-on-model` means: "a photo of a person wearing the product, photographed from the front, with their whole body in frame." A different phenotype like `detail-material` means: "a zoomed-in photo showing just the fabric or texture."

**Developer note:**
An `ImageNGP` phenotype is derived by combining multiple measured `ImageFeature` values (orientation, human presence, head visibility, background, shot type). It is not stored directly in the image — it is *computed* from features during the Classified stage.

---

## Insight 2 — Why does "human first, artificial last" matter?

**Simple version:**
On a clothing website, the first photo is almost always a real person wearing the item. That is because people immediately want to know: *does this look good on a person my size?* Photos of mannequins come second because they still show fit but feel less real. Flat-lay photos (laid on a table) come third. A plain product shot on white background comes last as a reference. Nobody wants to see the plain white-background photo first — it is boring and unhelpful.

PRISM orders images the same way: real person (on-model) → mannequin (ghost) → flat-lay → plain packshot → render. This mirrors what shoppers actually want to see first.

**Developer note:**
The ordering preference `human → artificial` is encoded in `DetOrderRules.json` and the `HERO_IS_HUMAN` + `TypeOfShot` enum values. `ONMODEL=85` scores higher than `GHOST=70`, which scores higher than `FLAT=55`, which scores higher than a clipping-path packshot. These numeric weights feed the ordering algorithm.

---

## Insight 3 — Why CPU-only?

**Simple version:**
Imagine you want to play a video game but you are only allowed to use the computer your school has, not a fancy gaming PC. PRISM has to work on a normal laptop or company server — not a super-powered machine with a special graphics chip (GPU). So every detection method PRISM uses must work well even on ordinary hardware.

This means PRISM cannot use the fastest, most powerful AI tricks, but it can still be very accurate by using smarter, lighter methods: edge detection, color histograms, skeleton detection, and small ONNX models.

**Developer note:**
CPU-only is a *first-class requirement*, not a fallback. From `PRISM-classify.md`: "CPU is the required baseline. PRISM must run on local servers and laptops without a GPU." Any new feature detector proposed for the pipeline must be benchmarked on CPU. GPU may accelerate but must never be required.

---

## Insight 4 — What is a DetOrder slot?

**Simple version:**
Think of an e-commerce product page as a photo album with numbered pages: page 1, page 2, page 3... Each page has a *job*. Page 1 always shows the front of the product. Page 2 shows the back. Page 3 shows a close-up of an interesting detail. A **DetOrder slot** (like `det0`, `det1`, `det2`) is just the numbered page position in that album, and the *job* is which type of photo should go there.

Different products have different albums. Shoes need a diagonal view at position 1 because shoppers want to see the shape. T-shirts need a front ghost or on-model view at position 1. PRISM reads the rules for each product type and places each image in the right slot.

**Developer note:**
`DetOrderRules.json` maps `ProductType → det# → role keyword`. The current file has 5 rules: `default`
plus 4 product-type-specific rules (`topwear`, `bottomwear`, `footwear`, `bags-accessories`) —
collapsed from 18 product-type-specific rules by a follow-up ticket that merged/retired the rest
(`clothing-dresses` merged into `bottomwear`; the other 13 now fall back to `default`). The keyword (e.g.,
`"front"`, `"diagonal"`, `"detail"`) is matched against derived ImageNGP phenotypes. The ordering stage
in `jb/src/core/Services/Matching/Order/` implements this lookup.

---

## Insight 5 — What does "UNKNOWN" mean and why is it good?

**Simple version:**
Imagine you are sorting a box of Lego pieces but one piece is so dirty you cannot tell what color it is. The right thing to do is put it in a "don't know yet" pile instead of guessing and putting it in the wrong color bin. PRISM does the same thing: if it cannot figure out the orientation of a photo with enough confidence, it labels that feature as `UNKNOWN` instead of guessing `FRONT` or `SIDE`.

This is important because a wrong guess causes the wrong image to end up in the wrong slot, which is worse than admitting uncertainty. `UNKNOWN` stays visible so a human can check it.

**Developer note:**
Every bounded `ImageNGP` enum has an `UNKNOWN` member. When confidence falls below `Classification.Confidence_Threshold` (currently `0.9`), the feature is set to `UNKNOWN`, not to a default value. Unknown transform-critical features route to `Tx_ProblemImageProcessor.cs` for conservative handling. Unknown features are surfaced in the manifest, not silently discarded.

---

## Insight 6 — Why "features → phenotype → DetOrder" instead of "features → DetOrder" directly?

**Simple version:**
Suppose you are a librarian and you want to file a book. You do not look at the book and *directly* decide which shelf — you first figure out: is it fiction or non-fiction? Then: what genre? Then: what author letter? Each step is simpler and easier to check for mistakes than one giant jump from "book" to "exact shelf."

PRISM does the same three-step logic:
1. Measure simple facts about the image (features): "has a person," "front view," "full body visible."
2. Combine those facts into a named image type (phenotype): `front-on-model-full-product`.
3. Look up where that image type goes for *this product* (DetOrder): `det0` for a t-shirt.

This three-step approach makes errors easy to find and fix, because you can inspect each level separately.

**Developer note:**
This is the `ImageFeatures → ImageNGP → (ProductType, DetOrder)` pipeline. The phenotype layer decouples feature extraction from ordering rules, making it possible to re-use the same features across many product-type rule sets and to update ordering rules without touching the CV feature detectors.

---

## Insight 7 — What is a "border intersection" and why does it matter?

**Simple version:**
Look at a photo of a person wearing jeans, but the photo is cropped so their feet are cut off by the bottom edge of the image. The jeans are *intersecting* the bottom border. PRISM detects this automatically. Why does it matter? Because if you know the legs are cut off, you also know the image cannot be resized to show the full garment without cropping, so PRISM will handle it differently during transformation.

It also helps classify orientation: if both the top AND bottom edges are intersected by the product, the photo is likely a close-up detail shot, not a full-body shot.

**Developer note:**
Border intersection detection uses Hough line detection on 10%-wide subsampled strips along each image edge. Results are per-edge booleans: `intersects_top`, `intersects_bottom`, `intersects_left`, `intersects_right`. These feed both the human detection stage (partial skeleton inference) and phenotype classification (full-product vs. partial vs. detail).

---

## Insight 8 — Why are phenotypes better than a single "TypeOfShot" label?

**Simple version:**
If someone asked you to describe a photo using only one word — say "shirt" — you would lose a lot of information. Is it a front photo? Is someone wearing it? Is it a close-up? A **phenotype** is like a short sentence instead of one word: "front view, worn by a person, full body visible, studio background." That sentence tells you much more and makes it much easier to decide which slot the photo belongs in.

The existing `TypeOfShot` enum (`PACKSHOT`, `ONMODEL`, `GHOST`, etc.) is a *simplified* version of this idea. The full phenotype system is richer because it combines orientation, human presence, occlusion level, and background into one named type.

**Developer note:**
`TypeOfShot` may remain as one `ImageFeature` contributing evidence, but it is explicitly documented in `PRISM-classify.md` as "not the canonical ImageNGP list." The phenotype system defined in `imagePhenotypes.md` replaces `TypeOfShot` as the primary classification unit for ordering decisions.

---

## Insight 9 — Slots are a competition, not a sorting

**Simple version:**
Imagine a school play with 8 named roles, and 12 kids auditioning. You do not just line the kids up by
height — each *role* has a wish-list of who fits best, and each kid can only get one role. You hand out
roles so the whole play is as good as possible, making sure no role is left empty and no kid is double-cast.

PRISM fills `det` slots the same way. Each slot (det0, det1, …) has a wish-list of photo types it prefers,
every photo in the FamilyID "auditions" for slots, and PRISM hands out slots so each photo gets at most one
slot and the best overall match wins. This is why we solve it as an *assignment*, not a simple sort.

**Developer note:**
Layer B is a minimum-cost assignment (Hungarian / greedy slot-priority) over a sparse preference tensor
`W[ProductType, DetSlot, Phenotype]`. It guarantees distinct images per slot and removes the "one image
claims two slots" bug. See [`NGP-architecture.md`](ImageNGP/NGP-architecture.md) Part 1.

---

## Insight 10 — Guess softly, decide firmly

**Simple version:**
When you are not 100% sure what a photo is, it is smart to keep a few guesses with "how sure" next to each
("80% a front photo, 20% a side photo"). But when it is finally time to put the photo on a page, you must
pick *one* answer — and you should always pick the same way given the same guesses, so the result never
changes randomly.

PRISM does both: inside, it keeps a *score for every phenotype* (soft). At the end, it makes *one firm,
repeatable choice* (deterministic). Soft thinking, firm answer.

**Developer note:**
Phenotype scoring produces a full score vector per image (soft, explainable). Assignment is a pure
function of (features, config) — no sampling, no learned weights — so the same input always yields the same
`_det` output. This is how the system stays both ambiguity-tolerant and deterministic.

---

## Open questions noted during concept development

- How many phenotypes are "stable enough" to be used as hard DetOrder inputs vs. soft scoring inputs?
- Should the `UNKNOWN` phenotype map to a fallback DetOrder slot or be excluded from ordering entirely?
- When two images produce the same phenotype for the same FamilyID, which wins? (Tie-breaking rule not yet defined.)
- Does `ProductType` need to be detected automatically from image features, or is it always supplied via Excel/metadata?
