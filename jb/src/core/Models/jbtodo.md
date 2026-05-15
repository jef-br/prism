# Core Models Todo

- [ ] Define fields for `ProductRecord.cs`: list the product identifier, canonical properties, column classes, and source Excel values it stores.
  - Impact:
    - Project progress: High - ProductRecord is the canonical catalog entity used by matching, naming, and manifest output.
    - Effect on other TODOs: Blocks - It gates Excel mapping, matcher comparisons, filename stems, and generated image records.
  - Industry standard:
    Data aggregators separate canonical entity fields from source values and classification metadata so downstream matching can be deterministic and explainable.
  - Recommended solution:
    Include FamilyID, canonical properties, column classifications, normalized tokens, source cell values, and conflict evidence.
  - Answer:

- [ ] Define fields for `SourceImageRecord.cs`: list original name, normalized bytes or stream, media type, source kind, and import status.
  - Impact:
    - Project progress: High - SourceImageRecord is the handoff between IO normalization and image matching.
    - Effect on other TODOs: Blocks - It feeds source image state, filename tokens, matcher evidence, transform input, and manifest provenance.
  - Industry standard:
    Image pipelines carry original identity, normalized artifact reference, media metadata, source type, and ingestion status as a durable per-item record.
  - Recommended solution:
    Store original filename, normalized JPG artifact reference, media type, source kind, import status, dimensions, hash, and import diagnostics.
  - Answer:

- [ ] Define fields for `ProcessedImageRecord.cs`: list links to source image, matched product, matcher result, transform result, output name, and KO state.
  - Impact:
    - Project progress: High - ProcessedImageRecord is the central per-image lifecycle record after matching starts.
    - Effect on other TODOs: Blocks - It connects matching, classification, transform, order/rename, export, and manifest projection.
  - Industry standard:
    Per-item pipeline records link each stage result instead of overwriting state, making retries, audits, and UI diagnostics possible.
  - Recommended solution:
    Include source image ID, matched ProductRecord ID, matcher result, classification traits, transform result, output filename, output record, status, and KO reason.
  - Answer:

- [ ] Define fields for `BatchManifest.cs`: list batch summary counts, image rows, KO groups, config snapshot, and output format metadata.
  - Impact:
    - Project progress: High - BatchManifest is the audit and export contract for every completed job.
    - Effect on other TODOs: Blocks - It gates zip/JSON parity, KO reason projection, workbench review, and output response models.
  - Industry standard:
    Batch systems emit a manifest with summary counts, per-record outcomes, failure groups, effective config, timings, and artifact metadata.
  - Recommended solution:
    Include batch ID, summary counts, per-image rows, KO groups, config snapshot, stage timings, output format metadata, and safe diagnostics.
  - Answer:

- [ ] Define fields for `KoReason.cs`: list reason code, human-readable message, source stage, source file, and whether the batch continues.
  - Impact:
    - Project progress: High - KO reasons define failure semantics across user files and pipeline stages.
    - Effect on other TODOs: Blocks - It unifies IO errors, zip errors, transform failures, API errors, and manifest KO groups.
  - Industry standard:
    Record-level failures use stable machine codes plus safe human messages, stage provenance, retryability, and continuation policy.
  - Recommended solution:
    Include code, message, stage, source file/member, item ID, retryable flag, batch-continues flag, and safe details.
  - Answer:

- [ ] Define fields for `PipelineProgressEvent.cs`: list stage name, current item, item counts, message, severity, and optional snapshot reference.
  - Impact:
    - Project progress: High - ProgressEvent is the common observability contract for API and workbench clients.
    - Effect on other TODOs: Blocks - It feeds API progress streaming, WPF subscription, web progress display, and diagnostic snapshots.
  - Industry standard:
    Long-running processing systems emit structured progress events with correlation ID, stage, counts, severity, message, timestamp, and artifact references.
  - Recommended solution:
    Include job ID, stage, current item ID/name, completed count, total count, message, severity, timestamp, and optional snapshot reference.
  - Answer:

- [ ] Define `InternalExcelModel` to `ProductRecord` mapping: say how dynamic Excel columns become canonical product properties.
  - Impact:
    - Project progress: High - Mapping converts flexible supplier spreadsheets into stable matching input.
    - Effect on other TODOs: Blocks - It affects ProductRecord fields, string matching, column classification, and manifest diagnostics.
  - Industry standard:
    Schema-flexible aggregators map raw source columns into canonical fields and retain unmapped or conflicting source values for review.
  - Recommended solution:
    Map FamilyID to the product identifier, classify each column, normalize tokens for matching, and preserve original source values as evidence.
  - Answer:

- [ ] Define fields for `MatcherResult.cs`: list final candidate FamilyID, score, threshold status, tie state, and selected evidence.
  - Impact:
    - Project progress: High - MatcherResult defines the decision that links an image to a product.
    - Effect on other TODOs: Blocks - It gates matcher score aggregation, tie-breaking, ordering, output naming, and KO unmatched policy.
  - Industry standard:
    Matching engines store winning candidate, score, thresholds, tie state, and supporting evidence so automated links can be audited.
  - Recommended solution:
    Include candidate FamilyID, score, threshold status, tie status, runner scores, selected evidence, and decision explanation.
  - Answer:

- [ ] Define fields for `MatcherEvidence.cs`: list matcher name, source token or label, compared Excel value, score, and explanation text.
  - Impact:
    - Project progress: High - Evidence fields make matching explainable and debuggable.
    - Effect on other TODOs: Unblocks - It supports matcher evidence retention, workbench diagnostics, and manifest rows.
  - Industry standard:
    Explainable matching systems store granular evidence per rule/model rather than only a final score.
  - Recommended solution:
    Include matcher name, evidence type, source token/label, compared product field/value, score, weight, and explanation.
  - Answer:

- [ ] Define `ProcessedImageRecord` matcher evidence reference: say whether evidence is embedded, linked by id, or stored as a list.
  - Impact:
    - Project progress: High - Evidence reference policy affects memory, manifest size, and diagnostic access.
    - Effect on other TODOs: Influences - It shapes MatcherResult, BatchManifest, and workbench snapshot display.
  - Industry standard:
    Per-record evidence is commonly embedded for bounded batches, while large or verbose evidence can be linked to diagnostic artifacts.
  - Recommended solution:
    Store selected evidence as a bounded list on MatcherResult and link verbose debug artifacts through optional snapshot references.
  - Answer:

- [ ] Define fields for `ImageClassificationTraits.cs`: list human, head, border, orientation, image type, confidence, and unknown values.
  - Impact:
    - Project progress: High - Classification traits drive transform choices and ordering rules.
    - Effect on other TODOs: Blocks - It feeds classification values, transform tag output, ordering hints, and diagnostics.
  - Industry standard:
    Vision pipelines represent traits as typed values with confidence and unknown/unavailable states rather than bare booleans.
  - Recommended solution:
    Include human, head visibility, border intersections, orientation, image type, confidence values, and explicit unknown reasons.
  - Answer:

- [ ] Define fields for `TransformResult.cs`: list crop box, resize data, output size, background fill method, warnings, and failure reason.
  - Impact:
    - Project progress: High - TransformResult records how each output image was produced or why it failed.
    - Effect on other TODOs: Blocks - It supports transform failure policy, diagnostics, manifest projection, and output records.
  - Industry standard:
    Image transform pipelines store geometric decisions, output dimensions, fill methods, warnings, and errors for every transformed item.
  - Recommended solution:
    Include crop box, resize operation, output size, background fill method, warnings, status, failure reason, and snapshot references.
  - Answer:

- [ ] Define `ProcessedImageRecord` transform result reference: say whether transform results are embedded or shared with manifest rows.
  - Impact:
    - Project progress: Medium - Reference policy determines how transform details flow to export and UI.
    - Effect on other TODOs: Influences - It affects TransformResult fields, manifest projection, and workbench display.
  - Industry standard:
    Per-item transform summaries are embedded in processing records while heavy artifacts are linked by stable references.
  - Recommended solution:
    Embed the TransformResult summary on ProcessedImageRecord and project selected fields into manifest rows.
  - Answer:

- [ ] Define fields for `OutputImageRecord.cs`: list final filename, extension, MIME type, byte source, dimensions, and export status.
  - Impact:
    - Project progress: High - OutputImageRecord defines what exporters and clients receive.
    - Effect on other TODOs: Blocks - It gates JSON MIME metadata, zip entries, output extension rules, and manifest output fields.
  - Industry standard:
    Output artifact records include logical name, content type, dimensions, byte reference, checksum/length, and export status.
  - Recommended solution:
    Include final filename, extension, MIME type, artifact reference, dimensions, byte length, checksum, and export status.
  - Answer:

- [ ] Define manifest row projection: say which processed image fields are copied into the exported manifest.
  - Impact:
    - Project progress: High - Projection defines the stable public view of internal processing records.
    - Effect on other TODOs: Blocks - It aligns BatchManifest, JSON output, zip parity, workbench review, and privacy policy.
  - Industry standard:
    Manifests expose stable, safe fields needed for audit and client processing while hiding volatile internal implementation details.
  - Recommended solution:
    Project original filename, final filename, status, KO reason, matched FamilyID, scores, output metadata, transform summary, and safe diagnostics.
  - Answer:

- [ ] Define fields for `GeneratedImageRecord.cs`: list source FamilyID, source image references, generation method, output image, and quality decision.
  - Impact:
    - Project progress: Medium - Generated images are future pipeline extensions and should not destabilize current processing contracts.
    - Effect on other TODOs: Influences - It affects generation policy, output records, manifest rows, and config limits.
  - Industry standard:
    Generated media records preserve source lineage, generation method, parameters, quality gates, and final artifact links for audit.
  - Recommended solution:
    Include FamilyID, source image IDs, generation method, parameters/config snapshot, output image record, quality decision, and KO reason if rejected.
  - Answer:
