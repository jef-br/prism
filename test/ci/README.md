# PRISM CI

Automated build + test gates and a real end-to-end pipeline run, executed on a **self-hosted Windows
runner** (the pipeline needs the CLIP ONNX model and real image data, neither of which is in git).

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
   the 145 MB model):

   ```
   C:\prism-ci-assets\models\Images\Classify\ONNX\clip-vit-b32-uint8\model_uint8.onnx
   C:\prism-ci-assets\models\Images\Classify\ONNX\clip-vit-b32-uint8\vocab.json
   C:\prism-ci-assets\models\Images\Classify\ONNX\clip-vit-b32-uint8\merges.txt
   C:\prism-ci-assets\models\Images\Upscale\ONNX\Real-ESRGAN_x2plus.onnx   # only if Full asserts upscaled output
   ```

3. **Machine-level environment variables** (System, so the runner service inherits them):

   ```
   PRISM_ONNX_MODEL_DIR = C:\prism-ci-assets\models
   ```

   `PrismConfigLocator.FindModelAsset` resolves each model relative path (now supplied by
   `Prism_Config.json` → `Models`) against this folder. Layout mirrors the `Dir`/`Path` values in the
   config's `Models` section.

4. **Toolchain**: install **.NET 9 SDK** (tests target `net9.0`), **.NET 10 SDK** (API/ServiceHost/WPF),
   and **Node 20+**. Verify: `dotnet --list-sdks`, `node -v`.

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

> **Status — Full run is currently red (by design).** `-Mode Match` passes and gates every PR.
> `-Mode Full` currently fails because the in-process/API pipeline does not initialize the GPU
> Real-ESRGAN upscaler (`Upscaler_g_p_u.Initialize()` is only wired in the ServiceHost, not the API),
> so Transform throws when it needs to upscale a small image on a GPU machine. CI surfaced this
> pre-existing pipeline bug; it is **out of scope for the CI setup** and tracked separately. Until it
> is fixed, `expected-manifest.json` cannot be captured and the daily `full-pipeline.yml` run reports
> red — which is CI correctly reporting a real failure, not a CI defect. Once the upscaler init is
> fixed, run `-Mode Full -Capture`, verify, and commit `expected-manifest.json` to turn it green.

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
