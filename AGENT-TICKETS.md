# PRISM Agent Tickets

Main thread is the orchestrator: owns ticket status, final integration, conflict resolution, and user-facing summaries.

## Team Rules

- Do not revert or overwrite edits made by other agents.
- Stay inside the ownership and write scope stated on your ticket.
- Read `jb/docs/PRISM-index.md` first; load only docs relevant to the ticket.
- Preserve pipeline order: Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported.
- Do not advance past a milestone until its gate is documented and passed.
- Unresolved product decisions stay in folder-local `jbtodo.md` files — do not guess policy.

## Agent Reporting Protocol

- Report: ticket ID, changed files, commands run, pass/fail results, blockers, assumptions, next ticket.
- If blocked: stop, ask the orchestrator one targeted question — never ask the user directly.
- If work is found outside ticket scope: report a follow-up ticket, do not edit out of scope.
- Do not self-start the next ticket; orchestrator reviews completed work first.

## Orchestrator Handoff Protocol

- Satisfactory → mark `Done`.
- Incomplete but salvageable → correction to same agent or follow-up ticket.
- Missing product intent → ask user, then unblock agent.
- Milestone gates are authoritative: later tickets stay blocked until the gate passes.

## Ticket Format

Status: Ready, Blocked, Active, Review, Done. Agent type: `explorer`, `worker`, or orchestrator.

## Runtime Profiles

| Profile | Model | Use |
|---|---|---|
| `P0-orchestrator` | parent/default | Main thread, integration, conflict resolution, milestone decisions |
| `P1-feature-worker` | parent/default | Primary implementation tickets |
| `P2-verifier` | haiku | Smoke-test agents — run commands, inspect results, report blockers |
| `P3-scout` | haiku | Read-only exploration, architecture maps, dependency checks |
| `P4-critical-architecture` | parent/default | Cross-cutting contracts or pipeline architecture |

## Milestone Gates

| Milestone | Feature area | Gate condition |
|---|---|---|
| M5 Classification Groundwork | ImageNGP taxonomy + rule correctness | All Classify `jbtodo.md` decisions answered; ONNX session migrated to singleton |
| M6 Human & Model Detection | `hero-is-human`, `contains-mannequin`, `has-human`, `head-visible`, `face-visible` | On-model and ghost phenotypes (`front-on-model-*`, `ghost-front/back/side`) fire correctly on labeled images |
| M7 Orientation & Pose | `hero-orientation`, `pose-type`, `camera-angle`, `top-view` | Packshot orientation-split phenotypes (`front-packshot`, `back-packshot`, `side-packshot`) fire from real signal |
| M8 Product & Packaging | `packaging-visible`, `product-type-label`, `multiple-products` | packshot phenotypes fire from CLIP; `packaging-visible` no longer always UNKNOWN |
| M9 Composition & Spatial | `product-coverage-ratio`, `image-occupancy`, `salient-bbox`, `vertical-centering`, `horizontal-centering` | Composition phenotypes measured; overflow slot assignment accuracy confirmed |
| M10 Semantic & Content | `text-present`, `logo-present`, `dominant-colors`, `lighting` | Content features populated; transform routing that depends on them verified |
| M11 Production Validation | All 26 phenotypes | < 5% misassignment on labeled validation set; no systematic error on any single phenotype |

## Tickets

### T-1300 · Implement Fetch_HTTPS_DirectFile.cs
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker

Implement `IFetchStrategy` in `jb/src/core/IO/Fetchers/Fetch_HTTPS_DirectFile.cs` to download a file from a direct HTTPS URL.

**Acceptance:**
- Validates URL against `HostRules.json`: allowed schemes, blocked hosts, redirect count limit, timeout.
- Streams download to `%TEMP%/prism/{jobID}/`.
- Returns `ImageRecord_INPUT`.
- `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/IO/Fetchers/Fetch_HTTPS_DirectFile.cs`

---

### T-1400 · Implement Fetch_DropBox.cs
**Status:** Blocked | **Profile:** P1-feature-worker | **Agent:** worker  
**Blocked-by:** Product decision — public-only vs. OAuth-authenticated scope. Not required for V1.

