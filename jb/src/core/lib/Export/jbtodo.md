# Export / Evidence-Manifest Todo

Split the single `manifest.json` into six purpose-built evidence manifests. Each block below states
the target content, what already exists to build it from, and the open decisions/blockers that must
be settled before implementation.

**Shared decision (applies to all six) — config-driven manifest emission.**
Add a `Manifests` section to `Prism_Config.json`. Per manifest, a small object with three fields:
`fileName`, `content` (short human-readable descriptor of what the manifest holds), and `enabled`
(bool toggle). Every manifest is toggleable **except `prism-manifest.json`, which is mandatory and
carries no toggle** (always emitted). No hardcoded filenames anywhere in `Exporter.cs` — the current
literal `"manifest.json"` at `Exporter.cs:85` moves into config. Config class must follow the
no-shadow-defaults rule (all `required`, fail loud on a missing key), like every other PRISM config.

**Shared decision — one in-memory evidence model, projected to files on export (RESOLVED 2026-07-27).**
PRISM holds a single authoritative per-image ledger in memory for the whole run; each manifest is a
*projection* of it written at export — never six independently-assembled files re-correlated by
filename. `ImageRecord_LAMBDA` already IS that ledger: it accumulates the full journey in one object
(`BoundingBox`/`Features`/`Tags` → `MatchEvidence` → `OrderEvidence`/`ProductTypeId` →
`GenerationRouteState`/`GeneratedChildren` → `ProcessedBytes`/`OutputRecord`). Because each image is
one object, the unified per-image trace is intrinsic and the correlation key never needs to be the
filename — this is why keeping the current key as-is is safe. The finishing work is only: (1) fill the
fields currently computed-and-discarded (upscale scale/resolutions, dedup hashes, transform params via
`SafeSummaryText`); (2) unify the **import-KO identity** with LAMBDA — files that die at import
(corrupt/unsupported) never become LAMBDAs (they are `ImportKoRecord`s), so a fully unified trace needs
those two types to share a common identity so an import-dead file still appears in the same ledger
(this is the real work behind `ingress-manifest.json`, Todo 2); (3) let export project the ledger into
each file. All manifests are assembled at the current single choke point (`Exporter.BuildZip` /
`BuildJson`), reusing the gathered `ExportRequest` — extended to also carry the import-KO records.

-------

## Todo 1 — `prism-manifest.json` (mandatory highlights: exported images + match evidence)

**Target content:** only the exported images. Header counts: `#images`, `#OK`, `#KO`. Per image:
original filename, new filename, and all match evidence.

**What exists:** closest to today's `manifest.json`. `MatchEvidence` (`MatchEvidence.cs`) already
carries the full per-image match evidence (accepted matcher, top candidates, numeric/string token
evidence, CLIP label evidence, tie detection, safe explanation) and hangs off
`ImageRecord_LAMBDA.MatchEvidence`. Today's `ManifestImageRow` is only a thin projection
(`MatchedBy` string, no evidence body).

**Decisions / blockers:**
- **"All evidence for the match" scope.** Embed the whole `MatchEvidence` object per row, or a
  named subset? Recommend embedding `MatchEvidence` wholesale — it is already the bounded,
  safe-to-serialize evidence record, so no new projection type is needed.
- **"Only exported images" definition.** Confirm this means rows where
  `OutputRecord.ExportStatus == "Ok"` (renamed + written files), not all non-KO lambdas. `#KO`
  in the header then counts everything that did not export.
- **`Exporter.cs:85` remains the site** for this one, but the filename is read from the new
  `Manifests` config section (mandatory entry, no toggle).

**Files:** `Exporter.cs`, `BatchManifest.cs` / `ManifestImageRow.cs` (or a new `prism-manifest`
record), `MatchEvidence.cs` (reused as-is), `Prism_Config.json` (new `Manifests` section).

-------

## Todo 2 — `ingress-manifest.json` (ALL source images + dedup evidence + IEM table)

**Target content:** every source image (all of them), image-deduplication evidence, and the final
Internal Excel Model rendered as a table.

