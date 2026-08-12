# PRISM — Transformation & Generation
*Abbreviations: `GLOSSARY.md`*

## ImagePreProcessor

`jb/src/core/Services/Matching/ImagePreProcessor.cs` — static class, single entry point `Preprocess(lambda, imagePath, config)`.

Steps in order:
1. **EXIF orient + flatten**: ImageSharp `AutoOrient()` + `BackgroundColor(White)` → encoded to flat JPEG (no alpha, sRGB).
2. **Salient bounding box**: OpenCVSharp Canny + local-contrast sigmoid mask, computed at ≤512 px analysis resolution. Result written to `lambda.Features["salient-bbox"]` as `"x1,y1,x2,y2"` (normalized 0–1 floats, invariant culture). Confidence fixed at 0.99.
3. **Settle the transform geometry** — `ImageTransformer.FinalizeGeometry(lambda, parameters, seed)` promotes a
   confident `SubjectDetection` over the legacy salient bbox and applies the shadow-bottom shrink. This runs
   *before* the upscale decision (T-4910): both steps change the box the Transformed stage crops on, so sizing
   against the pre-promotion box would target a canvas the pipeline never builds.
4. **Upscale decision — unified final-size bar (T-4920).** See the section below.
5. Returns flat-JPEG bytes (enlarged or untouched) or null on KO. Sets `lambda.IsKo`, `lambda.KoReasonCode`,
   `lambda.KoSafeMessage` on KO. When the bytes were enlarged, the record's geometry is enlarged with them and
   the returned BGR `Mat` is re-decoded from the new bytes, so pixels and coordinates share one space.

Called from `TransformService` immediately before routing; `lambda.BoundingBox` is available to all Tx_ classes.

### Unified upscale: same bar for both modes

**The bar is the FINAL output image, not the bounding box.** Every upscale aims at the output reaching
`MinOutputWidth` (800 px) on its longest side. That size is *computed exactly*, not predicted, by
`FinalOutputSize` (`jb/src/core/Services/Transform/FinalOutputSize.cs`) — the single helper both the upscale
stage and `Tx_CenterAndStretch` size against, so they cannot drift apart:

| Routing | Final longest dimension | Margin term |
|---|---|---|
| No edge intersect → `Tx_CenterAndStretch` | `evenFloor(bboxLongest × (1 + 2·margin)) − 2` | yes (`CropTransformSettings.WhiteSpaceMargin`, 0.042 — note the cross-config read from `transform_Config.json`) |
| Any edge intersect → `Tx_DetailCropper` | `min(imageWidth, imageHeight)` | **no** — `FinalOutputSize` still estimates against the whole-frame floor; `Tx_DetailCropper`'s actual per-pattern side (see **Transform Routing Matrix** below) can differ from this estimate |

Worked example: a 1800 px bbox at margin 0.042 gives a 1948 px canvas. Inverting it, **740 px is the smallest
bbox longest side that yields an 800 px canvas** (739 gives 798) — so images between 740 and 800 px, which the
old rule upscaled, now pass through untouched.

The `AllowEsrganUpscale` job parameter then picks only the resampler and the cap:

| | Resampler | Cap | Over the cap |
|---|---|---|---|
| **OFF (default)** | Lanczos4, locally in `ImagePreProcessor` | `MaxLanczosOnlyUpScaleFactor` (1.33) | KO `PREPROCESS_UPSCALE_EXCEEDED`, message names the toggle |
| **ON** | Real-ESRGAN via `Upscaler` / the remote Upscale host | `MaxUpScaleFactor` (1.42) | KO `PREPROCESS_UPSCALE_EXCEEDED` |

The `< MinInputSizeInPixels` (570 px) → KO `PREPROCESS_TOO_SMALL` check is unchanged, except that it now
measures the *promoted* box rather than the raw salient one.

