# PRISM Core Integration Tests

End-to-end integration tests for the full PRISM pipeline using the xUnit test framework.

## Test Structure

- **PipelineIntegrationTests.cs** — End-to-end tests exercising the complete pipeline.

## Tests

### SPACINI29_TINY_EndToEnd_VerifiesAllEightStagesInOrder
Primary test for T-150 acceptance criteria. Validates:
1. All 8 definitive pipeline stages appear in RouteSummaries in correct order
2. Job completes with status "Completed"
3. Manifest contains summary data

Uses test/datasets/TinyTest fixture (11 small JPGs) and SPACINI29-INPUTS.xlsx.

### PrismJobRequest_WithMinimalInput_AcceptsJob
Verifies that minimal valid input (1 image + 1 Excel file) is accepted without error.

### BatchManifest_AlwaysContainsRouteSummaries
Confirms RouteSummaries list is always populated and non-empty.

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

## Fixture Data
Tests use real fixture data from `jb/testing/SPACINI29/`:
- `TINY/` — Subset of 11 images for fast tests
- `SPACINI29-INPUTS.xlsx` — Excel mapping file

The fixture path is resolved dynamically at runtime by walking up the directory tree from the assembly location, with a fallback to a known absolute path.

## Assertions
Tests assert on `BatchManifest.RouteSummaries` only (not implementation details). The stage order assertion is definitive and immutable.