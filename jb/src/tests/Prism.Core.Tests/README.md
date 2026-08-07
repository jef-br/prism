# PRISM Core Integration Tests

End-to-end integration tests for the full PRISM pipeline using the xUnit test framework.

## Test Structure

- **PipelineFixture.cs** (lives in `Prism.Tests.Shared`, referenced by this project) — Runs each distinct pipeline configuration once and caches the result. Also shared by `Prism.Services.Matching.Tests` (MatchLite, SubjectEdgeDetector real-image tests), so it moved out of this project to a dedicated classlib (T-3300 step 3).
- **PipelineIntegrationTests.cs** — End-to-end assertions, all reading the fixture's cached results.

### Why a fixture

Every test used to call `new PrismService().Process(...)`, reloading the 146 MB CLIP and 37 MB YOLO ONNX
models and re-running all 8 stages. xUnit does not parallelize within a class, so eleven tests meant eleven
serial pipeline runs — 97% of the suite's wall clock. Only three request shapes actually existed, so the
fixture executes those three (`Default`, `Zip`, `Minimal`) once each against a single shared `PrismService`.
Suite time went from ~30 min to ~6 min.

Tests must never start a pipeline. Read `fixture.Default` / `fixture.Zip` / `fixture.Minimal` instead.

## Tests

### CiMini_EndToEnd_VerifiesAllEightStagesInOrder
Primary acceptance test. Validates:
1. All 8 definitive pipeline stages appear in RouteSummaries in correct order
2. Job completes with status "Completed"
3. Manifest contains summary data

### CiMini_ImagesAreAssociatedToFamilyId
Non-vacuous guard: OK rows must exist and carry a FamilyID. The other CiMini assertions are all satisfied
when every image is KO, so this is the one that catches an all-KO run. Also asserts a CLIP failure never
KOs an image (`CLASSIFY_ERROR`).

### CiMini_NoImagesSilentlyDropped
Every input image appears in either `OkImages` or `KoImages`.

### CiMini_OkImages_HaveWellFormedFinalNames / CiMini_KoImages_HaveReasonCode / CiMini_PairedImages_ShareFamily
Real-data quality contracts: `_det{n}` naming with no duplicates, every KO carries a reason code, and images
sharing a source stem (`2021_3024_46_A` / `_B`) resolve to the same FamilyId.

### CiMini_ZipFormat_ProducesNonEmptyBytes
Requesting `Format = "zip"` produces non-empty `ZipBytes`.

### PrismJobRequest_WithMinimalInput_AcceptsJob
Verifies minimal valid input (1 image + 1 Excel file) is accepted without error.

### BatchManifest_AlwaysContainsRouteSummaries
Confirms RouteSummaries is always populated and non-empty.

### ValidateExpectedStageOrder
Documents the definitive stage order: Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported.

## Running Tests
From the repository root:

```bash
dotnet test jb/src/PRISM.sln
```

Or run just the core tests:

```bash
dotnet test jb/src/tests/Prism.Core.Tests/Prism.Core.Tests.csproj
```

Model-dependent tests need the ONNX assets. On a machine without the source-tree copies, set
`PRISM_ONNX_MODEL_DIR` — see [`test/ci/README.md`](../../../../test/ci/README.md) for the required layout.

## Fixture Data

Tests use `test/datasets/CiMini/` — the **only committed dataset** (the rest of `test/datasets/` is
gitignored). It holds 97 loose images (jpg/png, some in subfolders) plus a 3-member zip and
`Brackets-Complete.xlsx`, and is paired with committed golden expectations (`expected-match.json`,
`expected-manifest.json`, `expected-phenotype.json`).

`PipelineFixture.ResolveTestFixturePath()` finds it by walking up from the test assembly looking for a
`test/datasets` folder containing `CiMini` — no hardcoded absolute path, so it resolves on any checkout
including the CI runner.

> Do not point tests at `test/datasets/TinyTest/`. It is a scratch set whose images are swapped in and out
> freely, and it is not committed. CiMini is the blessed golden fixture.

## Assertions
Tests assert on `BatchManifest.RouteSummaries` only (not implementation details). The stage order assertion
is definitive and immutable.
