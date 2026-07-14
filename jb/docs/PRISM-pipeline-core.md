# PRISM — Pipeline Core Architecture
*Abbreviations: `GLOSSARY.md`*

## Stage Order (definitive, immutable)

```
Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported
```

1. **Imported** — Import media, parse Excel → IEM, unpack zips, normalize images → JPG.
2. **Classified** — Deduplicate by visual hash (highest res wins); apply classification to every canonical image.
3. **Matched** — Tokenize each image via all matchers; compare vs IEM; resolve to one FID above threshold.
4. **Ordered** — Per FID: order images using INGP qualification, DOR, filename hints, classification labels. Single pass.
5. **Renamed** — Collapse FID + order → final filename.
6. **Generated** — For low-image-count families: copy hero image, create alternative (when enabled + quality sufficient).
7. **Transformed** — Transform each image per INGP config and per-image IFs.
8. **Exported** — Return output images + `manifest.json`.

**Stage rules:**
- Stage emits: OK records, KO records, warnings, diagnostics, progress events.
- User-file KO → job continues when valid work remains.
- PRISM-owned failure → job stops as `Failed`.
- No cancellation stage. Accepted jobs run to natural completion.

---

## `Prism.cs` — Facade

Management-only code. Must read like a story — calls classes that do the real work.
- Receives input + PRISM-owned `JobID`, builds/completes PJR, passes to `Pipeline.cs`, returns PJRes.
- Cleanup: calls `JobCleaner.cs` / `JobErrorHandling.cs` only — no inline cleanup or error logic.

---

## `Pipeline.cs` — Processing Ownership

Receives structured input from `Prism.cs`; returns structured output through the exporter.

Uses: Excel → IEM via `ModelBuilder.cs`, `FilenameTokenizer.cs`, `ImageMatcher.cs` (strategy pattern), `ImageOrderer.cs`, image transformation, generation, export.

---

## PJR — `PrismJobRequest`

C# PODO. Structured job contract passed into `Prism.Process`. All inputs normalized into PRISM input records before construction.

**Required fields:**
- `Guid JobID` — PRISM-owned. Caller-provided IDs must not become PRISM's internal JobID.
- `string? ClientRequestToken` — optional, echoed only for correlation.
- `IReadOnlyList<IRI> ImageRecords`
- `IReadOnlyList<InputExcelFileRecord> ExcelRecords`
- `IReadOnlyList<InputZipFileRecord> ZipFileRecords`
- `PPP PrismProcessingParameters`

**Rules:**
- Must not expose raw frontend upload objects, API types, or platform link objects.
- `Prism.Process` rejects before pipeline if: no accepted image records, no accepted Excel records, missing PPP, or invalid input record structure.
- User-file failures during import remain attached to input records for inclusion in BM.
- No cancellation path. Every accepted request runs start to finish.

---

## PJRes — `PrismJobResult`

C# PODO. Client-facing result from `Prism.Process`.

**Required content:**
- `Guid JobID`, `string? ClientRequestToken` (echoed unchanged)
- Job status: completed / completed with KO / failed
- Output image records and/or exported artifacts per PPP
- BM, KO records (all stages), safe stage summaries, warnings, optional diagnostic refs
- Export metadata: format, filenames/artifact refs, content types, byte counts
- Original image data only when `PPP.ReturnOriginalImages = true`

**Rules:**
- Original bytes excluded by default; never in `manifest.json`.
- Manifest is the audit contract; byte-heavy payloads stay in result/export-specific fields.

---

## Failure Policies

**User-file KO (continue):** corrupt/unsupported/unreadable media, bad zip members, Excel rows with invalid PK, worksheets with no PK column, unmatched images, images that cannot be generated or transformed. KO records stay attached to the relevant record. Bad zip members keep archive provenance.

**PRISM-owned failure (stop as `Failed`):** missing/invalid CFG or any required folder-local `..._config.json`; missing/invalid/incompatible model files; invalid internal settings/schemas/thresholds; unavailable required storage; exporter failure. Not converted to per-image KO. FFAIL before expensive work whenever possible.

---

## Configuration Lifecycle

- Config built **on server startup**: loads CFG + all required folder-local `..._config.json` files.
- Validated before `Prism.Process` starts. V1 queue settings loaded before accepting jobs.
- Missing/invalid PRISM-owned config → FFAIL.
- No mutable config reads mid-stage. Each job uses the effective config at acceptance time.
- Effective config snapshot available for manifest and diagnostics.

### Loading is two phases: load, then bundle (accepted 2026-07-12, T-4500 / T-4530 / T-4540)

A config area is **one JSON file with named top-level sections** (`transform_Config.json`,
`analyzer_Config.json`). There is no root config class per area — a root class fuses three jobs
(deserialization target, validator, parameter bundle) and only the third is worth keeping.

**Phase 1 — load.** `ConfigLoader.Section<T>(file, section)` reads and deserializes one section
independently. Every section class is `required`-props with **no in-code initializers**, and
self-validates by implementing `IValidatableConfig` — `ConfigLoader` calls `Validate()` immediately
after deserialization. A missing file, a missing/misspelled key, or an out-of-range value throws,
naming the file, the section, and the key. Sections are cached per (type, path, section, file
timestamp).

