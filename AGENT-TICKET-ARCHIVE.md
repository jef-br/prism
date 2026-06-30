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

### ONNX Singleton (M5 gate item)
**Done (2026-06-29).** `InferenceSession` hoisted from per-job to application-scoped singleton on `MatchingService`. `ClassificationService` now borrows the shared `ImageClassifier` (no longer owns/disposes it). `_clipLock` on `MatchingService` serializes all `Run()` calls (required for DML). Disposal chain: `MatchingService` → `Pipeline` → `PrismService` (all now implement `IDisposable`). PRISM-classify.md updated. Verified: two TinyTest jobs, CLIP tags in Lambda documents, probe fired once at startup.
