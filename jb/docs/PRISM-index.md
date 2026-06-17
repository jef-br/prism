# PRISM Documentation Index

Load only the files relevant to your current task. Abbreviations: `GLOSSARY.md`.

## File Map

| File | Covers |
|---|---|
| `GLOSSARY.md` | All abbreviations used across docs |
| `PRISM-overview.md` | System specs, accepted media, batch limits, terminology |
| `PRISM-pipeline-core.md` | Stage order, Prism.cs / Pipeline.cs, PJR, PJRes, failure policies, config lifecycle |
| `PRISM-excel.md` | IEM, header detection, deduplication, PK rules, column validity |
| `PRISM-io-import.md` | Import strategies, path/stream/multipart/URL/zip/directory, flat JPG, EXIF, corrupt-image KO, original-image export policy |
| `PRISM-classify.md` | ONNX, classification thresholds, orientation values, border intersection, human detection, head visibility, UNKNOWN states |
| `PRISM-match.md` | Waterfall gates, NumericMatcher, StringMatcher, ImageLabelingMatcher, tie-breaking, synonyms, stop words, NoiseFilter |
| `PRISM-order-rename.md` | `_det` suffix, ordering rules, output filename stem, unmatched naming |
| `PRISM-transform-generate.md` | Transformation decisions, background extension, generation logic |
| `PRISM-api.md` | HTTP contracts, request/response shapes, SSE, health/config endpoints, error payloads, URL validation, request size |
| `PRISM-workbench.md` | Shared web+WPF behavior, web upload/layout, WPF direct invocation, allowed differences, no-hidden-behavior rule |
| `PRISM-models.md` | All C# record field definitions: IRI/IRL/IRO/IRG, FR, BM/BMS/MIR, ME, ITR, PPE |
| `PRISM-knowledge-base.md` | Consolidated reference: architecture, all CFG values, data model hierarchy, API summary, design principles |
| `ImageNGP/imagePhenotypes.md` | 26 INGP phenotype definitions |
| `ImageNGP/ImageFeatures.md` | 40 IF catalog: datatypes, values, difficulty, confidence |
| `ImageNGP/NGP-architecture.md` | Mapping model (deliverable 6), detection architecture (7), workbench concept (8) |
| `ideas-on-NGP.md` | Plain-language NGP insights (10 insights + open questions) |

## Task → Files to Load

| Task | Load |
|---|---|
| Working on `Prism.cs` / `Pipeline.cs` | `PRISM-pipeline-core.md` |
| Working on `Importer.cs`, IO, fetchers, zip | `PRISM-io-import.md` |
| Working on IEM, Excel parsing | `PRISM-excel.md` + `PRISM-models.md` |
| Working on matchers (Numeric/String/ImageLabeling) | `PRISM-match.md` + `PRISM-models.md` |
| Working on `ImageClassifier`, ONNX | `PRISM-classify.md` + `PRISM-models.md` |
| Working on `ImageOrderer.cs`, rename | `PRISM-order-rename.md` + `PRISM-match.md` |
| Working on `ImageTransformer.cs`, Tx classes | `PRISM-transform-generate.md` + `PRISM-classify.md` |
| Working on generation logic | `PRISM-transform-generate.md` |
| Working on API controllers, SSE | `PRISM-api.md` + `PRISM-pipeline-core.md` |
| Working on web workbench | `PRISM-workbench.md` + `PRISM-api.md` |
| Working on WPF workbench | `PRISM-workbench.md` + `PRISM-pipeline-core.md` |
| Defining/updating any C# model/record | `PRISM-models.md` |
| General orientation | `PRISM-overview.md` + `PRISM-pipeline-core.md` |
| INGP phenotypes / DO assignment | `ImageNGP/imagePhenotypes.md` + `ImageNGP/ImageFeatures.md` |
