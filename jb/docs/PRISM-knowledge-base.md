# PRISM Knowledge Base
*Abbreviations: `GLOSSARY.md`. For task-specific detail, use `PRISM-index.md` to load only the relevant doc.*

---

## Core Architecture

### Prism.cs — Pipeline Facade

- Entry point of `Prism.Core`. Management-only code that calls classes doing the real work.
- Receives input + PRISM-owned `JobID`, builds/completes PJR, passes to `Pipeline.cs`, returns PJRes.
- Cleanup: calls `JobCleaner.cs` / `JobErrorHandling.cs` — no inline cleanup or error logic.
- Resource management: `Initialize()` → `Run()` → `IDisposable` for `InferenceSession`/`Mat`; always release in `finally`/`using`.

### Data Flow

1. Accept input via `Importer.cs`
2. Build IEM via `ModelBuilder.cs`
3. Unpack ZIPs via `ZipHandler`
4. Classify via `ImageClassifier` (ONNX/CLIP)
5. Match via `ImageMatcher` (numeric/string/CV waterfall)
6. Order via `ImageOrderer`
7. Transform via `ImageTransformer`
8. Rename to `{FID}_det{N}.jpg`
9. Export via `Exporter` (ZIP or JSON)

### Batch Capacity

- Configured cap: 2500 images; hard ceiling: 5000
- Max image filesize: 25 MB (configurable in CFG)
- Designed for ~250 concurrent users, ~4 batches/day each

---

## Pipeline Stages (Immutable Order)

```
Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported
```

| Stage | Key model |
|---|---|
| Imported | IRI |
| Classified | IRI + IFs + candidate INGP |
| Matched | IRL + ME |
| Ordered | IRL + OrderEvidence, DO set |
| Renamed | IRL, NewName computed |
| Generated | IRG, `GenerationRouteState` on IRL |
| Transformed | ITR on IRL |
| Exported | IRO, BM, MIR |

---

## Configuration: CFG

**Location:** `jb/src/core/Prism_Config.json`

### Input Constraints
```json
{
  "Input": {
    "MAXIMUM_REQUEST_SIZE": 2684354560,
    "Images":  { "amount": { "min": 1, "max": 2500 }, "filesize": { "min": 2048, "max": 26214400 } },
    "ZIP":     { "NestDepth": 5, "amount": { "min": 0, "max": 50 }, "filesize": { "min": 1048, "max": 2147483648 } },
    "EXCEL":   { "amount": { "min": 1, "max": 10 }, "filesize": { "min": 9216, "max": 1048576 } }
  }
}
```

### Output Constraints
```json
{
  "Output": {
    "Images": {
      "Processed":  { "MINIMUM_SIZE_IN_PIXELS": { "width": 800, "height": 800 }, "MAXIMUM_SIZE_IN_PIXELS": { "width": 2000, "height": 2000 } },
      "Resize":     { "MAXIMUM_UpScale": 1.42, "MAXIMUM_DownScale": 0.001 },
      "Generated":  { "MINIMUM_SIZE_IN_PIXELS": { "width": 800, "height": 800 }, "MAXIMUM_SIZE_IN_PIXELS": { "width": 1410, "height": 1410 } }
    }
  }
}
```

### Matching Weights
```json
{
  "Classification": {
    "Confidence_Threshold": 0.9,
    "Cutoff_Threshold": 0.25,
    "Weights": {
      "NumericToken_Weight": 0.55,
      "StringToken_Weight": 0.15,
      "Classification_Weight": 0.15,
      "SemanticalRelevanceWeight": 0.15,
      "CONVERGENCE_WEIGHT": 0.25
    }
  }
}
```

### Other Keys
```json
{
  "Generation": { "InputImages": { "MINIMUM_SIZE_IN_PIXELS": { "width": 1600, "height": 1600 }, "MAXIMUM_SIZE_IN_PIXELS": { "width": 4000, "height": 6000 } } },
  "Transformation": {
    "Positioning": { "Center": true, "Margin": 0.042, "BothAxis": true },
    "Cropping": { "Coverage": 0.8, "Extension": { "OneSided": 0.14, "BiDirectional": 0.25 } }
  },
  "Pipeline": { "JobRetries": 3 },
  "Jobs": { "JobRetentionPeriodInHours": 24 }
}
```

---

## Configuration: XCFG

**Location:** `jb/src/core/Excel/ExcelConfig.json`

```json
{
  "RecordPrimaryKey": "FamilyID",
  "HeaderRowIndicators": [
    "fam", "famID", "family", "famille", "familleID",
    "ean", "sku", "refco", "reference", "veepee", "ref", "ngp", "lot", "pack",
    "label", "marque", "produit", "societe",
    "color", "material", "composition", "motif", "description", "designation",
    "dimension", "hauteur", "largeur", "longeur",
    "weight", "poids", "fit", "rise info", "waist", "sleeve",
    "fastening", "pocket", "compartment", "washing instructions", "style", "type of product"
  ],
  "HeaderRowSearchSpace": { "FirstRow": 0, "LastRow": 20, "FirstColumn": 0, "LastColumn": 20 }
}
```

---

## Data Privacy & Cleanup

- All imported files deleted after output sent to client.
- Small batches: in-memory. Large batches: spill to `/tmp` per job.
- Remove all imported file traces after successful export.

---

## Import/Export Rules

- **Excel:** `.xlsx` only. Must contain FID column.
- **ZIP:** max depth 5, 0–50/request, 1 KB – 2 GB each. Excel inside → Excel collection; images inside → flat JPG.
- **Images:** JPG/JPEG, PNG, TIF/TIFF, PDF, WebP, BMP, GIF. Multipage TIFF/PDF rendered per page. Alpha → `#ffffff` on JPG flatten. EXIF applied at import.
- PRISM-owned files missing/invalid → FFAIL before accepting jobs.
- User-supplied files corrupt/unsupported/unmatched → KO in manifest; job continues.

---

## Data Model Hierarchy

See `PRISM-models.md` for authoritative field-level definitions.

| Record | After | Key fields |
|---|---|---|
| IRI | Import | source provenance, normalized JPG ref, dimensions, import status |
| IRL | Classification onwards | IFs, INGP, ME, ordering result, generation state, ITR, IRO link |
| IRG | Generation | source FID, hero ref, generation method, output path, KO reason |
| IRO | Export | final filename, MIME type, artifact ref, dimensions, byte length, export status |
| MIR | Export | SourceReference, FinalFileName, Status, KoReasonCode, FID, DetOrder, TransformerType, TransformationStatus |
| BMS | Export | ImageCount, ExcelCount, ZipCount, OkRenamed, KoRecords, OkTransformed, KoTransformed, GeneratedCount |
| FR | IEM build | FID, CanonicalProperties, ColumnClassifications, NormalizedTokens, OriginalSourceCellValues, ConflictEvidence |

---

## API Contract Summary

Full contracts in `PRISM-api.md`.

- **`POST /PRISM/process`** — multipart: `request` (JSON) + repeated `input` (file). Returns envelope: `JobID`, `progressUrl`, `resultUrl`.
- **`GET /PRISM/jobs/{JobID}/progress`** — SSE: stage name, current item, counts, severity, safe message.
- **`GET /PRISM/jobs/{JobID}/result`** — ZIP (`OK/`, `KO/`, `manifest.json`, Excel) or JSON (`{ manifest, images: { ok[], ko[] } }`).
- **`GET /PRISM/health`** — job counts, config/model/disk readiness, supported providers.
- **`GET /PRISM/config`** — accepted media, size limits, visible feature flags. Hides local paths.

---

## Design Principles

1. **Configuration-driven**: All parameters in JSON files, never hardcoded.
2. **FFAIL**: PRISM files missing/invalid → stop immediately; user files → KO + manifest.
3. **Explicit resource management**: `Initialize()` → `Run()` → `Dispose()`.
4. **Story-readable code**: `Prism.cs` and main flows read like a recipe.
5. **Immutable pipeline order**: Stage sequence never changes.
6. **Privacy-first**: All imported files deleted after export.
7. **Careful file handling**: User problems → verbose reason in manifest.

---

**Last updated:** M4 complete 2026-06-17. 130/130 tests green. All 8 stages implemented.
