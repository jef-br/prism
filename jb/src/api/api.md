The API is an interface between any frontend or platform wishing to use Prism.

Prism core is found inside the [../core](../core/) folder.

Mandatory, no exceptions: routes start with `/PRISM/`.

## Endpoints

### Process

The actual processing pipeline route:

```http
POST /PRISM/process?rename=true&transform=true&format=zip
```

Parameters:

- `rename` = boolean.
- `transform` = boolean.
- `generation` = optional boolean.
- `Translate` = optional .NET culture string such as `nl-BE`, `fr-FR`, `en-GB`, `es-ES`, `de-DE`, or `it-IT`.
- `format` = `zip` or `json`.

`rename=true` means images are renamed.

`transform=true` means images are repositioned, cropped, stretched, or otherwise transformed.

`generation=true` means Prism attempts to generate extra images when only x images are tied to a FamilyID.

`Translate` only works when `rename=true`.

`format=zip` returns a zip file containing renamed and transformed images plus `manifest.json`.

`format=json` returns a JSON payload containing the same operation result and image data.

The request body is multipart/form-data.

Every request must contain at least 1 image representation and 1 Excel file.

Supported direct file uploads are Excel files, zip files, and accepted media files.

Accepted image and document media are jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif.

External resources are allowed before entering the Prism pipeline.

External resources such as Dropbox, WeTransfer, cloud platform links, and direct HTTP links are accepted as input media only.

External resource fields use `url=<resource-url>`.

External image-like resources must be converted to flat jpg data as a raw byte array or memory-backed stream before entering the image processing pipeline.

External zip resources must be unzipped before entering the image processing pipeline.

Each valid image found inside a zip resource must be converted to flat jpg data before entering the image processing pipeline.

Each Excel file found inside a zip resource must be added to the Excel collection and processed later as part of the internal Excel model.

Once data enters the Prism pipeline, no external resources are permitted except the approved external upscaling API.

Further limitations are configured in `jb/src/core/Prism_Config.json`.

The total request size cannot exceed the runtime configuration loaded by `Prism.cs` from `Prism_Config.json`.

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