**Known reachability property at the current config values.** On the centre-and-stretch route the Lanczos-only
cap can never fire: a bbox at the 570 px input floor needs 740/570 = 1.30×, already inside the 1.33× cap. The
OFF-mode KO is therefore reachable only on the bleed route, for images whose *shorter side* is under 602 px.
Changing `MinInputSizeInPixels`, `MinOutputWidth`, `WhiteSpaceMargin` or either cap changes this — it is a
consequence of the numbers, not a designed guarantee.

**Geometry travels with the pixels.** Upscale enlarges `lambda.BoundingBox` and `lambda.LegacySalientBox` by
the same factor it enlarges the bytes. Deliberately *not* scaled: `ImageRecord_Base.Width`/`Height` (the
original-resolution contract the upscale manifest reports against) and `lambda.Subject` (pre-upscale evidence
that stays self-consistent with its own pixel mask; the box it contributed is already promoted into
`BoundingBox` by this point).

---

## Tx_LowContrastEnhancement

Pre-step called inside `Tx_CenterAndStretch` when `lambda.Features["low-contrast"]` is true. Purpose: improve foreground/background separation before bounding-box use — not a visual quality pass for export.

**Algorithm:** CLAHE (Contrast Limited Adaptive Histogram Equalization) via OpenCVSharp4.  
**Scope:** applied to the full image, not the background region only — applying to a background-only region requires a reliable background mask which is unavailable at this stage, and full-image CLAHE is safer for bbox accuracy.  
**Implementation:** `jb/src/core/Services/Transform/Engine/Utils/Tx_LowContrastEnhancement.cs` (T-1900).

---

## Upscaling (Real-ESRGAN)

**Decision (T-2500, closed; reshaped by T-4110):** single `Upscaler` class (`Services/Upscale/Engine/Upscaler.cs`) — the model runs on every host; `OnnxSessionFactory` picks DirectML (hardware adapter present) or the CPU EP. No separate CPU algorithm exists (the former `ImageUpscaler` router and `Upscaler_c_p_u` Lanczos fallback are deleted).

- Model: `Real-ESRGAN_x2plus_dynamic.onnx` — fixed ×2 super-resolution, dynamic input H/W. Located at `jb/src/core/Services/Upscale/Engine/ONNX/`, path from `Prism_Config.json`'s `Models.Upscaling.Path`. **T-4905:** the original export declared a fixed `[1,3,64,64]` input, so an 800 px image ran as 625 serialized 64×64 tile passes (~122.9 s on the GPU). The RRDBNet is already spatially size-agnostic internally, so a metadata-only edit to the declared input shape — weights verified bit-identical, all 702 initializers hashing the same — lets the whole image run in one pass: **122.9 s → 10.19 s, ~12×**. The `_dynamic` file is gitignored (too large for git) and lives in the source tree next to the fixed-64 original.
- Session init: `Upscaler.Initialize(modelPath, configPath)` called from `UpscaleService.Create()` on every host at startup.
- Tensor pipeline: JPEG → BGR uint8 → NCHW float32 [0,1] → `_session.Run(["input"])` → NCHW float32 [0,1] × 2 → clamp → BGR uint8 → JPEG. Tensor names: `input` / `output`.
- Top-up: remaining scale after ×2 SR applied via Lanczos4 resize.
- Config: model asset resolved via `ModelAssetLocator.Find(configuration.UpscaleModelPath)` — the path comes from `Prism_Config.json`'s `Models` section. Missing asset fails config load (`PrismConfiguration.ValidateModelAssets`); a present-but-unloadable model fails `UpscaleService.Create` — both loud, no fallback (T-4110, see `PRISM-model-runtime.md`). Exception: with `Models.Upscaling.UseIt` false the session is never created and the asset is not existence-checked; `TransformService` forces `allowEsrganUpscale` off so every image takes the Lanczos path capped at `MAXIMUM_UpScale_LanczosOnly`.
- Access boundary: `GpuProbe` is internal to `Prism.Services.Upscale`. External callers (e.g. `RuntimeProviderProbe` in `Prism.Api`) use `Upscaler.IsGpuAvailable` rather than calling `GpuProbe` directly.

