# How to add a phenotype (or analyzer, or feature, or det-order rule)

This is a plain-English walkthrough of every file you touch to add something new to the
ImageNGP system — the mechanism that turns raw pixels into a routing/ordering decision for one
image. Read this before adding a new `Analyzer_*.cs`, a new feature id, a new phenotype, or a
new det-order mapping. It exists so per-category special cases get expressed as **config**, not
as new code scattered through the pipeline.

## The five layers

```
Analyzer (.cs)  →  Feature (ImageNGP.json)  →  Phenotype rule (ImageRoles.json)
    →  Det-slot mapping (DetOrderRules.json)  →  (rarely) Transform routing
```

| Layer | File(s) | What it owns |
|---|---|---|
| Analyzer | `jb/src/core/Services/Matching/Analyzers/Analyzer_X.cs` | Measures one thing from pixels/detections and writes it to the snapshot |
| Feature | `jb/src/core/config/ImageNGP.json` | Declares the feature's id, datatype, and allowed values |
| Phenotype rule | `jb/src/core/config/ImageRoles.json` | Combines feature values into a named, compound image type |
| Det-slot mapping | `jb/src/core/config/DetOrderRules.json` | Per-product-type: which phenotypes qualify an image for which output position |
| Transform routing | `jb/src/core/Services/Transform/ImageTransformer.cs` | Rarely touched — see the warning at the end |

`ImageNgpValidator` (`jb/src/core/lib/ImageNGP/ImageNgpValidator.cs`) runs at startup and checks
the first four layers against each other: every feature id and value used in `ImageRoles.json`,
`DetOrderRules.json`, and `ClipPrompts.json` must exist in `ImageNGP.json`. A typo is caught at
boot, not silently evaluated to `UNKNOWN` and never matched at runtime.

## 1. Adding a new analyzer

An analyzer is a static class with an `Analyze(...)` method that writes to an
`ImageFeatureSnapshot` via `snapshot.Set(featureId, value, confidence, source)`. Steps:

1. Create `Analyzer_X.cs` in `jb/src/core/Services/Matching/Analyzers/`, one type per file, K&R
   braces, following the existing analyzers as a template (e.g. `Analyzer_Exposure.cs` for a
   simple one, `Analyzer_SubjectGeometry.cs` for one that needs a detection box).
2. If it needs tunable numbers, add a `Config` nested class (see `Analyzer_Exposure.Config` for
   the pattern) and a matching section in `jb/src/core/config/analyzer_Config.json`, then add the
   new field to `AnalyzerParameters.cs`'s properties and its `FromConfig()` loader — **no
   in-code defaults**; every value must come from the JSON or fail loud at startup (repo-wide
   config rule, see root `CLAUDE.md`).
3. Wire the call into `ImageFeatureAnalyzer.Refine()` (`jb/src/core/Services/Matching/Classify/`),
   in whichever wave makes sense — wave 1 is IEM/filename evidence, wave 2 is human detection,
   wave 3 is everything downstream of the subject box.
4. Anything your analyzer doesn't confidently set stays `UNKNOWN` — don't guess a default.
   `RecordUnknownFeatures()` in the same file back-fills `UNKNOWN` for any feature nobody set.

**Do not build a stub.** An empty-body analyzer that "will be implemented later" makes every
phenotype requiring its feature permanently unreachable (see "the UNKNOWN trap" below) — T-4700
deleted 10 of exactly this shape. If you can't implement it now, don't declare its feature in
`ImageNGP.json` either; add both together when the real logic exists.

## 2. Declaring a new feature

Add one entry to the `features` array in `ImageNGP.json`:

```json
{ "id": "my-new-feature", "datatype": "boolean", "values": ["true", "false", "unknown"] }
```

`datatype` is `boolean`, `enum`, `integer`, `float`, or `string`. For `boolean`/`enum`, list every
allowed value — `UNKNOWN`/`unknown` is always implicitly accepted on top of what you list, so
don't repeat it unless the feature's own convention includes it explicitly (compare
`hero-is-human`'s `UNKNOWN` enum member against `intersects-top`'s plain `true`/`false`, where
`UNKNOWN` is never actually produced).

## 3. The UNKNOWN trap — why phenotype rules matter

`PhenotypeRuleSet` (`jb/src/core/Services/Matching/Classify/PhenotypeRuleSet.cs`) never treats
`UNKNOWN` as satisfying a `required` condition. This means: a feature that's only ever `UNKNOWN`
(no analyzer sets it, or the analyzer is a stub) makes every phenotype that hard-requires it
**permanently unreachable** — not "rare," not "needs calibration," genuinely impossible for any
image. Before writing a phenotype rule, make sure every feature it hard-requires already has a
real producer (an analyzer, or a `ClipPrompts.json` entry).

This is different from a feature that's real but just conservatively tuned — a confidence
threshold set to 0.9 that rarely fires is a calibration question, not a reason to avoid using
the feature. Only a feature with **no possible producer at all** is a trap.

## 4. Writing a phenotype rule

Add an entry to the `phenotypes` array in `ImageNGP.json` (just the id), then a matching rule
object in `ImageRoles.json`:

```json
{
  "id": "my-phenotype",
  "required": [
    { "feature": "hero-is-human", "equals": "FALSE" },
    { "anyOf": [
        { "feature": "background-type", "equals": "SOLIDCOLOR" },
        { "feature": "white-background", "equals": "true" }
    ]}
  ]
}
```

- `required` is a hard AND — every entry must hold.
- `anyOf` is an OR-group nested inside `required` — at least one of its children must hold.
- **Array order matters.** `PhenotypeRuleSet.Assign` evaluates rules top-to-bottom and the first
  fully-satisfied rule wins — there is no priority field, no specificity scoring. If two rules
  can both match the same image, the one earlier in the file always wins. Place more specific
  rules before more general ones when they can overlap.

## 5. Wiring a phenotype into ordering

`DetOrderRules.json` has one table per product type, each with numbered det-slots:

```json
"my-product-type": {
  "det0": { "keyword": "front", "phenotypes": ["my-phenotype", "front-packshot"] }
}
```

- `phenotypes` is a ranked preference list for that slot — `ImageOrderer.BuildCandidates` builds
  one candidate per image per qualifying slot, and `CompareCandidates` sorts by slot index first
  (earliest slot wins), then by the phenotype's position within that slot's list (earlier in the
  array wins the tie). An image whose phenotype qualifies for two slots naturally tries the
  earlier slot first and only falls back to the later one if a stronger rival already claimed it
  — you don't need to remove a phenotype from a later slot just because you added it to an
  earlier one.
- `keyword` is an independent filename-hint mechanism (`ImageOrderer.ResolveHintSlot`) — it still
  anchors overflow images to roughly the right position even when no phenotype matches at all.
  An empty `phenotypes: []` array is legal; the slot then relies purely on the keyword.
- A product type not listed in `DetOrderRules.json` falls back to `"default"`.

## 6. Transform routing — rare, and never unilateral

Most phenotypes only affect ordering, not pixel processing. `ImageTransformer.cs` currently
routes almost entirely on geometry (bounding box, edge intersections), with only a narrow
phenotype-driven branch for `Tx_DetailCropper`.

**Never invent a new `Tx_*.cs` transform class to handle a specific
phenotype/product-type/det-order combination on your own judgment.** It's fine to *suggest* one
if an existing `Tx_*` class genuinely can't do what's needed — but building it, and wiring its
routing in `ImageTransformer.SelectTransformer`, requires the user's explicit sign-off every
time, including sign-off on exactly which combinations map to it. The default extension point
for new per-category behavior is a switch *inside* an existing `Tx_*` class on
`ImageRecord_LAMBDA` fields (`ProductTypeId`, `DetOrder`, `hero-is-human`, etc.) — not a new file.

## Worked example — the "hero" image (illustrative only)

This section is a teaching example of why one real-world concept decomposes into several
phenotypes. It does **not** describe what's currently implemented in `ImageRoles.json` — treat it
as a demonstration of the mechanism, not a spec to copy verbatim.

The idea "the hero shot" means different things depending on what's in the image:

- For the `default` product type, a hero image has **no edge intersections at all** and shows
  the front view: `hero-orientation = FRONT`, `intersection-count = 0`.
- If a human is detected (`hero-is-human = TRUE`), top and/or bottom intersection becomes
  acceptable — a person's feet or head can be cropped without it stopping being a hero shot.
- If a human is detected but no face is detected, a *single* side intersection (left OR right,
  but never both) becomes acceptable too — e.g. a person turned slightly so one arm crosses the
  frame edge, but never both sides at once (that would read as a broken crop, not a deliberate
  composition). Note: a `face-visible`-style feature is used here purely to illustrate the
  *pattern* — it isn't part of the current taxonomy (T-4700 removed it along with its stub
  producer); a real feature filling this role would need a real analyzer first, per section 3.
- For the t-shirt product category specifically, a hero image may have **both** top and bottom
  intersection (a t-shirt hero conventionally excludes feet and sometimes the top of the head).

Each of these is a **different phenotype** — distinguished by `hero-is-human`, whichever face/pose
signal is actually available, which `intersects-*` features are true, and (via
`DetOrderRules.json`) product type — not one mega-rule with `if productType == "t-shirt"` embedded
inside it. The product-type-specific part lives entirely in which phenotypes a product type's
det-slot accepts, in `DetOrderRules.json` — never as a conditional inside a phenotype rule or
inside C# code.

## Counter-example

A one-off visual quirk specific to a single SKU (e.g. "this one product's photographer always
shoots at a weird angle") is **not** a new phenotype — that's what filename hints or a manual
override are for. A composition pattern that recurs across many SKUs in a category — like the
t-shirt hero-crop convention above — is the right size of thing to model as a phenotype.

## Sequencing note

This system is being deliberately kept small right now: simplify by subtraction first (T-4700
removed 10 stub analyzers and every phenotype/feature that depended only on them), prove a
reliable minimal baseline works, and only then re-expand — one analyzer, one phenotype, one
product-type variant at a time. Don't try to enumerate every product-type/phenotype permutation
in a single change; that combinatorial space is exactly what got this subsystem into trouble the
first time.
