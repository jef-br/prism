# PRISM CI

Automated build + test gates and a real end-to-end pipeline run, executed on a **self-hosted Windows
runner** (the pipeline needs the CLIP and YOLO ONNX models and real image data, none of which is in git).

## Workflows

| Workflow | Trigger | What it does |
|---|---|---|
| [`ci.yml`](../../.github/workflows/ci.yml) | every PR + push to `main` | build solution, xUnit tests, web typecheck+build, **match-only** smoke on `CiMini` |
| [`full-pipeline.yml`](../../.github/workflows/full-pipeline.yml) | 10:30 Europe/Brussels workdays + manual | **full** classify→transform→export on `CiMini`, publish API/ServiceHost + web |

Both run on `runs-on: [self-hosted, windows]`.

## One-time runner setup

1. **Register the runner** (repo → Settings → Actions → Runners → New self-hosted runner, Windows).
   Give it the default labels `self-hosted, windows`. Install it **as a service** so scheduled runs
   fire without a logged-in session.

2. **Stable assets folder** outside the runner workspace (so `git clean` between runs never deletes
   the models). Pick any folder; its path goes in `PRISM_ONNX_MODEL_DIR` (step 3). The **layout beneath
   it is not free** — it must mirror the relative paths the code asks for:

   ```
   <PRISM_ONNX_MODEL_DIR>/
     Services/Matching/Classify/ONNX/clip-vit-b32-uint8/model_uint8.onnx   # 146 MB
     Services/Matching/Classify/ONNX/clip-vit-b32-uint8/vocab.json
     Services/Matching/Classify/ONNX/clip-vit-b32-uint8/merges.txt
     Services/Matching/Analyzers/ONNX/yolo26s.onnx                          # 37 MB
     Services/Upscale/Engine/ONNX/Real-ESRGAN_x2plus.onnx                   # 67 MB
   ```

   > These paths changed when `jb/src/core/` was restructured into `Services/` + `lib/`. They were
   > previously `Images/Classify/ONNX/…` and `Images/Upscale/ONNX/…`. A runner still holding the old
   > layout fails every model-dependent test with
   > `PrismConfigurationException: YOLO26 ONNX model not found`.

   Where each relative path comes from — **keep these in sync when moving a model**:

   | Asset | Source of the relative path |
   |---|---|
   | CLIP dir + filenames | `jb/src/core/config/Prism_Config.json` → `Models.Clip.Dir` / `.Model` / `.Vocab` / `.Merges` |
   | Real-ESRGAN | `jb/src/core/config/Prism_Config.json` → `Models.Upscale.Path` |
   | YOLO26 | **Not config-driven** — the `YoloModelRelativePath` const in `jb/src/core/Services/Matching/FeatureAnalysisService.cs` |

3. **Machine-level environment variables** (System, so the runner service inherits them):

   ```
   PRISM_ONNX_MODEL_DIR = C:\prism-ci-assets\models
   ```

   `PrismConfigLocator.FindModelAsset` resolves each relative path above in order: beside
   `Prism_Config.json`, then against `PRISM_ONNX_MODEL_DIR`, then the source-tree copy under
   `jb/src/core/` (a dev convenience — the models are gitignored, so this last hop never resolves on a
   fresh CI checkout).

4. **Toolchain**: install **.NET 9 SDK** (tests target `net9.0`), **.NET 10 SDK** (API/ServiceHost/WPF),
   and **Node 24+**. Verify: `dotnet --list-sdks`, `node -v`.

> **Availability**: CI only runs while the machine + runner service are up. The 10:30 slot assumes the
> machine is on by then; if it isn't, trigger `full-pipeline.yml` manually via **Run workflow**
> (workflow_dispatch always passes the schedule gate).

## The CiMini golden fixture

`test/datasets/CiMini/` is the only committed dataset (the rest of `test/datasets/` is gitignored). It
is small (<30 MB), deterministic, and paired with committed **golden** expectations:

- `expected-match.json` — `SourceReference -> FamilyId`, asserted by `-Mode Match`.
- `expected-manifest.json` — `Status / FamilyId / FinalFileName / DetOrder` per source, asserted by
  `-Mode Full`.

See [`../datasets/CiMini/README.md`](../datasets/CiMini/README.md) for how to build/refresh it.

### Running locally

```powershell
# Fast match-only gate (what PRs run):
pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Match -Dataset CiMini

# Full pipeline (what nightly runs):
pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full -Dataset CiMini
```

The script starts the API if needed (`Ensure-PrismApi`), submits the fixture, and compares the manifest
to the golden. It **fails** on any FamilyId/Status/filename/det mismatch, a missing output image, an
empty manifest, or an all-KO run (the vacuous-green guard).

### Golden drift — re-blessing after an intended change

When you deliberately change matcher/transform logic, the correct output changes and the golden files
go stale (the build goes red even though the new behaviour is right). This is expected maintenance:

```powershell
# 1. Re-run and eyeball the output is actually correct, then capture:
pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Match -Dataset CiMini -Capture
pwsh test/ci/Invoke-CiPipeline.ps1 -Mode Full  -Dataset CiMini -Capture
# 2. git diff the expected-*.json, confirm the change is what you intended, then commit.
```

`-Capture` writes the golden from the current run instead of asserting. Only commit it after a human
has verified the run is correct.
