# IO Todo

- [x] Define path input handling: say whether core accepts local file paths directly and who checks.
  - Answer:
    Prism accepts local path descriptors before the pipeline starts. `Importer.cs` performs the first checks for existence, size, and extension before opening the file. Only inputs that pass those checks enter normalization. Importer turns the accepted input paths, whether folder or file, local or resolved remote, into two normalized collections: one image collection and one Excel collection. Paths that fail validation are skipped and logged to `manifest.json`. Import strategy classes handle content type and origin-specific parsing, including separate strategies for remote paths and platform links such as WeTransfer or Dropbox.

- [x] Define stream input handling: say how memory-backed streams enter Importer and who owns stream disposal.
  - Answer:
    Memory-backed streams enter Importer as input descriptors with source metadata, stream reference, and explicit ownership. Importer reads those descriptors into the same normalized image and Excel collections used for path inputs. If the descriptor says Importer owns the stream, Importer disposes it after normalization or KO handling; otherwise the caller remains responsible for disposal.

- [x] Define multipart file input handling: say how API upload parts become Importer inputs before the pipeline starts.
  - Answer:
    API upload parts are converted before pipeline entry into importer input descriptors containing original filename, content type, byte length, source kind, and either a stream reference or a job-temp-file reference. The API performs edge validation first, then passes the descriptors to Importer so multipart uploads follow the same normalization path as local files and resolved remote inputs.

- [x] Define logical job folder handling: say whether each batch gets a temporary folder and what gets stored there.
  - Answer:
    Each logical job gets a temporary folder that is cleaned up once output has been sent back to the requesting client or frontend. The folder is used as spill-to-disk storage for temporary inputs, downloaded files, extracted zip members, normalized JPGs, diagnostic snapshots, and output assembly as needed.

- [x] Define directory input handling: local folders may be scanned recursively, but recursion stops for any folder whose total byte size is below `Input.Images.filesize.min`; every discovered file is still validated individually against configured file size, extension, request size, and batch image count limits.
  - Answer:
    Local folders may be scanned recursively, but recursion stops for any folder whose total byte size is below `Input.Images.filesize.min`. Every discovered file is still validated individually against configured file size, extension, request size, and batch image count limits.

- [x] Define link input handling: remote URLs are fetched before pipeline entry, converted into temporary input descriptors, and then handled like local files by `Importer.cs`; use a generic direct-URL import strategy by default.
  - Answer:
    Remote URLs are fetched before pipeline entry, converted into temporary input descriptors, and then handled like local files by `Importer.cs`. Use a generic direct-URL import strategy by default.

- [x] Define remote import strategies: implement generic direct-URL, Dropbox, and WeTransfer import strategies from the start; add other platform-specific strategies only when their links require custom resolution.
  - Answer:
    Implement generic direct-URL, Dropbox, and WeTransfer import strategies from the start. Add other platform-specific strategies only when their links require custom resolution.

- [x] Define flat jpg conversion ownership: say which IO class converts external images, PDFs, and TIFF pages into jpg bytes or streams.
  - Answer:
    `Importer.cs` owns conversion of supported external image formats, PDFs, and TIFF pages into flat JPG artifacts. Media-specific import strategies perform the format-specific work, and Importer stores the normalized JPGs in the job temporary folder before adding them to the image collection.

- [x] Define alpha handling after import: say what happens to transparency when images are flattened to jpg.
  - Answer:
    Transparent pixels are converted to `#ffffff` when images are flattened to JPG.

- [x] Define EXIF orientation application after flat jpg conversion: say when orientation is applied.
  - Answer:
    EXIF orientation is applied during import normalization so the normalized image is oriented correct-side-up before downstream matching, classification, and transformation. If no EXIF orientation information is found, the image orientation is kept in its original state.

- [ ] Define EXIF orientation metadata recording for normalized jpg output: say whether the normalized jpg records that orientation was applied, missing, invalid, or unchanged.
  - Impact:
    - Project progress: Medium - Orientation metadata makes import diagnostics and downstream image analysis explainable.
    - Effect on other TODOs: Influences - It affects source image state, manifest projection, workbench diagnostics, and transform troubleshooting.
  - Industry standard:
    Image ingestion pipelines record whether orientation metadata was present, applied, missing, invalid, or ignored so normalized artifacts can be audited without keeping the original bytes.
  - Recommended solution:
    Add a source image diagnostic field such as `orientationStatus` with values for `applied`, `missing`, `invalid`, and `unchanged`, plus the original EXIF orientation value when safe to expose.
  - Answer:

- [x] Define corrupt image KO reasons: list the reason codes used when an image cannot be opened, decoded, or converted.
  - Answer:
    KO reasons for images are `500` for damaged files that could not be opened or fully decoded, `500` for corrupt files where part of the image is missing, and `541` for conversion failures. Relevant details are added as a safe description for the client, while information that could be abused is not disclosed. The message appears in the console log and as an entry in `manifest.json`.

- [ ] Define JSON export property names: list the exact names for original filename, new filename, image bytes, status, and reason fields.
  - Impact:
    - Project progress: High - JSON field names are a public export contract and must match manifest projection.
    - Effect on other TODOs: Blocks - It gates JSON response model, output image records, manifest parity, and client parsing.
  - Industry standard:
    JSON exports from batch processors use stable, lower-camel-case field names and avoid mixing binary payloads with status fields ambiguously.
  - Recommended solution:
    Use `originalFilename`, `newFilename`, `imageBytesBase64`, `status`, and `reason` as the canonical JSON properties.
  - Answer:

- [ ] Define JSON export MIME metadata: say how output content type and file extension are represented in JSON.
  - Impact:
    - Project progress: High - MIME metadata lets clients reconstruct files correctly from JSON output.
    - Effect on other TODOs: Unblocks - It aligns output records, JSON response model, output extension rules, and zip parity.
  - Industry standard:
    Binary-in-JSON exports include explicit content type, extension, encoding, byte length, and filename metadata so consumers do not infer file type from payloads.
  - Recommended solution:
    Add `contentType`, `extension`, `encoding`, and `byteLength` fields beside each JSON image payload.
  - Answer:

- [ ] Define original image export policy: say whether original input bytes are ever included in output or manifest data.
  - Impact:
    - Project progress: High - Original byte policy affects privacy, output size, and manifest contract.
    - Effect on other TODOs: Blocks - It influences JSON export, manifest projection, diagnostics, and cleanup rules.
  - Industry standard:
    Image processing outputs usually retain provenance metadata but avoid redistributing original bytes unless explicitly required, especially when inputs may contain sensitive or licensed content.
  - Recommended solution:
    Do not include original input bytes by default. Include original images in the returned result only when `PrismProcessingParameters.ReturnOriginalImages` is true, and still keep manifest rows limited to original filename, source kind, safe metadata, and output references.
  - Answer:
