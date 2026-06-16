# PRISM Knowledge Base

**Purpose**: This document consolidates all architectural, structural, and configuration knowledge from user-created C# and configuration files. Agents should reference this document as source of truth for business logic, constraints, and design decisions.

**Source Files**: Prism.cs, Prism_Config.json, ExcelConfig.json, Importer.cs, FamilyRecord.cs, ImageRecord_Base.cs, API Program.cs, and related contract definitions.

---

## Core Architecture

### System Entry Point: Prism.cs (The Pipeline Facade)

**Core Principle**: Prism.cs is the main entry point of Prism.Core library. It must contain minimal complex-looking code and read like a story understandable by a five-year-old.

**Data Flow**:
1. **Accept input** via `Importer.cs`
2. **Extract Excel model** via `ModelBuilder.cs` (creates Internal Excel Model with one record per FamilyID)
3. **Unpack ZIP files** via `ZipHandler`
4. **Classify images** via `ImageClassifier` (ONNX-based, assigns ImageType traits)
5. **Match images to families** via `ImageMatcher` (numeric, string, and computer vision matching)
6. **Order images** via `ImageOrderer` (determines ranking for website display)
7. **Transform images** via `ImageTransformer` (center object, add margins, clean background)
8. **Rename images** to deterministic names based on matching scores
9. **Export results** via `Exporter` (ZIP or JSON)

**Resource Management Policy**:
- Initialize all external resources (ONNX session, OpenCV allocations) in dedicated `Initialize()` method before `Run()`
- Implement `IDisposable` on any class owning `InferenceSession`, `Mat`, or unmanaged resources
- Always release resources in `finally` or via `using` statements
- Configuration: Load all `*_config.json` files on startup via dedicated config object

**Batch Capacity & Memory Policy**:
- **Designed to handle**: 1–5000 images at 2 MB per image comfortably
- **Configured batch cap**: 2500 images (prevents constant heavy stress)
- **Max concurrent users**: ~500 users, each uploading max 4 batches per day
- **Max image filesize**: 25 MB per image (configurable, see Prism_Config.json)
- **Spill to disk**: Use in-memory cache for small batches; spill to `/tmp` folder as needed based on available RAM

---

## Pipeline Stages (Immutable Order)

Pipeline stage order **must never change**:

```
Imported → Classified → Matched → Ordered → Renamed → Generated → Transformed → Exported
```

Each stage has associated data models:
- **Imported**: Raw files (Excel, ZIP, images)
- **Classified**: `ImageRecord_INPUT` with classification traits (Borders, Human, HeadVisible, Orientation, Type)
- **Matched**: `ImageRecord_LAMBDA` with matching evidence (numeric, string, CV scores)
- **Ordered**: `ImageRecord_LAMBDA` with ordering information
- **Renamed**: `ImageRecord_BASE` with `Family` and `DetOrder` properties
- **Generated**: `ImageRecord_GENERATED` (supplementary images)
- **Transformed**: `ImageRecord_TRANSFORMED` (processed output)
- **Exported**: `ImageRecord_EXPORTED` in ZIP or JSON format

---

## Configuration: Prism_Config.json

**Location**: `jb/src/core/Prism_Config.json`

All configuration must be loaded from JSON, never hardcoded.

### Input Constraints

```json
{
  "Input": {
    "MAXIMUM_REQUEST_SIZE": 2684354560,  // 2.5 GB max request
    "Images": {
      "amount": { "min": 1, "max": 2500 },
      "filesize": { "min": 2048, "max": 26214400 }  // 2 KB – 25 MB per image
    },
    "ZIP": {
      "NestDepth": 5,
      "amount": { "min": 0, "max": 50 },
      "filesize": { "min": 1048, "max": 2147483648 }  // 1 KB – 2 GB
    },
    "EXCEL": {
      "amount": { "min": 1, "max": 10 },
      "filesize": { "min": 9216, "max": 1048576 }  // 9 KB – 1 MB
    }
  }
}
```

### Output Constraints

```json
{
  "Output": {
    "Images": {
      "Processed": {
        "MINIMUM_SIZE_IN_PIXELS": { "width": 800, "height": 800 },
        "MAXIMUM_SIZE_IN_PIXELS": { "width": 2000, "height": 2000 }
      },
      "Resize": {
        "MAXIMUM_UpScale": 1.42,
        "MAXIMUM_DownScale": 0.001
      },
      "Generated": {
        "MINIMUM_SIZE_IN_PIXELS": { "width": 800, "height": 800 },
        "MAXIMUM_SIZE_IN_PIXELS": { "width": 1410, "height": 1410 }
      }
    }
  }
}
```

### Classification Scoring

