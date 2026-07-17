# PRISM Abbreviation Glossary

Used across all `jb/docs/` files, `AGENTFEEDBACK.md`, and `PRISM-information.md`. Where a full form appears in code (class names, method names, JSON keys), it is not abbreviated.

| Abbr | Full form |
|---|---|
| PRISM | The application / pipeline |
| CFG | `Prism_Config.json` at `jb/src/core/` |
| XCFG | `ExcelConfig.json` at `jb/src/core/Excel/` |
| MCFG | `MatchingConfig.json` at `jb/src/core/config/` |
| HCFG | `HostRules.json` at `jb/src/core/IO/cfg/` |
| IEM | InternalExcelModel (collated, deduplicated Excel worksheets) |
| FID | FamilyID (primary product identifier; becomes output filename stem) |
| PK | Primary Key |
| FR | FamilyIDRecord |
| IRI | ImageRecord_INPUT |
| IRL | ImageRecord_LAMBDA |
| IRO | ImageRecord_OUTPUT |
| IRG | ImageRecord_GENERATED |
| BM | BatchManifest |
| BMS | BatchManifestSummary |
| MIR | ManifestImageRow |
| ME | MatchEvidence |
| PPE | PipelineProgressEvent |
| PJR | PrismJobRequest |
| PJRes | PrismJobResult |
| PPP | PrismProcessingParameters |
| IF | ImageFeature |
| INGP | ImageNGP (phenotype derived from a combination of IFs) |
| DO | DetOrder |
| DOR | `DetOrderRules.json` at `jb/src/core/config/` |
| PT | ProductType |
| KO | Failed/rejected item (recorded in manifest; job continues when valid work remains) |
| OK | Successful item |
| FFAIL | Fail fast and loud — PRISM-owned config/model failure → stop job before pipeline |
| EtD | easy_to_detect (phenotype catalog field) |
| TCD | Tokenized Concatenation Distance |
| SSE | Server-Sent Events |
| PAF | Part Affinity Field (pose estimation method) |