Public shared links (`dropbox.com/s/...?dl=0`) can be normalized (`?dl=1`) and delegated to `Fetch_HTTPS_DirectFile`. Private links require OAuth2 + Dropbox API v2.

**Acceptance (when unblocked):**
- Scope decision documented.
- Public link normalization implemented; delegates to `Fetch_HTTPS_DirectFile`.
- `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/IO/Fetchers/Fetch_DropBox.cs`

---

### T-1500 · Split StageShells.cs into per-stage files
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker

`jb/src/core/Pipeline/StageShells.cs` contains 8 `internal static class` declarations (~430 lines). Rule: one type per file, filename matches type name. Naming convention: `ShellStage_Xyz.cs` (not `XyzStageShell.cs`).

**Acceptance:**
- `StageShells.cs` deleted.
- Eight new files in `jb/src/core/Pipeline/`, each with one renamed class:
  - `ShellStage_Import.cs` (was `ImportStageShell`)
  - `ShellStage_Classify.cs` (was `ClassifyStageShell`)
  - `ShellStage_Match.cs` (was `MatchStageShell`)
  - `ShellStage_Order.cs` (was `OrderStageShell`)
  - `ShellStage_Rename.cs` (was `RenameStageShell`)
  - `ShellStage_Generate.cs` (was `GenerateStageShell`)
  - `ShellStage_Transform.cs` (was `TransformStageShell`)
  - `ShellStage_Export.cs` (was `ExportStageShell`)
- `Prism.cs` call sites updated to use new class names.
- `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/Pipeline/StageShells.cs` (delete); `ShellStage_Import.cs` through `ShellStage_Export.cs` (new); `Prism.cs` (call site renames)

---

### T-1600 · SD-8: ImageRecord_OUTPUT Width/Height/Checksum
**Status:** Done | **Profile:** P0-orchestrator

**Resolution — not a bug.** `ImageRecord_OUTPUT` inherits from `ImageRecord_Base` which already declares `Width`, `Height`, and `Checksum`. All `ImageRecord*` types carry these fields via inheritance. No fix required.

**Files:** `jb/src/core/Models/ImageRecord_Base.cs` (no changes)

---

### T-1700 · Implement Tx_util_BgStretch
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker

Implement the tiered background fill utility in `jb/src/core/Images/Transform/Tx_util_BgStretch.cs`.
Called as a sub-step from `Tx_CenterAndStretch` and `Tx_DetailCropper`. Not an `IImageTransformation` implementor.

