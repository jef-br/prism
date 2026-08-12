# PRISM — Model Runtime Policy
*Abbreviations: `GLOSSARY.md`*

Cross-cutting policy for every part of PRISM that loads and runs a model — CLIP (`ImageClassifier.cs`), YOLO (`YoloDetector.cs`), Upscale (`Upscaler.cs`), and any future analyzer or transformer. Not CLIP-specific; see `PRISM-classify.md` for classification-specific behavior (thresholds, taxonomy, prompts).

---

## Mandate (2026-07-15, user; T-4110)

Every part of PRISM that runs a model MUST use:
- The **same ONNX Runtime DirectML package, at the same version**, repo-wide.
- The **same execution-provider policy**: CPU-only is the mandatory baseline (must run on servers/laptops without a GPU); DirectML (GPU) is used automatically when a hardware adapter is present. GPU is a bonus resource only — its absence must never fail a job or disable a model-dependent stage.

This applies to CLIP, YOLO, and Upscale today, and to every future model-running component without exception.

---

## Single pinned version

`Microsoft.ML.OnnxRuntime.DirectML` is pinned once, centrally, in `jb/src/Directory.Packages.props` (MSBuild Central Package Management — `ManagePackageVersionsCentrally = true`). Every consuming `.csproj` references the package with no `Version` attribute; the version is resolved from that one file. `SixLabors.ImageSharp` and `OpenCvSharp4`/`OpenCvSharp4.runtime.win` are centralized the same way, in the same file, for the same reason (one source of truth, no silent skew across projects that share a process).

Do not add a per-project `Version` on any of these four `PackageReference` entries — CPM rejects it (NU1008). To bump a version, change it in `Directory.Packages.props` only, then re-verify per the checklist below.

---

## Single construction path: `OnnxSessionFactory`

`jb/src/core/Services/Matching/OnnxSessionFactory.cs` is the **sole** place in PRISM allowed to construct `SessionOptions`/`InferenceSession`:

```csharp
internal static InferenceSession Create(string modelPath) {
    var opts = new SessionOptions();
    if (GpuProbe.HasHardwareDirectMLAdapter())
        opts.AppendExecutionProvider_DML(0);
    return new InferenceSession(modelPath, opts);
}
```

It's `internal`, file-linked (via `<Compile Include>`) into every project that loads a model — the same convention already used for `GpuProbe.cs`, which it reuses for the hardware probe. Each consuming assembly compiles its own private copy; there is no shared binary dependency to add.

**Every model-running component calls `OnnxSessionFactory.Create(modelPath)` — no exceptions.** No component builds `SessionOptions` or calls `AppendExecutionProvider_DML`/`new InferenceSession(...)` itself. This is enforced, not just documented — see below.

Adding a new model-running component (a new analyzer, a segmentation transformer, etc.): file-link `OnnxSessionFactory.cs` into its project next to `GpuProbe.cs`, call `OnnxSessionFactory.Create(modelPath)` inside its own `Initialize()`, and keep the surrounding lifecycle (validate → initialize → try/catch/finally → `IDisposable`) exactly as CLIP/YOLO/Upscale already do. Do not write a new probe-and-append block — that duplication is exactly what T-4110 removed.

**No algorithm switching on GPU presence (2026-07-20, user decision).** A component must never gate *loading its model* on `GpuProbe`/`IsGpuAvailable` — the model loads on every host and the factory alone decides the execution provider. Upscale was the last violator: it used to skip Real-ESRGAN entirely without a GPU and silently swap in Lanczos4 (capped ×1.42). That fallback class (`Upscaler_c_p_u`) and the `ImageUpscaler` router are deleted — the single `Upscaler` class (`Services/Upscale/Engine/Upscaler.cs`) is the whole upscale path, and Lanczos4 survives only as its in-model top-up resize after the fixed ×2 SR step.

**Missing or corrupt model asset = loud startup failure (2026-07-20, user decision).** No model degrades silently to a lesser algorithm — unless that model's `UseIt` toggle is off, which is the one deliberate, config-declared exception (see "Per-model AI toggles" below). Existence: `PrismConfiguration.ValidateModelAssets` fails config load without the YOLO or Real-ESRGAN asset; `ClassificationService.ResolveClassifierPaths` does the same for the CLIP assets. Corruption: all three loaders (`Upscaler.Initialize`, `YoloDetector.Initialize`, `ImageClassifier.Initialize`) throw `PrismConfigurationException` — message naming the file as corrupt/truncated/incompatible, inner exception preserved — when a model file is present but fails to load. A quiet `IsReady = false` exists only for the missing-file case inside the loaders, because existence is already enforced loud upstream. Contract pinned by tests: `UpscalerTests`, `YoloDetectorTests`, `ImageClassifierTests` each assert corrupt-file → throws and missing-file → quiet not-ready (garbage-byte fixtures, CI-runnable without real models). The former monolith degrade path (T-2800's swallow in `PipelineServiceFactory.EnsureUpscalerReady`) is removed. The ×1.42 upscale KO bound is unrelated to any of this — it lives in `ImagePreProcessor` (`PREPROCESS_UPSCALE_EXCEEDED`, config `Output.Images.Resize.MAXIMUM_UpScale`) and is about refusing quality-destroying scale factors, not about hardware.

---

## Per-model AI toggles (2026-08-12)

Each model's own section in `Prism_Config.json`'s `Models` block carries a `UseIt` boolean. The section
names are the model's *job*, not its vendor: `classification` (CLIP), `Detection` (YOLO26), `Upscaling`
(Real-ESRGAN), and `Generation` (the not-yet-built generation backend, no other fields).

```json
"Models": {
    "classification": { "Dir": "…", "Model": "…", "Vocab": "…", "Merges": "…", "UseIt": true },
    "Upscaling":      { "Path": "…", "UseIt": true },
    "Detection":      { "Path": "…", "UseIt": true },
    "Generation":     { "UseIt": false }
}
```

`PrismConfiguration` exposes them as `AiClassificationEnabled` / `AiDetectionEnabled` /
`AiUpscalingEnabled` / `AiGenerationEnabled`, all `required` with no initializer — a missing or
misspelled `UseIt` throws `PrismConfigurationException` at load, per the repo's no-shadow-defaults rule.

**The governing rule: every feature value starts at UNKNOWN and is only overwritten by an actual
measurement.** A toggle never skips an analyzer and never introduces a parallel code path — every
analyzer is still called on every image exactly as normal. The gate sits *inside* the analyzer at the
point where it would consume the model's output, so a closed gate simply leaves the feature at its "I
don't know" default. Each toggle reuses the plumbing that already existed for "model unavailable":

| Toggle | Model-load gate | Downstream behavior when off |
|---|---|---|
| `classification` | `MatchingService` constructor skips `ClipPromptCatalog` + `ImageClassifier.GetShared`; both fields stay null | `ClassificationService.IsReady` reports false — the same state an absent CLIP file already produced. `MatchAsync`'s existing `doClassify` guard needs no change. `ImageMatcher` already auto-skips Bracket 4 with no CLIP tags; `SemanticMatcher` already passes candidates through unfiltered |
| `Detection` | `FeatureAnalysisService` never resolves the YOLO asset; `yoloModelPath` stays null | `ImageFeatureAnalyzer.Refine` already feeds `[]` to every analyzer on a null path. `Analyzer_SubjectGeometry` falls through to its existing CV-based box; `Analyzer_MultipleProducts` already returns early. **`Analyzer_HasHuman` needed an explicit gate** — its empty-detections branch writes a *confident* `false`, correct when YOLO ran and found nobody, wrong when YOLO never ran, and `detections.Count == 0` cannot distinguish the two |
| `Upscaling` | `PipelineServiceFactory.EnsureUpscalerReady` and the ServiceHost transform branch skip `UpscaleService.Create` | `TransformService` ANDs the toggle into `allowEsrganUpscale`, so every image takes the Lanczos path T-4900 already built, capped at `MAXIMUM_UpScale_LanczosOnly`. Past that cap the image is KO'd rather than shipped soft |
| `Generation` | none — no backend exists | Shipped `false`, which reproduces the previously-hardcoded `GenerationBackendAvailable = false` exactly. **Do not default it to `true`**: `ImageGenerator.Run` would skip creating the `Gated` placeholder record and the family would silently produce nothing |

**Asset validation follows the toggle.** `ValidateModelAssets` skips the existence check for a model
whose `UseIt` is false — otherwise switching a model off *because* its file is missing or known-bad
would still fail startup, defeating the point. A model that is on is validated exactly as loudly as
before.

**`PRISM_SERVICE=upscale` with `Upscaling.UseIt=false` fails loud.** A host whose only reason to exist
is a disabled model is a config contradiction, not a degraded mode. The default all-services host
instead just drops the upscale route, since every transform host reading the same config has already
forced itself onto Lanczos and nothing addresses it.

**The manifest records the outcome.** `BatchManifest.Models` (`BatchManifestModelToggles`) sits
immediately after `Summary` and reports `Classification` / `Detection` / `Upscale` / `Generation` for
the job, so a manifest read later still distinguishes "the model measured nothing" from "the model
never ran". Threaded `PrismConfiguration` → `Pipeline` → `ExportRequest` → `Exporter.BuildManifest`,
mirroring `DetOrderGapsAllowed`.

---

## Enforcement

`.claude/scripts/check-cs-conventions.ps1` (PostToolUse hook on every `.cs` edit) has an `onnx-session-bypass` category: any new `new InferenceSession(`, `AppendExecutionProvider_DML(`, or `new SessionOptions()` outside `OnnxSessionFactory.cs` fails the edit. This is delta-based (only new violations vs. git HEAD are reported), so it catches any future code that bypasses the factory without re-flagging history.

---

## Health surface

`GET /PRISM/health`'s `SessionRuntimeProviders` field (`jb/src/api/RuntimeProviderProbe.cs`) reports what each component's session actually opened with. Because CLIP/YOLO/Upscale all share one factory and one gate, all three are guaranteed identical — the probe reads `Upscaler.IsGpuAvailable` once and applies it to all three labels rather than querying each session independently. If this drifts (e.g. a future component genuinely needs a different policy), that's a policy exception significant enough to require updating this document first.

---

## Re-verification after any version bump

Changing the pinned ONNX Runtime version can shift CLIP's floating-point output, which can flip near-tied det-slot ordering (guards T-2820's determinism). After any bump to `Directory.Packages.props`:

```
pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini
```

Run **5 consecutive times** (no code change between runs) and confirm byte-identical `FinalFileName`/`DetOrder` for every image, per T-2820's original acceptance bar.
