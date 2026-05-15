# Images Todo

- [ ] Define source image state after import: list the fields an image has after IO normalization and before matching starts.
  - Impact:
    - Project progress: High - Source image state is the input contract for tokenization, matching, classification, and transform.
    - Effect on other TODOs: Blocks - It gates filename token metadata, duplicate visual hash handling, matcher inputs, and manifest provenance.
  - Industry standard:
    Image pipelines use a normalized per-image record after ingestion with original identity, normalized artifact reference, dimensions, hashes, and status.
  - Recommended solution:
    Define source state with original filename, normalized JPG reference, media type, dimensions, source kind, hash, import status, and diagnostics.
  - Answer:

- [ ] Define filename token metadata: say how tokens remember their source filename, position, type, and original text.
  - Impact:
    - Project progress: High - Token metadata is the foundation of explainable filename matching and ordering.
    - Effect on other TODOs: Blocks - It feeds numeric matching, string matching, ordering hints, and matcher evidence retention.
  - Industry standard:
    Data matching systems keep token provenance, offsets, normalized value, and token type so scores can be audited and tuned.
  - Recommended solution:
    Store token ID, source filename, start/end position, original text, normalized text, token type, and parser confidence.
  - Answer:

- [ ] Define output filename stem rules: say which product identifier becomes the filename stem.
  - Impact:
    - Project progress: High - Filename stems determine the primary business output of PRISM.
    - Effect on other TODOs: Blocks - It depends on ProductRecord identity and gates suffix assignment, collision handling, zip export, and manifest rows.
  - Industry standard:
    Media renaming pipelines derive output names from a canonical entity identifier, not from ambiguous source filenames or display labels.
  - Recommended solution:
    Use the matched ProductRecord FamilyID as the filename stem.
  - Answer:

- [ ] Define output filename suffix rules: say how `_det` numbers are assigned after image ordering.
  - Impact:
    - Project progress: High - Suffix rules define image sequence semantics for downstream websites.
    - Effect on other TODOs: Blocks - It relies on ordering rules and affects collision handling, zip output, and manifest projection.
  - Industry standard:
    Product image pipelines assign deterministic sequence suffixes after grouping and ordering, with no dependence on nondeterministic file enumeration.
  - Recommended solution:
    Assign `_det0`, `_det1`, and so on per FamilyID after final ordering, with zero-based contiguous numbering.
  - Answer:

- [ ] Define output filename collision handling: say what happens when two images want the same final filename.
  - Impact:
    - Project progress: High - Collision handling prevents overwrites and inconsistent exports.
    - Effect on other TODOs: Blocks - It affects zip duplicate filename handling, suffix assignment, JSON names, and manifest rows.
  - Industry standard:
    Exporters reserve final artifact names before writing and resolve conflicts deterministically with manifest evidence.
  - Recommended solution:
    Resolve collisions during order/rename by assigning deterministic suffixes, then reject or uniquely disambiguate any remaining collision before export.
  - Answer:

- [ ] Define unmatched image naming: say whether unmatched images keep original names, get KO names, or are excluded.
  - Impact:
    - Project progress: High - Unmatched naming defines how no-match images appear in outputs and manifests.
    - Effect on other TODOs: Unblocks - It aligns matcher threshold enforcement, KO policy, zip layout, and JSON status fields.
  - Industry standard:
    Items that fail entity matching are reported as failed records and kept separate from successful output artifacts to avoid false catalog links.
  - Recommended solution:
    Treat unmatched images as KO records, keep original filename in manifest, and exclude them from OK output images unless a configured KO folder is exported.
  - Answer:

- [ ] Define duplicate visual hash handling: say how visually duplicate images are detected and reported.
  - Impact:
    - Project progress: Medium - Duplicate detection improves quality and prevents redundant outputs but follows source image and output naming contracts.
    - Effect on other TODOs: Influences - It affects manifest diagnostics, ordering, KO grouping, and output collision handling.
  - Industry standard:
    Large image aggregators compute perceptual hashes or embeddings to detect near duplicates, then report duplicate groups with a chosen primary artifact.
  - Recommended solution:
    Compute a normalized visual hash after import, group near duplicates per FamilyID, keep the best candidate, and report duplicates in manifest diagnostics.
  - Answer:

- [ ] Define forbidden filesystem character handling: say how invalid filename characters are removed or replaced.
  - Impact:
    - Project progress: Medium - Sanitization prevents invalid archive entries and cross-platform file issues.
    - Effect on other TODOs: Influences - It affects output filename rules, zip export, JSON names, and collision handling.
  - Industry standard:
    Export systems sanitize filenames using a deterministic allowlist and keep the original name separately for provenance.
  - Recommended solution:
    Normalize final filenames to a conservative ASCII-safe allowlist, replace invalid characters with `_`, and record original filenames separately.
  - Answer:

- [ ] Define output extension rules: say whether every output image uses `.jpg` or preserves another normalized extension.
  - Impact:
    - Project progress: Medium - Extension policy affects exporters and client expectations after flat JPG normalization.
    - Effect on other TODOs: Influences - It aligns flat JPG conversion, MIME metadata, OutputImageRecord fields, and zip/JSON parity.
  - Industry standard:
    Pipelines that normalize to one internal image format export with matching content type and extension unless an explicit derivative format is configured.
  - Recommended solution:
    Export processed images as `.jpg` with `image/jpeg` by default.
  - Answer:
