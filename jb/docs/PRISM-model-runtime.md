# PRISM — Model Runtime Policy
*Abbreviations: `GLOSSARY.md`*

Cross-cutting policy for every part of PRISM that loads and runs a model — CLIP (`ImageClassifier.cs`), YOLO (`YoloDetector.cs`), Upscale (`Upscaler_g_p_u.cs`), and any future analyzer or transformer. Not CLIP-specific; see `PRISM-classify.md` for classification-specific behavior (thresholds, taxonomy, prompts).

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

---

## Enforcement

`.claude/scripts/check-cs-conventions.ps1` (PostToolUse hook on every `.cs` edit) has an `onnx-session-bypass` category: any new `new InferenceSession(`, `AppendExecutionProvider_DML(`, or `new SessionOptions()` outside `OnnxSessionFactory.cs` fails the edit. This is delta-based (only new violations vs. git HEAD are reported), so it catches any future code that bypasses the factory without re-flagging history.

---

## Health surface

`GET /PRISM/health`'s `SessionRuntimeProviders` field (`jb/src/api/RuntimeProviderProbe.cs`) reports what each component's session actually opened with. Because CLIP/YOLO/Upscale all share one factory and one gate, all three are guaranteed identical — the probe reads `ImageUpscaler.IsGpuAvailable` once and applies it to all three labels rather than querying each session independently. If this drifts (e.g. a future component genuinely needs a different policy), that's a policy exception significant enough to require updating this document first.

---

## Re-verification after any version bump

Changing the pinned ONNX Runtime version can shift CLIP's floating-point output, which can flip near-tied det-slot ordering (guards T-2820's determinism). After any bump to `Directory.Packages.props`:

```
pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini
```

Run **5 consecutive times** (no code change between runs) and confirm byte-identical `FinalFileName`/`DetOrder` for every image, per T-2820's original acceptance bar.