```json
{
  "Classification": {
    "Confidence_Threshold": 0.9,     // ONNX model confidence floor
    "Cutoff_Threshold": 0.25,        // Score below this = no match
    "Weights": {
      "NumericToken_Weight": 0.55,   // Numeric matching contributes 55%
      "StringToken_Weight": 0.15,    // Fuzzy string matching contributes 15%
      "Classification_Weight": 0.15, // ONNX classification contributes 15%
      "SemanticalRelevanceWeight": 0.15,  // Semantic relevance contributes 15%
      "CONVERGENCE_WEIGHT": 0.25     // Weight given to agreeing matchers
    }
  }
}
```

### Generation Configuration

```json
{
  "Generation": {
    "InputImages": {
      "MINIMUM_SIZE_IN_PIXELS": { "width": 1600, "height": 1600 },
      "MAXIMUM_SIZE_IN_PIXELS": { "width": 4000, "height": 6000 }
    }
  }
}
```

### Transformation Configuration

```json
{
  "Transformation": {
    "Positioning": {
      "Center": true,          // Center main object in frame
      "Margin": 0.042,         // Whitespace margin as % of image size
      "BothAxis": true         // Apply centering to both axes
    },
    "Cropping": {
      "Coverage": 0.8,         // Crop covers 80% of visible object
      "Extension": {
        "OneSided": 0.14,      // One-sided background extension: 14%
        "BiDirectional": 0.25  // Two-sided background extension: 25%
      }
    }
  }
}
```

### Pipeline & Job Configuration

```json
{
  "Pipeline": {
    "JobRetries": 3  // Retry failed jobs up to 3 times
  },
  "Jobs": {
    "JobRetentionPeriodInHours": 24  // Keep job results for 24 hours
  }
}
```

---

## Configuration: ExcelConfig.json

**Location**: `jb/src/core/Excel/ExcelConfig.json`

Defines how the Internal Excel Model is built from uploaded XLSX files.

### Primary Key & Header Row Detection

```json
{
  "RecordPrimaryKey": "FamilyID",  // All records keyed by FamilyID
  "HeaderRowIndicators": [
    "fam", "famID", "family", "famille", "familleID",
    "ean", "sku", "refco", "reference", "veepee", "ref", "ngp", "lot", "pack",
    "label", "marque", "produit", "societe",
    "color", "material", "composition", "motif", "description", "designation",
    "dimension", "hauteur", "largeur", "longeur",
    "weight", "poids", "fit", "rise info", "waist", "sleeve",
    "fastening", "pocket", "compartment", "washing instructions", "style", "type of product"
  ],
  "HeaderRowSearchSpace": {
    "FirstRow": 0,
    "LastRow": 20,
    "FirstColumn": 0,
    "LastColumn": 20
  }
}
```

**How it works**:
- Searches first 20 rows and first 20 columns for header row indicator keywords (case-insensitive)
- Once header row found, treats all subsequent rows as data records
- Groups all columns under the same FamilyID into a single deduplicated FamilyRecord
- Detects conflicts when the same FamilyID appears in multiple sheets with conflicting data

---

## Image Classification Traits

From Prism.cs comment block. Every image gets classified with:

- **Borders** (enum): None (plein-pied), Bottom, Left, Top, Right
  - Indicates which edges of image touch/are cut off
  - None = full unobstructed view (plein-pied)
  - Bottom = bottom border touched, assume plan américain composition
  - Left = left border only, assume plein-pied with arm cut off

- **Human** (bool): Whether a human is present in the image

- **HeadVisible** (bool): Whether head is visible
  - If Borders contains "Top", head may already be cut off

- **Orientation** (enum): Front, Back, Left, Right, Top, Bottom, etc.
  - Viewpoint angle of the main object

- **Type** (enum): Packshot, Clothing, Detail, Ambiance, Illustration, etc.
  - Category of image content

---

## Image Matching Logic

Matches images to FamilyID records via three matchers (scores weighted 55/15/15/15):

### 1. Numeric Matcher (55% weight)
- Extracts numeric tokens from image filename
- Matches against numeric tokens in FamilyID records
- Example: filename "12345.jpg" matches record with numeric column "12345"

### 2. String Matcher (15% weight)
- Fuzzy string matching of filename tokens
- Matches against string columns in FamilyID records
- Example: filename "blue_shirt.jpg" matches record with description "blue shirt"

### 3. Computer Vision Matcher (15% weight)
- Uses ONNX model (CLIP ViT-B-32) to classify image content
- Triggers when:
  - Filename contains natural language words (not just numbers)
  - Internal Excel Model contains long strings (product descriptions)
  - Excel cells contain color names (blue, rojo, azul, groen, verde, cerveny, etc.)
- Compares ONNX embeddings against record properties

### 4. Semantic Relevance (15% weight)
- Combination of context and relevance scoring
- Weights matchers that agree on a match (CONVERGENCE_WEIGHT = 0.25)