**Image dedup already exists — recovered.** It is perceptual-hash dedup, not absent:
- `VisualHasher.cs` (`Services/Matching/Classify/`) computes a 128-bit **dHash** per image and groups
  by Hamming distance. Threshold is config-driven: `MaxHammingDistance` plus
  `ClassifyConfig.json` → `VisualHasher` (`HashWidth`/`HashHeight`).
- `DedupGroup.cs` = `record(Canonical, Duplicates)`; canonical = highest pixel area. Two images group
  only when **both** visually similar **and** sharing the same base filename (different filenames =
  intentional reuse across products).
- Applied in `MatchingService.Deduplicate` (`MatchingService.cs:339`): each suppressed duplicate is
  KO'd with `KoReasonCode = "VISUAL_DUPLICATE"` and
  `KoSafeMessage = "Visual duplicate of {canonical}"`. Configured phenotypes
  (`DeduplicationExemptPhenotypes` — illustrations, tech drawings, labels) are exempt. Hash computed
  once at `MatchingService.cs:278`, shared with feature analysis.
- **Gap for evidence:** the raw dHash values and pairwise Hamming distances are computed then
  discarded — only the KO reason/message survive. Producing rich dedup evidence (canonical hash,
  each duplicate's hash + measured distance) needs those values persisted onto the record instead of
  thrown away.

**"All source images" — where the list lives.** There is **no single pre-KO snapshot** of everything
that entered PRISM. `Importer.Run` (`Importer.cs:64`) takes three inputs — direct `imageRecords`
(remote-fetched media already folded in here upstream by the fetchers), `zipRecords` (expanded inline
into members), `excelRecords` — and returns an `ImportStageResult` split into three disjoint buckets:
`NormalizedImages` (survivors), `ImageKoRecords` (rejected before/at normalization: corrupt,
unsupported, too small/large), `ZipKoRecords`. So the complete "all media as it came in" set is the
**union** of `NormalizedImages` + `ImageKoRecords` + `ZipKoRecords`, reconstructable only at
end-of-import.

**Decisions / blockers:**
- **Assemble vs. capture.** Either (a) union the three `ImportStageResult` buckets to reconstruct the
  full inbound set, or (b) add an explicit "as-received" capture point inside `Importer.Run` before
  any KO filtering. (a) is lower-risk and needs no pipeline change; (b) is cleaner but touches the
  hot import path. Recommend (a).
- **Retention to end-of-job.** `ExportRequest` today carries `NormalizedImages` but **not** the KO
  record collections. To emit ingress-manifest at the Export choke point, `ImportKoRecord` /
  `ZipMemberKoRecord` collections must be threaded through to `ExportRequest`, or ingress-manifest is
  written early as its own artifact at import time. Decide which.
- **Unified identity key.** Survivors key on `InitialFullName`; KO records key on `OriginalFileName`
  (plus `SourceProvenance`). Reconciling casing/sanitization differences needs one shared key.
- **IEM "as a table".** `InternalExcelModel` exposes `RecordsByFamilyID` as a dictionary, and columns
  are dynamic per batch (config-driven classification). Decide the JSON table shape — headers + row
  array, vs. per-family object map. Note `FamilyIDRecord` members must be public for serialization.
- **Dedup-evidence richness.** Minimum viable = the VISUAL_DUPLICATE KO rows already present. Richer
  (hashes + distances) requires persisting the discarded hash values — decide how far to go.

**Files:** `Importer.cs`, `ImportStageResult`, `ExportRequest.cs`, `Exporter.cs`, `VisualHasher.cs` /
`MatchingService.cs` (persist hashes if rich evidence chosen), `InternalExcelModel.cs`,
`FamilyIDRecord.cs`, `Prism_Config.json`.

-------

## Todo 3 — `match-manifest.json` (all ingress survivors + unfiltered analysis)

**Target content:** every image that survives ingress — **including images later KO'd at Match** —
with all analysis information, unfiltered.

**Decisions / blockers:**
- **"Unfiltered" is acceptable, but relabel UNKNOWN.** Where a feature was never measured, the
  snapshot returns the literal `"UNKNOWN"` (`ImageFeatureSnapshot.GetValue`). For this manifest,
  emit those as **`unknown-feature-not-implemented`** rather than bare `UNKNOWN`, so the manifest
  distinguishes "measured as unknown" from "analyzer not yet built". (Backdrop: `RecordUnknownFeatures`
  still stubs 35+ features pending [[T-4000]]; this manifest will faithfully expose that — acceptable.)
- **Enumeration is available.** `ImageFeatureSnapshot.All` exposes the full
  `IReadOnlyDictionary<string, ImageFeatureValue>` (value + confidence + source), so the whole feature
  set can be serialized directly — no keyed-lookup-only limitation.
- **Scope confirmed:** includes images KO'd at Match. This set is therefore strictly larger than
  Todo 1's exported-only set.
- **Do NOT repurpose `ImageJourneyItem` / `ImageStageStep`.** Those are the *bounded, web-safe*
  per-image journey (4 stages: Import/Classify/Match/Transform, each Ok/Skipped/Ko + safe message,
  no internal identifiers) surfaced in `images.ok[]/ko[]` of the JSON result envelope (SD-13,
  workbench visualization). This request needs an *unfiltered* projection with internal analysis
  data — pushing that into `ImageJourneyItem` would contaminate the safe web envelope. Build
  match-manifest as a **separate projection straight off `ImageRecord_LAMBDA`**; it neither hinders
  nor reuses the journey types, and current behavior is undisturbed.

**Files:** `Exporter.cs` (new projection), `ImageFeatureSnapshot.cs` (reused via `.All`),
`ImageRecord_LAMBDA.cs` (read-only source), `Prism_Config.json`.

-------

## Todo 4 — `transform-manifest.json` (all images + Tx class + parameter values + intersections)

**Target content:** all images, the `Tx_*.cs` class used, the parameter values for every variable of
that Tx class (thresholds, histogram start/mid/end points), and edge intersections.

**Decision (settled): serialize into `ImageRecord_OUTPUT.SafeSummaryText`.** Rather than adding
strongly-typed param records per transformer or a generic param dictionary to the model, each Tx class
writes its full runtime parameter set into the existing `OutputRecord.SafeSummaryText` field. The
manifest reads that back per image. This avoids new model surface.

**Blockers / notes:**
- **The params are currently not written out.** `Tx_DetailCropper`'s `_coverage`,
  `_extensionOneSided`, `_extensionBiDirectional`, `_headcut` are constructor-injected private fields
  never surfaced; `SafeSummaryText` today holds only a one-line human summary (e.g. "Detail crop
  applied."). Each of the 7 `Tx_*.cs` classes must be updated to write its actual parameter values
  into `SafeSummaryText`.
- **Format of `SafeSummaryText`.** Decide a consistent encoding (compact `key=value; ...` line, or an
  embedded JSON fragment) so the manifest builder can parse it uniformly across all transformers.
- **Edge intersections.** `EdgeIntersects` (Top/Bottom/Left/Right) is a private nested `record struct`
  in `Tx_DetailCropper`, derived from `Features.GetValue("intersects-*")`. The four intersect values
  already live on the feature snapshot, so the manifest can read them directly from
  `ImageFeatureSnapshot` — no need to widen the private struct.
- **Histogram points** live in the `Tx_util_BgStretch` / `Tx_LowContrastEnhancement` helpers invoked
  *from* the top-level Tx classes. Decide whether those helper params roll up into the calling Tx's
  `SafeSummaryText`, or are appended as their own labeled segment.

**Files:** all 7 `Services/Transform/Engine/Tx_*.cs` (+ `Utils/` helpers), `ImageRecord_OUTPUT.cs`
(field reused), `Exporter.cs`, `Prism_Config.json`.

-------

## Todo 5 — `generate-manifest.json` (one record per FamilyID with a generated image)

**Target content:** one record per FamilyID that received a generated image. Per FamilyID: all
original + target filenames, the prompt used to generate the image, the generated-image target
filename, and the phenotype.

**Blocked on real generation backend — now scoped.** `GenerationBackendAvailable()` is hardcoded
`false` (`ImageGenerator.cs:101`); every generated record is `GenerationStatus.Gated` and no prompt
concept exists in code yet.

**Prerequisite work (per direction):**
- **Install a local ComfyUI** that runs on Dell laptops with an Nvidia laptop GPU **and** on a
  CPU-only server. This is the backend `GenerationBackendAvailable()` will probe. (Model-runtime
  policy corollary: algorithm must not switch on GPU presence — the same path loads on every host,
  the runtime picks the execution provider.)
- **Prompt (interim).** First-pass generic prompt, roughly: *"pick the most hero-like image (front
  facing, no edge intersections) and integrate it in an appropriate scene that matches and preserves
  the same style and quality as the original image."* The real prompt is built later.
- **Prompt construction model.** Prompts are built by enriching/tailoring a generic template using
  `ImageRecord_LAMBDA` as the source of inspiration, drawing from a mapping analogous to the image
  phenotypes (a prompt-phenotype map paralleling `imagePhenotypes.md`).

**Model gaps / decisions:**
- `ImageRecord_GENERATED` has no `Prompt` or `Phenotype` field, and only a single
  `SourceHeroImageName`. The spec's "all original+target filenames" (plural) means the per-FamilyID
  record must reference multiple sibling originals — either extend `ImageRecord_GENERATED`, or build
  the manifest record by grouping generated records back to their FamilyID and gathering the family's
  originals at manifest time.
- **Build-now vs. defer.** Decide whether to stand up the schema now against the gated stub (near-empty
  output until ComfyUI lands) or defer the whole todo until generation is real. The prompt fields stay
  null/placeholder until the backend and prompt-builder exist.

**Files:** ComfyUI install (infra, out of repo), `ImageGenerator.cs`, `ImageRecord_GENERATED.cs`,
`Generate_Config.cs`, a new prompt-phenotype mapping, `Exporter.cs`, `Prism_Config.json`.

-------

## Todo 6 — `upscale-manifest.json` (only upscaled images: resolutions, bbox, % upscaled)

**Target content (revised 2026-07-27):** the upscale **decision for every image**, not only the ones
upscaled. Per image: all original + target filenames, original resolution, bounding-box values,
resolution-post-transform, upscale resolution, percentage upscaled — plus a **status** distinguishing
the four `UpscaleAsync` outcomes:
- `Upscaled` — actually enlarged; all resolution fields populated.
- `Unnecessary` — salient dimension already ≥ the output-width threshold; logged with reason e.g.
  *"already within 800–2000px, no upscale needed."* No scale applied.
- `KoTooSmall` — salient object below `MinInputSizeInPixels`; KO'd at preprocess.
- `KoExceededMaxScale` — required scale exceeded `MaxUpScaleFactor`; KO'd at preprocess.

`resolution-post-transform` is **independently nullable**: an image can be `Upscaled` and then KO'd at
match/transform before Transform runs — it belongs in the manifest but has no post-transform
resolution. (This is distinct from `Unnecessary`, which is "no upscale," not "upscaled-but-not-yet-
transformed.")

**Architecture confirmed correct (Option A).** Upscale runs during preprocessing (Classify stage),
before Transform — that ordering stays. The requested "resolution-post-transform" field is filled by
**joining Transform's `OutputRecord.OutputWidth/Height` back in at Export time** by `InitialFullName`.
No pipeline reordering.

**Original H&W lives on `ImageRecord_Base` (per direction).** `ImageRecord_Base.Width`/`Height` are
set at ingress and travel Base → INPUT → LAMBDA unchanged (Transform writes its dims to
`OutputRecord`, not back onto Base), so original resolution is already retained there — no new file
needed. Confirm no downstream stage mutates `Base.Width/Height` after upscale.

**Blockers / decisions — evidence not captured today:**
- **Upscale evidence is entirely discarded.** `ImagePreProcessor.UpscaleAsync`
  (`ImagePreProcessor.cs:192`) computes `scale`, the bbox pixel dims, and `largest` as throwaway
  locals, then returns bytes — nothing is attached to any record. Original resolution, computed scale,
  and true post-upscale resolution all need new plumbing onto the record (per direction, ride on the
  `ImageRecord_Base`/LAMBDA that already accompanies the image — no new type).
- **Status capture replaces the silent no-op.** `UpscaleAsync` currently returns the image unchanged
  whenever the bbox already meets `MinOutputWidth` (`ImagePreProcessor.cs:204`), indistinguishable
  from an actual upscale, and the two KO branches (`PREPROCESS_TOO_SMALL`,
  `PREPROCESS_UPSCALE_EXCEEDED`) already exist but aren't surfaced as upscale evidence. Attach the
  four-outcome status enum above to the record so the manifest reports every decision, not just the
  successful enlargements.
- **"Percentage upscaled" definition.** One line: linear scale as % vs. area-based %. Config's
  `MaxUpScaleFactor` is a ratio (e.g. 1.42), so pick and document the convention.
- **`BoundingBox`** is already on `ImageRecord_LAMBDA.BoundingBox` — read directly.

**Files:** `ImagePreProcessor.cs` (capture scale + upscaled flag + resolutions),
`ImageRecord_Base.cs` / `ImageRecord_LAMBDA.cs` (carry the evidence), `Exporter.cs` (join
post-transform dims), `Prism_Config.json`.

-------

## Cross-cutting — delivery model (RESOLVED 2026-07-27)

There are two independent delivery surfaces. They break, or don't, independently — a "manifest inside
the ZIP" and "the manifest field of the JSON envelope" are not the same contract.

**ZIP route = the everything-but-the-kitchen-sink diagnostic route.** Only humans use it, and they are
typically non-technical clients who care only about the images. The accompanying JSON manifests ride
along as a **first-aid kit for diagnosis** when a client hits something unexpected — not a
programmatic contract. The archive carries the images plus **1 + 5 manifests**:
- `prism-manifest.json` — **mandatory**, always emitted, no toggle (Todo 1).
- `ingress-manifest.json`, `match-manifest.json`, `transform-manifest.json`, `generate-manifest.json`,
  `upscale-manifest.json` — **each togglable** via the `Manifests` section in `Prism_Config.json`
  (Todos 2–6).

Adding these as archive members is **additive and not a contract break**: the workbench downloads the
ZIP as an opaque blob and never parses `manifest.json` out of it (`ResultSection.tsx` zip path shows a
static descriptor string only). The only self-owned churn from the `manifest.json → prism-manifest.json`
rename is the literal in `Exporter.cs:85`, the `ExporterTests.cs` assertions, and one cosmetic display
string in `ResultSection.tsx`.

**JSON envelope stays UNCHANGED.** The single `BatchManifest` surfaced as `result.manifest`
(`PrismJsonResultEnvelope.cs:9`, parsed by the workbench) is untouched. The six-manifest split is
scoped entirely to the ZIP archive; it never reshapes the JSON field. Zero break for JSON callers.

**Non-zip routes are client-facing and carry NO evidence, ever.** They deliver two shapes:
1. **Filename-map route** — original + new filenames only.
2. **Filename-map + imageset route** — original + new filenames plus the complete imageset (every
   image that was transformed or generated).

Neither non-zip route carries any of the evidence manifests — evidence lives only in the ZIP
diagnostic route. Todos 1 and 3 are mostly "serialize data that already exists"; Todos 2, 4, 5, 6
require capturing data currently computed and discarded — that capture is the real cost, not the JSON
serialization.

**Open decision (non-zip imageset transport):** how the "complete imageset" is delivered on a non-zip
route needs a transport call — base64-embedded in the JSON, a list of download URLs, or a multipart
response — since "non-zip + image payload" has no established shape today. Flag for the user before
implementing route 2.
