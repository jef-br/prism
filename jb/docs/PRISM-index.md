# PRISM Documentation Index

Load only the files relevant to your current task.

## File Map

| File | Covers |
|---|---|
| `PRISM-overview.md` | System specs, accepted media, batch limits, terminology/vocabulary |
| `PRISM-pipeline-core.md` | Stage order, Prism.cs / Pipeline.cs architecture, PrismJobRequest, PrismJobResult, failure policies, config lifecycle |
| `PRISM-excel.md` | Internal Excel Model (IEM), header detection, deduplication, primary key rules, column validity |
| `PRISM-io-import.md` | Import strategies, path/stream/multipart/URL/zip/directory input handling, flat JPG conversion, EXIF, alpha, corrupt-image KO, original-image export policy |
| `PRISM-classify.md` | ONNX runtime, classification confidence thresholds, orientation values, border intersection, human detection, head visibility, unknown states |
| `PRISM-match.md` | Waterfall matching gates, NumericMatcher, StringMatcher, ImageLabelingMatcher, tie-breaking, language/synonym handling, stop words, NoiseFilter |
| `PRISM-order-rename.md` | `_det` suffix, ordering rules, output filename stem, unmatched image naming |
| `PRISM-transform-generate.md` | Transformation decisions, background extension, generation logic |
| `PRISM-api.md` | HTTP API contracts, request/response models, progress SSE, health/config endpoints, error payloads, URL validation, request size validation |
| `PRISM-workbench.md` | Shared web+WPF behavior, web-specific layout/upload, WPF-specific local invocation, allowed differences, no-hidden-behavior rule |
| `PRISM-models.md` | All C# record/model field definitions: ImageRecord_INPUT/LAMBDA/OUTPUT/GENERATED, FamilyRecord, BatchManifest, MatchEvidence, ImageTransformationResult, PipelineProgressEvent, InternalExcelModel mapping |

## Task → Files to Load

| Task | Load |
|---|---|
| Working on `Prism.cs` or `Pipeline.cs` | `PRISM-pipeline-core.md` |
| Working on `Importer.cs`, IO, fetchers, zip | `PRISM-io-import.md` |
| Working on `InternalExcelModel.cs`, Excel parsing | `PRISM-excel.md` + `PRISM-models.md` |
| Working on matchers (`NumericMatcher`, `StringMatcher`, `ImageLabelingMatcher`) | `PRISM-match.md` + `PRISM-models.md` |
| Working on `ImageClassifier`, ONNX, `Preprocessor.cs` | `PRISM-classify.md` + `PRISM-models.md` |
| Working on `ImageOrderer.cs`, rename | `PRISM-order-rename.md` + `PRISM-match.md` |
| Working on `ImageTransformer.cs`, `Tx_ProblemImageProcessor.cs` | `PRISM-transform-generate.md` + `PRISM-classify.md` |
| Working on generation logic | `PRISM-transform-generate.md` |
| Working on API controllers, request/response, SSE | `PRISM-api.md` + `PRISM-pipeline-core.md` |
| Working on web workbench | `PRISM-workbench.md` + `PRISM-api.md` |
| Working on WPF workbench | `PRISM-workbench.md` + `PRISM-pipeline-core.md` |
| Defining or updating any C# model/record | `PRISM-models.md` |
| General orientation / unfamiliar with the project | `PRISM-overview.md` + `PRISM-pipeline-core.md` |