**Phase 2 — bundle.** The loaded sections are composed into a plain parameter object —
`TransformParameters`, `AnalyzerParameters` — via its `FromConfig()`. These are **not** config
loaders and **not** deserialization targets: they own no parsing and no validation, and every section
stays independently loadable without them (what per-section service hosts need). `FromConfig()` is
called once at a well-defined point — host startup (`PrismApiConfiguration.Load()`), service
construction (`FeatureAnalysisService`), or stage entry (`TransformService`) — never per image.

**Consequences, deliberately:**
- Config is **injected, not fetched**. Consumers receive their parameters; they do not reach for the
  filesystem. `ConfigLoader.Section<T>` costs two syscalls per call even when cached (path probe +
  timestamp), so a self-load inside a per-image `Parallel.ForEach` is an anti-pattern — rejected by name.
- **The one exception:** the fixed-signature webservice entry points `Process(byte[], int, float)` on
  `Tx_util_BgStretch` and `Tx_LowContrastEnhancement`. They have no parameter to receive config
  through, so they load their own section in the body of `Process()` itself — never in a shared
  helper that an in-pipeline path might also call. This is what replaced the old static
  `Configure()` push-in.
- Engine assemblies can do this because `ConfigLoader` compiles into `Prism.Core.Contracts`, which
  they already reference.

### One loader, one exception type, no config cache (accepted 2026-07-14, T-4560)

`PrismConfigLocator` and `ConfigCache` are **deleted**. Everything resolves config through
`ConfigLoader.RequireFile(name)` (path) / `ConfigLoader.Section<T>` / `Root<T>` (parsed), and model
assets through `ModelAssetLocator.Find(relativePath)`.

**Every config failure throws `PrismConfigurationException`** — the single fail-loud type across the
whole codebase. Not just `ConfigLoader`'s own failures (missing file, missing section, missing/misspelled
required key), but every section class's `Validate()` and every hand-written `Load(path)` parser:
Excel (`ExcelConfig` + its sub-configs), Analyzers, Classify (`ClipPromptCatalog`, `PhenotypeRuleSet`),
`MatchingConfig`, `TranslationConfig`, `DetOrderConfig`, `ProductTypeResolver`, Transform's `Admin/*Config`,
and `UpscaleConfig`. `HostRules_Config` and `ImageNgpVocabulary` already did.

It derives from `InvalidOperationException`, so any `catch (InvalidOperationException)` still catches it.
**But `Assert.Throws<T>` in xUnit is an exact-type match**, so tests assert
`Assert.Throws<PrismConfigurationException>` — asserting the base type fails.

**Not** converted, deliberately: failures that are *not* config — image-too-small
(`Tx_ProblemImageProcessor`), HTTP/WeTransfer fetch errors, `ServiceHttp` empty responses, the
`Upscaler_g_p_u.Initialize()` lifecycle guard, and user-workbook parsing in `ExcelFileHandler` (that is
user data, not PRISM-owned config). Those keep `InvalidOperationException`.

**Do not re-add a config cache.** `ConfigCache` memoized the hand-written `Load(path)` parsers
(`MatchingConfig`, `TranslationConfig`, `ExcelConfig`, `PhenotypeRuleSet`, `DetOrderConfig`,
`ProductTypeResolver`, `PrismConfiguration`). It was measured and removed:

- **All config JSON in the project totals 62 KB.**
- Every one of those load sites fires **once per job**, never per image — `ImageMatcher.Run` is a
  static per-job method, `MatchingService` constructs `FeatureAnalysisService`/`ClassificationService`
  once per job, `TransformService` bundles once per stage run.
- Total config parse cost is therefore single-digit-to-low-tens of **milliseconds per job**, against a
  job that runs CLIP + YOLO per image and Real-ESRGAN upscaling — on the order of **0.01% of a job**.

The cache bought nothing and cost a whole indirection layer. Those sites now call their parser
directly. This is **not** the same as `ConfigLoader`'s internal cache, which stays: the two
fixed-signature engine `Process()` entry points above self-load **per call**, and that one *is* the
per-image path.

---

## V1 Job Queue

- Single-server in-process bounded queue.
- `POST /PRISM/process` validates → creates PRISM-owned `JobID` → creates job record → enqueues.
- Queue = bounded .NET `Channel<T>` consumed by fixed background workers.
- Queue carries job references + metadata only (JobID, config snapshot ref, job folder ref, output format) — **not** image/Excel/zip bytes.
- Queue full → reject before job creation with pre-core API error. No `manifest.json` produced.
- Queued/running jobs are process-local in V1. Restart recovery not guaranteed.

---

## Source Tree Ownership

| Folder | Owns |
|---|---|
| `jb/src/core` | Pipeline behavior, model contracts, image processing, import/export, zip, runtime config |
| `jb/src/api` | HTTP contracts, request/response models, API validation, health/config endpoints, SSE |
| `jb/src/workbench/web` | Browser upload, API client, layout, progress, validation |
| `jb/docs` | Accepted project knowledge |
| `jbtodo.md` (folder-local) | Temporary working notes for unresolved/pending decisions |