**Tiers (extension ratio = filled canvas area / source image area):**
- ≤ 125%: basic edge extension (mirror or clamp border pixels outward)
- ≤ 142%: content-aware edge extension (patch-based or frequency-aware border propagation)
- > 142%: OpenCV inpainting — INPAINT_TELEA preferred, INPAINT_NS as alternative
- > 250%: solid white fill (#FFFFFF)

**Rules:**
- Never use Gaussian blur as a fill method.
- Apply seam feathering at extension boundary after tiers 1 and 2.
- Tier 3 inpainting handles its own seam implicitly.
- Expose `Process(byte[] arr, int stride, float upscale_factor)` per the dual-interface contract.

**Acceptance:**
- All four tiers select the correct method for the given extension ratio.
- Seam feathering applied after tiers 1 and 2; not after tiers 3 or 4.
- `Process()` signature matches dual-interface contract.
- `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/Images/Transform/Tx_util_BgStretch.cs`

---

### T-1800 · Add ProductTypeId to ImageRecord_LAMBDA
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker

**Resolution:** Field existed in `ImageRecord_LAMBDA.cs` (line 67) and `ImageTransformer.cs` already read it. Only the write was missing. Fixed: added `lambda.ProductTypeId = productTypeId;` in `ImageOrderer.ProcessFamily` write-back loop (line ~93). `ResolveProductType()` reads from `FamilyIDRecord.CanonicalProperties.Values` (Excel IEM dynamic columns) and normalizes to kebab-case against `DetOrderRules.json` — no CLIP involvement; CLIP confidence enforcement is a separate future enhancement.

**Files:** `jb/src/core/Images/ImageOrderer.cs`

---

### T-1900 · Implement Tx_LowContrastEnhancement
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker

Implement `jb/src/core/Images/Transform/processingtools/Tx_LowContrastEnhancement.cs` (currently empty). Called as a pre-step inside `Tx_CenterAndStretch` when `lambda.Features["low-contrast"]` is true. Purpose: improve foreground/background separation to sharpen subsequent bounding-box accuracy — not a visual quality pass for export.

**What to do:**
1. Apply CLAHE (Contrast Limited Adaptive Histogram Equalization) via OpenCVSharp4 to the full image (not background-region only — safer for bbox accuracy).
2. Signature: `Process(byte[] arr, int stride, float upscale_factor)` matching the webservice dual-interface contract; also callable as a sub-step accepting/returning JPEG `byte[]`.
3. `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/Images/Transform/processingtools/Tx_LowContrastEnhancement.cs`

---

### T-2000 · Implement Tx_CenterAndStretch pixel flow
**Status:** Blocked | **Profile:** P1-feature-worker | **Agent:** worker  
**Blocked-by:** T-2300 (saliency/headcut/greedy user decisions) — T-1900 Done

Three-step pixel flow inside `Tx_CenterAndStretch.Transform()` — currently gated behind `ImageProcessorAvailable() = true` but pixel body is a `NotSupportedException`.

**When unblocked, what to do:**
1. Pre-steps: if `low-contrast` feature true → call `Tx_LowContrastEnhancement`; if `shadow-present` → shrink `salient-bbox` bottom edge above shadow band.
2. Tight crop: shrink source canvas to adjusted `salient-bbox`.
3. Center: place cropped object on target square canvas with `Transformation.Positioning.Margin` (4.2%) on all sides.
4. Fill: call `Tx_util_BgStretch.Stretch()` on the uncovered canvas edges.
5. Populate `ImageTransformationResult` fully (crop rect, fill method, warnings).
6. `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs`

---

### T-2100 · Implement Tx_DetailCropper pixel flow
**Status:** Blocked | **Profile:** P1-feature-worker | **Agent:** worker  
**Blocked-by:** T-2300 (saliency/headcut/greedy user decisions), T-2200 (HeadCutter spec), T-2000 (for pattern reference)

Pixel body for `Tx_DetailCropper.Transform()` — currently gated and throws.

**When unblocked, what to do:**
1. Read `salient-bbox` from `InputImage.Features`.
2. Detect border intersection (intersects-top/bottom/left/right features).
3. Non-intersecting: apply greedy crop centered on saliency region; apply headcut when `head-visible` and `hero-is-human` meet configured thresholds.
4. Border-intersecting: anchor crop to touched edges; record no-reposition decision.
5. Apply `Tx_util_BgStretch` when crop extends beyond original bounds.
6. Populate full `ImageTransformationResult`.
7. Internal fallback to `Tx_CropSquare` when border intersection blocks pixel-level repositioning.
8. `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/Images/Transform/Tx_DetailCropper.cs`

---

### T-2200 · Spec and implement Tx_util_HeadCutter
**Status:** Blocked | **Profile:** P1-feature-worker | **Agent:** worker  
**Blocked-by:** Product decisions (landmark model, family-aware threshold, cut line style, Y-coordinate return format) must be recorded in Transform `jbtodo.md` before any code is written.

Utility class for cutting a human head at the nose-to-lips boundary. Two modes: family-aware (shared cut line from clear-face images in the group) and per-image fallback.

**Files:** `jb/src/core/Images/Transform/processingtools/Tx_util_HeadCutter.cs`

---

### T-2300 · User decisions: detail crop saliency, headcut, greedy crop
**Status:** Blocked | **Profile:** P0-orchestrator  
**Blocked-by:** User product decision required — answers must be recorded in Transform `jbtodo.md` before T-2100 or T-2200 can proceed.

Three open questions in Transform `jbtodo.md` with blank `Answer:` fields:
1. Saliency map behavior: how the dominant saliency region influences square crop placement when no border intersection blocks repositioning.
2. Headcut thresholds: which `head-visible`/`hero-is-human` confidence levels enable headcut; how top crop placement shifts for eligible non-intersecting images.
3. Greedy crop behavior: minimum content retention and padding rules for non-headcut non-intersecting images.

Each answer unlocks T-2100 (DetailCropper) and T-2200 (HeadCutter).

**Files:** `jb/src/core/Images/Transform/jbtodo.md` (answers to be recorded there)

---

### T-2400 · Implement cross-bracket tie accumulator
**Status:** Ready | **Profile:** P1-feature-worker | **Agent:** worker

Decision recorded (T-2400 close-out note in `PRISM-match.md`): add a cross-bracket candidacy accumulator to `ImageMatcher.RunWaterfall`. Any image that was a candidate for 2+ FamilyIDs across brackets and exits Bracket 5 unmatched is KO'd with reason `MATCHES_MULTIPLE_FAMILYIDS`. No det-position comparison.

**Acceptance:**
- `RunWaterfall` accumulates all FamilyID candidates for each image across Brackets 1–4.
- `KoUnmatched` applies reason `MATCHES_MULTIPLE_FAMILYIDS` when the accumulated set has 2+ entries.
- Existing `MATCH_NOT_FOUND` reason applies only when the accumulated set is empty (genuine no-match).
- `dotnet build jb/src/PRISM.sln` passes.

**Files:** `jb/src/core/Images/Match/ImageMatcher.cs`

---

### T-2500 · Implement GPU upscaler (Real-ESRGAN via DirectML)
**Status:** Done | **Profile:** P1-feature-worker | **Agent:** worker

`Upscaler_g_p_u.RunRealEsrgan` throws `NotImplementedException`. `GpuProbe.HasHardwareDirectMLAdapter()` already gates the code path correctly.

**What to do:**
1. Add `Microsoft.ML.OnnxRuntime.DirectML` NuGet to `jb/src/core/Images/Upscale/Prism.Core.Images.Upscale.csproj`.
2. Initialize `InferenceSession` once with `AppendExecutionProvider_DML(adapterIndex: 0)` pointing to the model path from config.
3. Implement `RunRealEsrgan`: decode input JPEG → BGR float32 NCHW [1,3,H,W] → `_session.Run` → output [1,3,H×2,W×2] → clamp [0,1] → BGR uint8 → JPEG bytes.
4. Model path: add `Upscale.ModelPath` key to `Prism_Config.json` pointing to `real-esrgan-x2plus.onnx`.
5. `dotnet build jb/src/PRISM.sln` passes; `ImageUpscaler.Upscale(bytes, 1.3)` on a DX12-capable machine returns a larger image without throwing.

**Files:** `jb/src/core/Images/Upscale/Upscaler_g_p_u.cs`, `jb/src/core/Images/Upscale/Prism.Core.Images.Upscale.csproj`

---

### T-2600 · M5 Classify groundwork
**Status:** Blocked | **Profile:** P0-orchestrator  
**Blocked-by:** M5 milestone gate — all Classify `jbtodo.md` decisions must be answered first.

Tracks the five open items in `jb/src/core/Images/Classify/jbtodo.md`:
1. Gate phenotypes (bypass flag — stays open until phenotypes validated).
2. Confirm ImageNGP taxonomy: `ImageNGP.json` ↔ `imagePhenotypes.md` ↔ `ImageRoles.json` agree on 26 phenotypes and their IF combinations.
3. Resolve `illustration-technical-drawing` scope (option (b) = null/no-phenotype recommended).
4. Replace `RecordUnknownFeatures()` stub with real CLIP measurements (after taxonomy + prompts are settled).
5. Phenotype production validation: labeled set, confusion matrix, <5% misassignment rate across 26 phenotypes.

M5 gate condition: all Classify decisions answered; ONNX session migrated to singleton.

**Files:** `jb/src/core/Images/Classify/jbtodo.md`, `jb/src/core/Images/Classify/ImageFeatureAnalyzer.cs`

---

## Verification Rules

- After project/solution setup: `dotnet build jb/src/PRISM.sln`, API/WPF run smoke, web `npm run typecheck` + `npm run build`.
- After each milestone: run relevant smoke and record result in milestone table.
- After todo/doc sync: `rg --files -g 'jbtodo.md' jb/src` and `rg -n "^- \[ \]" jb/src`.
- Before advancing milestones: `git status --short` to confirm edits stayed inside assigned ownership.
