# PRISM Documentation Index

Load only the files relevant to your current task. Abbreviations: `GLOSSARY.md`.

## File Map

| File | Covers |
|---|---|
| [GLOSSARY.md](GLOSSARY.md) | All abbreviations used across docs |
| [architecture/ARCHITECTURE.md](architecture/ARCHITECTURE.md) | Architectural overview: system shape, deployment topologies, assembly map, the 8 stages, record lifecycle, job lifecycle, matching/classify/transform deep dives, cross-cutting rules. Nine editable `.drawio.svg` diagrams live beside it. Start here for orientation; every section points at the doc that owns the detail |
| [PRISM-overview.md](PRISM-overview.md) | System specs, accepted media, batch limits, terminology |
| [PRISM-pipeline-core.md](PRISM-pipeline-core.md) | Stage order, Prism.cs / Pipeline.cs, PJR, PJRes, failure policies, config lifecycle |
| [PRISM-excel.md](PRISM-excel.md) | IEM, header detection, deduplication, PK rules, column validity |
| [PRISM-io-import.md](PRISM-io-import.md) | Import strategies, path/stream/multipart/URL/zip/directory, flat JPG, EXIF, corrupt-image KO, original-image export policy, core co-deployment contract, Import→Match disk handoff decision |
| [PRISM-classify.md](PRISM-classify.md) | ONNX, classification thresholds, orientation values, border intersection, human detection, head visibility, UNKNOWN states |
| [PRISM-model-runtime.md](PRISM-model-runtime.md) | Cross-cutting ONNX Runtime policy: single package/version, `OnnxSessionFactory`, CPU-baseline/DirectML-when-present, enforcement |
| [PRISM-match.md](PRISM-match.md) | Waterfall gates, NumericMatcher, StringMatcher, ImageLabelingMatcher, tie-breaking, synonyms, stop words, NoiseFilter |
| [PRISM-order-rename.md](PRISM-order-rename.md) | `_det` suffix, ordering rules, output filename stem, unmatched naming |
| [PRISM-transform-generate.md](PRISM-transform-generate.md) | Transformation decisions, background extension, generation logic |
| [PRISM-api.md](PRISM-api.md) | HTTP contracts, request/response shapes, SSE, health/config endpoints, error payloads, URL validation, request size |
| [PRISM-workbench.md](PRISM-workbench.md) | Web workbench behavior, upload/layout, progress visualization, no-hidden-behavior rule |
| [PRISM-models.md](PRISM-models.md) | All C# record field definitions: IRI/IRL/IRO/IRG, FR, BM/BMS/MIR, ME, PPE |
| [PRISM-knowledge-base.md](PRISM-knowledge-base.md) | Consolidated reference: architecture, all CFG values, data model hierarchy, API summary, design principles |
| [PRISM-testing.md](PRISM-testing.md) | Test-suite layout, per-service suite filters, one-csproj decision, test conventions |
| [ImageNGP/imagePhenotypes.md](ImageNGP/imagePhenotypes.md) | 20 INGP phenotype definitions |
| [ImageNGP/ImageFeatures.md](ImageNGP/ImageFeatures.md) | 37 IF catalog: datatypes, values, difficulty, confidence |
| [ImageNGP/NGP-architecture.md](ImageNGP/NGP-architecture.md) | Mapping model (deliverable 6), detection architecture (7), workbench concept (8) |
| [ImageNGP/HowToAddAPhenotype.md](ImageNGP/HowToAddAPhenotype.md) | Step-by-step guide: adding a new analyzer/feature/phenotype/det-order mapping |
| [ImageNGP/phenotype-assignment-validation.md](ImageNGP/phenotype-assignment-validation.md) | T-4970 measurement: real phenotype distribution, why coverage is 7%, threshold precision/coverage curve, verdict on the BypassPhenotypes flip. **Second pass**: lowering the thresholds was tested and rejected — coverage 7%→62% but 0/5 correct at today's config; the real blockers are `model-detail-closeup` over-firing, a missing `back-on-model-partial` rule, and T-4955's 42% inconsistent snapshots |
| [ideas-on-NGP.md](ideas-on-NGP.md) | Plain-language NGP insights (10 insights + open questions) |
| [PRISM-postmortem-T6900-reasoning.md](PRISM-postmortem-T6900-reasoning.md) | Reasoning post-mortem: how three sessions chased a hang that did not exist. Five compounding errors (timeout read as evidence, busy CPU read as symptom, elimination over an assumed-complete list, plausible mechanism promoted to diagnosis, controls too small to falsify) + a checklist for the next "it hangs" report |

## Task → Files to Load

| Task | Load |
|---|---|
| Working on `Prism.cs` / `Pipeline.cs` | [PRISM-pipeline-core.md](PRISM-pipeline-core.md) |
| Working on `Importer.cs`, IO, fetchers, zip | [PRISM-io-import.md](PRISM-io-import.md) |
| Working on IEM, Excel parsing | [PRISM-excel.md](PRISM-excel.md) [PRISM-models.md](PRISM-models.md) |
| Working on matchers (Numeric/String/ImageLabeling) | [PRISM-match.md](PRISM-match.md) [PRISM-models.md](PRISM-models.md) |
| Working on `ImageClassifier`, ONNX | [PRISM-classify.md](PRISM-classify.md) [PRISM-models.md](PRISM-models.md) |
| Working on any ONNX/model-running code (new `InferenceSession`, new analyzer/transformer) | [PRISM-model-runtime.md](PRISM-model-runtime.md) |
| Working on `ImageOrderer.cs`, rename | [PRISM-order-rename.md](PRISM-order-rename.md) [PRISM-match.md](PRISM-match.md)|
| Working on `ImageTransformer.cs`, Tx classes | [PRISM-transform-generate.md] (PRISM-transform-generate.md) [PRISM-classify.md](PRISM-classify.md) |
| Working on generation logic | [PRISM-transform-generate.md](PRISM-transform-generate.md) |
| Working on API controllers, SSE | [PRISM-api.md] [PRISM-pipeline-core.md](PRISM-api.md) (PRISM-pipeline-core.md) |
| Working on web workbench | [PRISM-workbench.md](PRISM-workbench.md) [PRISM-api.md](PRISM-api.md) |
| Defining/updating any C# model/record | [PRISM-models.md](PRISM-models.md) |
| Writing or running tests | [PRISM-testing.md](PRISM-testing.md) |
| General orientation | [architecture/ARCHITECTURE.md](architecture/ARCHITECTURE.md), then [PRISM-overview.md](PRISM-overview.md) + [PRISM-pipeline-core.md](PRISM-pipeline-core.md) |
| Investigating a hang, a slow job, or "the pipeline is stuck" | [PRISM-postmortem-T6900-reasoning.md](PRISM-postmortem-T6900-reasoning.md) — read the checklist before forming a theory |
| INGP phenotypes / DO assignment | [ImageNGP/imagePhenotypes.md](ImageNGP/imagePhenotypes.md) [ImageNGP/ImageFeatures.md](ImageNGP/ImageFeatures.md) |
| Phenotype coverage / calibration / why a phenotype never fires | [ImageNGP/phenotype-assignment-validation.md](ImageNGP/phenotype-assignment-validation.md) |
