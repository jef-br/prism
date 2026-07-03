# PRISM Agent Ticket Archive

Done tickets — result only. Full specs lived in AGENT-TICKETS.md before archiving.

---

### T-1300 · Fetch_HTTPS_DirectFile
**Done.** `Fetch_HTTPS_DirectFile.cs` streams direct HTTPS downloads to `%TEMP%/prism/{jobID}/`, validates URL against `HostRules.json` (scheme, blocked hosts, redirect limit, timeout), returns `ImageRecord_INPUT`.

---

### T-1400 · Fetch_DropBox
**Done.** Public shared links (`dropbox.com/s/...?dl=0`) normalized to `?dl=1` and delegated to `Fetch_HTTPS_DirectFile`. `dl.dropboxusercontent.com` URLs pass through unchanged. Private OAuth deferred (out of scope V1).

---

### T-1500 · Split StageShells.cs
**Done.** `StageShells.cs` deleted. Eight `ShellStage_Xyz.cs` files created in `jb/src/core/Pipeline/` (one per stage). `Prism.cs` call sites updated to new class names.

---

### T-1600 · SD-8: ImageRecord_OUTPUT Width/Height/Checksum
**Done — not a bug.** Fields are declared on `ImageRecord_Base` and inherited by all `ImageRecord*` types. No code changes.

---

### T-1700 · Tx_util_BgStretch
**Done.** Tiered background fill: ≤125% edge clamp, ≤142% content-aware extension, >142% INPAINT_TELEA, >250% solid white. Seam feathering after tiers 1 and 2. `Process(byte[] arr, int stride, float upscale_factor)` dual-interface signature.

---

### T-1800 · ProductTypeId write to ImageRecord_LAMBDA
**Done.** `lambda.ProductTypeId = productTypeId;` added in `ImageOrderer.ProcessFamily` write-back loop. `ResolveProductType()` reads from Excel IEM dynamic columns and normalizes to kebab-case against `DetOrderRules.json`.

---

### T-1900 · Tx_LowContrastEnhancement
**Done.** CLAHE (Contrast Limited Adaptive Histogram Equalization) via OpenCVSharp4, applied to full image. Dual-interface signature `Process(byte[] arr, int stride, float upscale_factor)`.

---

### T-2400 · Cross-bracket tie accumulator
**Done.** `RunWaterfall` maintains `crossBracketCandidates` (per-image `HashSet<string>`). Brackets 1+2 populate from `tiedCandidates`; Bracket 3 adds candidates rejected by duplicate-phenotype guard. `KoUnmatched` emits `MATCHES_MULTIPLE_FAMILYIDS` (≥2 candidates) vs `MATCH_NOT_FOUND` (0). Two `AccumulateCandidates` overloads added.

---

### T-2500 · GPU upscaler (Real-ESRGAN via DirectML)
**Done.** `Upscaler_g_p_u.RunRealEsrgan` implemented: JPEG decode → BGR float32 NCHW [1,3,H,W] → `InferenceSession.Run` with DML EP → output [1,3,H×2,W×2] → clamp [0,1] → BGR uint8 → JPEG bytes. Model path from `Prism_Config.json Upscale.ModelPath`.

---

### T-2700 · Wire fetcher strategies into API ingress
**Done.** `FetchDispatcher` created — ordered strategy list with `CanHandle`/`FetchAsync`. `AddRemoteInputRecords` made async; routes via dispatcher first (content-type based), falls back to URL extension. Dropbox folder ZIPs routed to `zipFiles`. `PrismApiConfiguration` carries `FetchDispatcher` instance.

---

### T-2000 · Implement Tx_CenterAndStretch pixel flow
**Done.** Full `Transform()` + `Process()` pixel flow implemented and build clean. Headcut via `Tx_util_HeadCutter` when requested; background fill via `Tx_util_BgStretch.Stretch()`. Canvas math amended after T-2100/T-3100 verification: crop to bbox, resize to margin-adjusted target size preserving aspect ratio, center on canvas, then stretch background (guarantees non-negative placement offset).

**Files:** `jb/src/core/Images/Transform/Tx_CenterAndStretch.cs`

---

### T-2800 · API/in-process pipeline never initializes the GPU Real-ESRGAN upscaler
**Done.** `PipelineServiceFactory.CreateInProcess`/`CreateFromEnvironment` now call `UpscaleService.Create(configuration)` once (mirrors MatchingService/CLIP eager-init); missing model asset degrades to CPU. `Upscaler_g_p_u.Initialize` made idempotent, thread-safe (`_sessionLock`, serializes `session.Run()`) and non-throwing (`IsReady`); `ImageUpscaler.Upscale` routes to GPU only when hardware present *and* session loaded. Fix exposed second bug: committed model has fixed `[1,3,64,64]` input — added overlapping-tile inference (`RunTiled`/`RunSingleTile`, 8px border discard, shape from `session.InputMetadata`). 224/224 tests green (was 9 failing); live CiMini Full run via API completes with real GPU-tiled output. `expected-manifest.json` not committed — non-determinism filed as T-2820, det8 numbering as T-2830.

