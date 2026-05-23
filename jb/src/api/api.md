The API is an interface between any frontend or platform wishing to use Prism.

Prism core is found inside the [../core](../core/) folder.

Mandatory, no exceptions: routes start with `/PRISM/`.

## Endpoints

### Process

Starts a Prism processing job:

```http
POST /PRISM/process
```

The request body is `multipart/form-data`.

Multipart parts:

- `request`: JSON request model.
- `input`: repeated uploaded file part for every local file, regardless of whether the file is an image, `.xlsx` Excel file, or zip file.

The `request` JSON carries processing options and remote inputs:

```json
{
  "ClientRequestToken": "abc-123",
  "rename": true,
  "transform": true,
  "generation": true,
  "format": "zip",
  "ReturnOriginalImages": false,
  "Input": [
    "img1.jpg",
    "products.zip",
    "sheet.xlsx",
    "https://example.com/archive.zip",
    "https://dropbox.com/...",
    "https://wetransfer.com/..."
  ]
}
```

Processing option meanings:

- `rename=true` means images are renamed.
- `transform=true` means images are repositioned, cropped, stretched, or otherwise transformed.
- `generation=true` means Prism attempts to generate extra images when only x images are tied to a FamilyID.
- `format` is `zip` or `json`.
- `ReturnOriginalImages=true` includes original images only in allowed result payload fields and never inside `manifest`.

Remote resources go in `request.Input`. Uploaded local files are sent as repeated multipart `input` parts. Clients do not classify inputs as image, Excel, or zip; Prism/API ingress and `Importer.cs` triage by accepted media type.

Every request must contain at least 1 image representation and 1 Excel file.

Supported direct file uploads are Excel files, zip files, and accepted media files.

Accepted image and document media are jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif.

External resources are allowed before entering the Prism pipeline.

External resources such as Dropbox, WeTransfer, cloud platform links, and direct HTTP links are accepted as input media only.

External image-like resources must be converted to flat jpg data as a raw byte array or memory-backed stream before entering the image processing pipeline.

External zip resources must be unzipped before entering the image processing pipeline.

Each valid image found inside a zip resource must be converted to flat jpg data before entering the image processing pipeline.

Each Excel file found inside a zip resource must be added to the Excel collection and processed later as part of the internal Excel model.

Once data enters the Prism pipeline, no external resources are permitted except the approved external upscaling API.

Further limitations are configured in `jb/src/core/Prism_Config.json`.

The total request size cannot exceed the runtime configuration loaded by `Prism.cs` from `Prism_Config.json`.

`POST /PRISM/process` returns a job-start envelope quickly instead of returning the final zip or JSON result directly.

Job-start response fields:

- `JobID`
- `ClientRequestToken` when supplied
- `progressUrl`
- `resultUrl`
- Initial status

### Progress

Streams job progress to web clients.

```http
GET /PRISM/jobs/{JobID}/progress
```

The progress stream uses Server-Sent Events. Events project shared `PipelineProgressEvent` fields such as job ID, stage name, current item, completed count, total count, severity, safe message, timestamp, and optional diagnostic snapshot reference.

### Result

Returns the final completed result for a job.

```http
GET /PRISM/jobs/{JobID}/result
```

For `format="zip"`, the result endpoint returns a raw `application/zip` stream. The zip contains `manifest.json` at archive root, root-level `OK/`, root-level `KO/`, and the full first `.xlsx` file whose original workbook contained the first accepted `familyID` column or accepted alternative-familyID column. Web clients use the SSE completion `resultUrl` to trigger zip auto-download behavior.

For `format="json"`, the result endpoint returns `application/json` with top-level `manifest`, `images`, and optional `originalImages` when `ReturnOriginalImages=true`. The `manifest` field is the canonical summary and describes all OK and KO images, KO groups, route summaries, safe diagnostics, and export metadata. The `images` field contains frontend journey entries grouped as `images.ok[]` and `images.ko[]`; each entry contains `sourceReference`, bounded `lambda` route data, and `output` metadata or `null` when no exportable artifact exists.

### Health

Returns up-to-date health information on Prism.

```http
GET /PRISM/health
```

### Config

Returns the current runtime configuration object that `Prism.cs` built from `Prism_Config.json` at startup.

```http
GET /PRISM/config
```