**Scoring Thresholds**:
- Confidence ≥ 0.9 → Strong match
- Cutoff Threshold = 0.25 → Scores below 0.25 treated as no match
- Images with no match → Flagged as "KO" in manifest

---

## Image Renaming Convention

Once matched and ordered:

```
Family = FamilyID from best match
DetOrder = Zero-based ranking (0, 1, 2, ...)
NewName = "{Family}_det{DetOrder}.jpg"

Example: "ABC123_det0.jpg", "ABC123_det1.jpg"
```

The suffix determines website display order (det = detail).

---

## Data Model Hierarchy

### ImageRecord_Base (Core Properties)
```csharp
public class ImageRecord_Base
{
    public string InitialFullName { get; set; }  // Original filename
    public int Width { get; set; }
    public int Height { get; set; }
    public string Family { get; set; }           // FamilyID from match
    public int DetOrder { get; set; }            // Ranking for display
    
    // Computed property
    public string NewName => $"{Family}_det{DetOrder}.jpg";
}
```

### ImageRecord_INPUT (After Classification)
- Extends ImageRecord_Base
- Adds classification traits (Borders, Human, HeadVisible, Orientation, Type)

### ImageRecord_LAMBDA (After Matching)
- Extends ImageRecord_INPUT
- Adds matching evidence (numeric score, string score, CV score, convergence)

### ImageRecord_GENERATED (Supplementary)
- Extends ImageRecord_Base
- Generated images (e.g., upscaled or synthetic variants)

### ImageRecord_TRANSFORMED (After Transformation)
- Extends ImageRecord_Base
- Final processed image with applied centering, margins, background cleaning

---

## Data Privacy & Cleanup Rules

**Critical constraint**: All imported files must be deleted once output is sent to client.

### File Deletion Rules
- Delete all imported files (images, ZIP, XLSX) belonging to a batch after export
- PRISM does not attempt to delete files from original location (often impossible)
- After export → remove all traces of imported files from temp storage

### Temporary Storage Strategy
- **Small batches** (fit in available RAM): Keep in-memory cache
- **Large batches** (no room): Use on-disk `/tmp` folder per industry best practices
- **Decision point**: Check available memory at intake; decide cache vs disk
- **Cleanup**: Remove all imported file traces after successful export

---

## Import/Export Rules (From Importer.cs)

### Supported File Types

**Excel**:
- `.xlsx` only
- Must contain FamilyID column and recognized header rows
- Case-insensitive header matching

**ZIP Archives**:
- Maximum nest depth: 5 levels
- Amount: 0–50 ZIPs per request
- Size: 1 KB – 2 GB per ZIP
- Excel files inside ZIP → added to Excel collection
- Valid images inside ZIP → converted to flat JPEG before pipeline entry

**Images**:
- Permitted formats: JPG/JPEG, PNG, TIF/TIFF, PDF, WebP, BMP, GIF
- Filenames: case-insensitive
- Multipage TIFF/PDF: rendered as one image per page
  - If multipage has problems → try to render & export first page as flat JPEG
  - If first-page rendering fails → flag as "KO", drop file
- Alpha channels: kept as long as possible
- Corrupt images: flagged "KO", dropped
- EXIF orientation: handled gracefully; missing EXIF → render as-is

### External Resources

**Allowed before pipeline entry**:
- Dropbox, WeTransfer, cloud platform links, HTTP links
- Non-Excel resources → converted to flat JPEG byte arrays or memory streams
- ZIP resources → unzipped; valid images inside → converted to flat JPEG before pipeline entry
- Excel in ZIP → added to Excel collection

**Not allowed inside pipeline** (except approved upscaling API):
- Once data enters the pipeline, no external resources permitted
- Upscaling API is the sole exception

### File Validation & Failure Handling

**PRISM files (fail loud & hard)**:
- Invalid config, missing model files, missing PRISM resources → **Fail immediately**
- Pause pipeline until "order is restored"
- No graceful degradation

**User-supplied files (careful handling)**:
- Empty, unsupported, damaged, non-well-formed files → flagged "KO" in manifest, dropped
- All dropped files → verbose reason in manifest
- No-match images → flagged "KO" with reason "no-match"
- Invalid file → reason documented in manifest.json output

---

## API Contract Definitions (From Program.cs)

### GET /PRISM/health

**Response** (PrismHealthResponse):
```csharp
{
    "Message": "Prism API host is running.",
    "CanAcceptJobs": false,
    "ProcessingWired": false,
    "ActiveJobCount": 0,
    "QueuedJobCount": 0,
    "MaxQueuedJobs": 0,
    "MaxConcurrentJobs": 0,
    "SupportedRuntimeProviders": [],
    "ConfigReady": false,
    "RequiredModelAssetsReady": false,
    "TempStorageReady": false,
    "Notes": "Core processing and runtime configuration are not wired into this API project yet."
}
```

**Purpose**: Describes current readiness without claiming PRISM processing is available.

**Key fields**:
- `CanAcceptJobs`: True only when processing is wired, config ready, models ready, temp storage ready
- `ProcessingWired`: True when API is connected to Prism.Process
- `ConfigReady`: True when Prism_Config.json loaded and validated
- `RequiredModelAssetsReady`: True when ONNX models present and validated
- `TempStorageReady`: True when temp storage validated

### GET /PRISM/config

**Response** (PrismConfigReadinessResponse):
```csharp
{
    "ConfigReady": false,
    "SafeConfigurationAvailable": false,
    "Notes": "Runtime configuration is owned by Prism.cs and is not wired into this API project yet."
}
```

**Purpose**: Describes whether a safe public PRISM configuration payload is available.

**Key fields**:
- `ConfigReady`: True when runtime config loaded and validated
- `SafeConfigurationAvailable`: True when sanitized config response available to callers

### POST /PRISM/process

**Request** (PrismJobRequest):
- Job ID (unique identifier)
- Input sources (files, URLs)
- Processing parameters (rename, transform, generation, ReturnOriginalImages, format)

**Response** (PrismJobResult):
- Job status
- Result type (ZIP or JSON)
- Result payload (binary or encoded data)
- Progress events (list of stage transitions)

**Status Code**:
- `200 OK`: Job accepted and queued
- `400 Bad Request`: Invalid input parameters
- `501 Not Implemented`: Processing not wired yet

---

## FamilyRecord: Internal Excel Model Structure

**Purpose**: Represents one deduplicated product family record built from Internal Excel Model.

**Key Properties**:

```csharp
public sealed class FamilyRecord
{
    public string FamilyID { get; }  // Primary key, validated and trimmed
    
    public IReadOnlyDictionary<string, string> CanonicalProperties { get; }
        // Canonical values derived from accepted Excel columns
        // Case-insensitive key lookup
    
    public IReadOnlyDictionary<string, ExcelColumnClassification> ColumnClassifications { get; }
        // Column type classifications (numeric, string, color, etc.)
    
    public IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizedTokens { get; }
        // Tokenized and normalized values for matching
        // Distinct, case-insensitive, sorted
    
    public IReadOnlyDictionary<string, IReadOnlyList<string>> OriginalSourceCellValues { get; }
        // Original cell values as they appeared in Excel
    
    public IReadOnlyList<FamilyConflictEvidence> ConflictEvidence { get; }
        // Conflicting values preserved for manifest/workbench review
}
```

**Conflict Resolution**:
- When same FamilyID appears in multiple sheets with conflicting data
- Conflict is recorded in `ConflictEvidence`
- Manifest.json includes conflict reasons for user review
- Conflicts are non-blocking; job continues with documented evidence

---

## Authorship & File Organization

### User-Created Architectural Files
These contain business logic, domain knowledge, and design decisions:
- `jb/docs/PRISM-*.md` — Authoritative documentation
- `jb/src/core/Prism.cs` — Pipeline facade with architectural comment block
- `jb/src/core/Prism_Config.json` — Configuration with business constraints
- `jb/src/core/Excel/ExcelConfig.json` — Excel parsing configuration
- `jb/src/core/Excel/FamilyRecord.cs` — Internal model structure
- `jb/src/core/Models/ImageRecord_*.cs` — Data model hierarchy
- `jb/src/core/IO/Importer.cs` — Input validation and file handling rules
- `jb/src/api/Program.cs` — API contract definitions
- `AGENTS.md`, `AGENT-TICKETS.md`, `AGENTFEEDBACK.md` — Project governance

### AI-Created Files
These are scaffolding and stubs awaiting implementation:
- `jb/src/core/Pipeline.cs` — Empty (awaiting T-300 implementation)
- Project structure and csproj files
- Template/placeholder implementations

---

## Design Principles Summary

1. **Configuration-Driven**: All parameters loaded from JSON files, never hardcoded
2. **Fail-Fast**: PRISM files missing/invalid → pause and alert; user files → graceful KO + manifest
3. **Explicit Resource Management**: `Initialize()` before `Run()`, `IDisposable` for all unmanaged resources
4. **Story-Readable Code**: Prism.cs and main flows should read like a recipe, understandable by non-experts
5. **Immutable Pipeline Order**: Fixed stage sequence never changes
6. **Privacy-First**: All imported files deleted after export
7. **Careful File Handling**: User-supplied files treated carefully; any problem → verbose reason in manifest
8. **Scoring Transparency**: Matching weights documented; thresholds configurable

---

## Last Updated
**From verified source files** during M0/M1 verification (2026-06-10).