### Tile stitching — weighted blend (no seams)

With the dynamic-shape model in use, `RunTiled` runs the whole image as a single tile (rounded up to even H/W — the model's `pixel_unshuffle(2)` reshape rejects odd dimensions; the existing pad plus the accumulator's bounds check clip the ×2 overshoot back to exactly `src × 2`). The tiling machinery below stays in place for a fixed-shape export and is the documented fallback if a large image ever exhausts GPU memory:

- Each tile edge that faces a real neighboring tile discards a small band nearest the seam (least-accurate pixels, at the edge of the model's receptive field), then tapers from 0 to 1 across the remaining overlap with a raised-cosine ramp. An edge facing the true image border carries full weight throughout — there is no neighbor to blend against there.
- Every output pixel accumulates a weighted sum from every tile that covers it (`AccumulateTile`) and is normalized by the accumulated weight at the end (`NormalizeAccumulator`). A pixel's "home" tile always contributes full weight, so the divide is never by zero.
- Tunable via `jb/src/core/Services/Upscale/Engine/cfg_Upscale.json` (`UpscaleConfig`): `Tiling.TileOverlapPixels` (total overlap reserved per seam, source pixels) and `Tiling.DiscardBandPixels` (portion of that overlap discarded before blending starts). Resolved via `ModelAssetLocator.Find("Services/Upscale/Engine/cfg_Upscale.json")`; `UpscaleService.Create()` throws `PrismConfigurationException` if it can't be found. A missing/unreadable config at the `Upscaler.Initialize` level itself falls back to hardcoded defaults (16px overlap / 3px discard) rather than blocking session load.

---

## Transformation Overview

Images transformed one by one, each based on image analysis enriched with match information. Salient object detection, bounding box calculation, and background identification feed the per-image transform decision. Useful tags from `ImageMatcher.cs` attenuate transformation parameters. Transform rules in `jb/src/core/Services/Transform/Engine`. Transformation parameters guided by per-image IFs and selected INGP phenotype.

**Current impl (DetailCropper rework, 2026-08-11):** All Tx classes (`Tx_CropSquare`, `Tx_CenterAndStretch`, `Tx_DetailCropper`, `Tx_ProblemImageProcessor`) are active. `ImageTransformer.SelectTransformer()` routes live per the matrix below — routing is now edge-intersection-count only; `SelectedPhenotype` and det-slot no longer gate any route. `Tx_DetailCropper` implements a gravitational-anchor decision tree over the 1/2-opposite/2-adjacent/3/4-intersection patterns: a touched edge stays flush (plus `WhiteSpaceMargin` on the far edge for the 1-intersection case only), every axis without a touched edge centers on the bbox, and each axis independently shrinks (crop, bbox-preserving) when possible or extends through `Tx_util_BgStretch` otherwise. `Tx_CropSquare` is not currently reached by any route (kept for a future repurposing, not deleted). See `Tx_DetailCropperTests.cs` for coverage. Remaining open work in `jb/src/core/Services/Transform/Engine/jbtodo.md` is limited to `Tx_util_HeadCutter` (Algorithm A anatomy-guided search, family-aware mode).

---

## Transform-Facing Classification Tags

Routing inputs read from `ImageRecord_LAMBDA.Features`:

| Feature | Role |
|---|---|
| `intersects-top/bottom/left/right` | Primary edge-intersect detection — drives Tx_CenterAndStretch vs. Tx_DetailCropper routing, and the exact anchor pattern within Tx_DetailCropper |
| `salient-bbox` | Object bounds — required for Tx_CenterAndStretch and Tx_DetailCropper; a genuinely missing bbox routes to Tx_ProblemImageProcessor as a last resort |
| `low-contrast` | Triggers `Tx_LowContrastEnhancement` pre-step inside Tx_CenterAndStretch |
| `shadow-present` | Triggers shadow-band shrink of `salient-bbox` bottom edge inside Tx_CenterAndStretch |

`ImageRecord_LAMBDA.SelectedPhenotype`, `DetOrder`, and `ProductTypeId` are **not** read by routing (the DetailCropper rework, 2026-08-11, removed the phenotype/det-slot gate; see **Transform Routing Matrix** below). They remain available on the record for other stages.

All other IFs (human detection, head visibility, orientation, background, color, material) are available as secondary decision modifiers but do not currently gate the primary routing decision.

---

## Salient Object Bounds

Represented by `jb/src/core/Models/BoundingBox.cs`. Fields (all integers): `X`, `Y`, `Width`, `Height`, `Top`, `Left`, `Right`, `Bottom`.

`BoundingBox` does not emit confidence, detection method, or border-intersection flags. Border-intersection state tracked separately as transform/classification evidence.

The `salient-bbox` computed by `ImagePreProcessor` is the sole saliency anchor for all Transform-stage work — no additional saliency computation happens downstream. `Tx_CenterAndStretch` and `Tx_DetailCropper` both center their crop/reposition math on this bounding box directly.

**Superseded by the subject box when one is available (T-4850).** `ImageTransformer.PreferSubjectGeometry`
promotes a persisted `SubjectDetection` — produced upstream in the Classify refinement chain, shadow- and
background-excluded — into `BoundingBox` and the four `intersects-*` features before routing. Every Tx
strategy then runs on the better geometry with no per-strategy change. Promotion requires the detection to
clear `Crop.SubjectPromotionMinConfidence` and not be the whole-frame fallback; below that bar the legacy
salient bbox stands. The pre-promotion box is retained on `LegacySalientBox` and written to the transform
evidence, so the two can be compared after the fact rather than only in a side-by-side rerun.

Transform performs **no detection of its own** — it consumes what the Classify stage measured.

---

## Background Identification

Emits: dominant background color + background type, measured by `ImageFeatureAnalyzer.AnalyzeBackground` and recorded as the `"background-type"` feature-snapshot string (`jb/src/core/Services/Matching/Classify/ImageFeatureAnalyzer.cs`).

| Type | Meaning |
|---|---|
| `SOLIDCOLOR` | Flat backdrop — the only value that counts as "flat" |
| `REALLIFE` | A real-world scene: location, environment, decorative context |
| `UNKNOWN` | Cannot be determined safely |

The earlier five-value taxonomy (`FLAT_PERFECT`, `FLAT_NATURAL`, `TEXTURED`, `AMBIANCE`) was retired by
[[T-4700]] (2026-07-27) along with its stub producers. **`UNKNOWN` is not flat** — an unmeasured background
is not a known-simple one, and code that collapses the two skips work it cannot justify skipping.

The distinction the retired taxonomy tried to draw between a *flat-natural* sweep and a *textured/ambiance*
scene is now made where it is actually needed and actually measurable: inside subject detection, from the
residual of the background plane fit. See "Seeded steering" in `PRISM-classify.md`.

---

## Light Object on Light Background

Object very light (white, light gray, creme) + background also very light → use different parameters to improve bounding box calculation.

---

## Border Intersection Rule (No-Reposition)

If salient object exits image frame at one or more edges → margin **cannot** be applied in that direction (margin is 1-intersection-only in any case, see the Routing Matrix). Object "sticks" to intersecting edges — the touched edge stays flush; there is no repositioning in the blocked direction(s).

`Tx_DetailCropper` never delegates to `Tx_CropSquare`. Every intersection pattern (1/2-opposite/2-adjacent/3/4) is handled locally: an axis without a touched edge shrinks (crop, bbox-preserving) when the bbox fits, or extends via `Tx_util_BgStretch` when it doesn't. The OUTPUT record's `TransformerType` always reads `Tx_DetailCropper` on this route; `Warnings` records which pattern fired and whether extension was applied.

---

## Transform Routing Matrix

`ImageTransformer.SelectTransformer()` (DetailCropper rework, 2026-08-11) evaluates in order (first match wins). `Tx_ProblemImageProcessor` is a last-resort fallback, not a precondition gate — every real image carries a bbox (a detection or the full frame), so it is reached only when no rule below claims the image:

1. Bbox present **and** any edge intersect is true (`intersects-top/bottom/left/right`) → **`Tx_DetailCropper`**, which dispatches internally on the exact intersection pattern (see below).
2. Bbox present, no edge intersects → **`Tx_CenterAndStretch`**.
3. Neither rule claims the image (bbox missing) → **`Tx_ProblemImageProcessor`**.

`SelectedPhenotype` and det-slot/`ProductTypeId` are **not read by routing at all** — a prior gate (phenotype = `closeup-image`/`model-detail-closeup` + det-slot eligibility) was removed. A stub comment in `ImageTransformer.SelectTransformer` shows how to reinstate a gate if a future ticket needs one; `Tx_CropSquare` and the det-slot-exclusion concept are not deleted, just currently unreferenced by routing (kept for a possible future repurposing).

### Tx_DetailCropper's internal dispatch (by exact intersection pattern)

| Pattern | Anchor | Margin | Free axis behavior |
|---|---|---|---|
| 1 intersection | Touched edge flush | `WhiteSpaceMargin` (0.042) on the far edge of the touched axis | Perpendicular axis centers on the bbox; shrinks if the bbox fits, else extends |
| 2 opposing (top+bottom or left+right) | Both edges of the pinned axis flush (full frame extent) | None | The other axis centers on the bbox; shrinks if it fits, else extends **symmetrically** |
| 2 adjacent (shared corner) | Shared corner flush on both axes | None | Each axis independently: shrink the larger dimension toward the smaller if bbox-preserving, else extend the smaller dimension **away from the corner** only — the larger dimension is never touched |
| 3 (one open side) | The 3 touched edges pin one axis at full extent (never moved) and anchor the 4th (open) axis's touched edge flush | None | The open axis's far edge shrinks toward the anchor if it fits, else extends toward the open side only |
| 4 (all edges touched) | N/A — no free direction anywhere | None | Always a centered square crop at `min(imgW, imgH)`, no extension |

Shrink-vs-extend test: an axis's flush/centered position is tried first as a plain crop; if the required window would run off the frame, the in-frame portion is cropped and the remainder filled via `Tx_util_BgStretch` (see **Fill Method** below). `Tx_util_BgStretch` owns all fill/stretch mechanics — `Tx_DetailCropper` only ever picks the canvas size and source offset.

`ComputeIdealSide`/Coverage-floor sizing and the `CropExtensionOneSided`/`CropExtensionBiDirectional`/`AdjacentCropCap` budget config values from the pre-rework design are gone — sizing is now driven purely by bbox-preservation (crop if the full bbox stays in frame, else extend), not a percentage cap.

## Repositioning and Margin Application

Margin applied so there is whitespace between object and image edge. Method: crop original image using bounding box coordinates + desired margin value. If repositioning would require **new pixels** → fill to mimic existing background (background extension).

---

## Headcut

Headcut is a job-level `Headcut` bool on `PrismProcessingParameters`, threaded through the Transform service chain to `Tx_DetailCropper` — there is no classification-confidence threshold gating it. When enabled, `Tx_util_HeadCutter.Analyze()` runs before the crop decision tree: human presence is pre-determined by `Analyzer_HasHuman` (runs in `ImagePreProcessor`, feature `has-human`); face position is found via OpenCV Haar cascade (`haarcascade_frontalface_default.xml`); the cut line is set at 75% of detected face height (nose-to-lips approximation) and the image is cropped from that line downward, with the bounding box shifted up to match. This is the per-image (Algorithm B) path — the anatomy-guided Algorithm A refinement remains blocked (see `jbtodo.md`).

---

## Background Extension

For eligible images (not blocked by intersection): repositioning centers the object by cropping/expanding geometry so the configured margin exists between object and image edge. New pixels are filled to mimic existing background. Object geometry must be preserved. Intersecting-border images remain governed by the no-reposition rule in blocked directions.

### Fill Method — Tiered by Extension Ratio

Extension ratio = filled canvas area / source image area.

| Tier | Extension ratio | Method |
|---|---|---|
| 1 | ≤ 125% | Basic edge extension (mirror or clamp border pixels outward) |
| 2 | ≤ 142% | Content-aware edge extension (patch-based or frequency-aware border propagation) |
| 3 | > 142% | OpenCV inpainting — INPAINT_TELEA preferred; INPAINT_NS as alternative |
| 4 | > 250% | Solid white fill (#FFFFFF) |

- Never use Gaussian blur as a fill method.
- Apply seam feathering at the extension boundary after tiers 1 and 2. Tier 3 (inpainting) handles its own seam implicitly. No separate blur pass at any tier.
- Implemented by `Tx_util_BgStretch.cs` (sub-step helper, not an `IImageTransformation` implementor).

**Post-fill cleanup (center-and-stretch):** seam feathering and local smoothing applied at extension boundaries after tier 1 and 2 passes. Tier 3 inpainting handles its own seam implicitly. No blur pass at any tier.

---

## Transform Failure & KO Policy

**KO the image** (transform stage) when:
- Decoded input is invalid (corrupt, unsupported encoding after format conversion).
- Image shorter dimension < `Input.Images.MINIMUM_SIZE_IN_PIXELS` (570 px) AND the upscale required to reach `Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS` (800 px) would exceed `Output.Images.Resize.MAXIMUM_UpScale` (1.42×).

All other failure modes — unknown features, low-confidence geometry, fill/stretch failures — are handled by `Tx_ProblemImageProcessor` (conservative proportional resize, export with warnings). No other transform failure triggers KO.

## Missing Bbox → Problem Processing

When `ImageTransformer.cs` finds no bbox on the record (a genuinely bbox-less record — not expected in practice, since every real image gets either a detection or the full-frame fallback), route to `Tx_ProblemImageProcessor.cs` for conservative processing. Do not use normal transform assumptions. `SelectedPhenotype` no longer factors into this decision (DetailCropper rework, 2026-08-11).

---

## Generation Logic

### Trigger

If image collection for a FID has **x or fewer images** (configurable in CFG) → generate new images, provided originals are high enough quality.

**Current impl:** `GenerationBackendAvailable()` returns `false`. Decision shell (which families qualify) implemented in `ImageGenerator.cs`. Every qualified family receives `GenerationRouteState.Gated` — no inference runs. Open work in `jb/src/core/Services/Generate/jbtodo.md`.

### Local Generation (Recommended)

ComfyUI + Flux.1-schnell (≥12 GB VRAM, 4-step distillation). On same machine or LAN server. ONNX Runtime considered later only if model conversion and quality are proven.

### External SaaS — NOT Permitted

No external SaaS pipeline dependencies. Only permitted external exception: `www.letsenhance.ai`.

### Generation Cases

For families with low image count, copy hero image (front-facing product/model, fullest view) and generate an alternative:
- Crop to a detail
- Embed on a different background using GenAI
- Or both

### IRG

IRL records only whether generation was skipped, created child records, or failed. Generation-specific details live in IRG — see `PRISM-models.md`.

### Order Gaps and Generation

Order gaps allowed when `_det` slots can be filled by copying/transforming an existing image. After generation, remaining gaps in `_det` sequence are closed during renaming.