**Files:** `jb/src/core/Services/PipelineServiceFactory.cs`, `jb/src/core/Images/ImageUpscaler.cs`, `jb/src/core/Images/Upscale/Upscaler_g_p_u.cs`, `jb/src/tests/Prism.Core.Tests/Upscaler_g_p_uTests.cs`

---

### T-2810 · PipelineIntegrationTests hard-depend on an uncommitted dataset
**Done.** `ResolveTestFixturePath()` rewritten to walk up to `test/datasets` keyed by the committed `CiMini` folder (no hardcoded path). All fixture references (`SPACINI29/TINY`, `SPACINI29-INPUTS.xlsx`, `SmallTest/*`) repointed to CiMini. CI `--filter` exclusion removed from `ci.yml`. Post-T-2800: all 12 `PipelineIntegrationTests` methods green with `Transform=true` against real CiMini fixture.

**Files:** `jb/src/tests/Prism.Core.Tests/PipelineIntegrationTests.cs`, `.github/workflows/ci.yml`

---

### T-2100 · Implement Tx_DetailCropper pixel flow
**Done.** Full 6-branch decision tree covering every bbox edge-intersection pattern. Crop-sizing driven by `Transformation.Cropping` config via new `CropTransformSettings` struct. 29 tests, including regression tests for two coordinate-shift bugs found during implementation. Verified against real TinyTest fixture image.

**Files:** `jb/src/core/Images/Transform/Tx_DetailCropper.cs`, `CropTransformSettings.cs`, `IImageTransformation.cs`, `ImageTransformer.cs`, `jb/src/core/Services/TransformService.cs`, `jb/src/core/config/PrismConfiguration.cs`

---

### T-2200 · Spec and implement Tx_util_HeadCutter
**Done.** Algorithm B (full-image Haar face search, centroid Y < 50%, pick face furthest from top, cutY = face.Y + 0.75×face.Height) implemented. Algorithm A (anatomy-ratio guided search) deferred — jbtodo recorded.

**Files:** `jb/src/core/Images/Transform/Tx_util_HeadCutter.cs`

---

### T-2300 · User decisions: detail crop saliency, headcut, greedy crop
**Done.** Three product decisions recorded in jbtodo.md: BoundingBox from ImagePreProcessor is the sole saliency anchor; Headcut controlled by a bool threaded through the pipeline (from `has-human`); greedy crop aligns bbox center to canvas center with `Tx_util_BgStretch` background fill.

**Files:** `jb/src/core/Images/Transform/jbtodo.md`

---

### T-3000 · Parallelize image import normalization
**Done.** Both image loops now normalize via `Parallel.ForEach` capped at `Environment.ProcessorCount`; result accumulation moved to `ConcurrentBag<T>`; filename-uniqueness index moved to a job-scoped `Interlocked` counter. Already-conforming JPEGs are copied unchanged instead of decoded/re-encoded. `jb/src/core/IO/Import/jbtodo.md` closed and removed.

**Files:** `jb/src/core/IO/Import/Importer.cs`

---

### T-3100 · Bracket 4 (SemanticMatcher) perf: skip without CLIP tags; index its string scoring
**Done.** `ImageMatcher.RunWaterfall` skips `RunBracket4` entirely when no record has an influential CLIP tag. `StringMatcher.ScoreCandidatesByStringTokens` rewritten to reuse Bracket 3's inverted token index instead of an un-indexed per-family scan. 18 tests. Verified identical `FamilyId` assignments with/without `--skip-classification` on real TinyTest data.

**Files:** `jb/src/core/Images/ImageMatcher.cs`, `jb/src/core/Images/Match/SemanticMatcher.cs`, `jb/src/core/Images/Match/StringMatcher.cs`

---

### ONNX Singleton (M5 gate item)
**Done (2026-06-29).** `InferenceSession` hoisted from per-job to application-scoped singleton on `MatchingService`. `ClassificationService` now borrows the shared `ImageClassifier` (no longer owns/disposes it). `_clipLock` on `MatchingService` serializes all `Run()` calls (required for DML). Disposal chain: `MatchingService` → `Pipeline` → `PrismService` (all now implement `IDisposable`). PRISM-classify.md updated. Verified: two TinyTest jobs, CLIP tags in Lambda documents, probe fired once at startup.
