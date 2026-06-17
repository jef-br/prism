# Source Information

Prism is an image processing pipeline that renames and transforms product images by combining incoming media with data found in Excel files.

Prism should be technically able to serve 250 concurrent users.

A heavy daily average per user can be about 10k images and 2 Excel files.

In this context, 10k daily images means about 4 batches of 2500 images each.

Prism should be built to handle up to 5000 images per batch with ease.

In normal operation, Prism is capped at 2500 images per batch so the service is not constantly put under heavy stress.

Prism will run on local servers.

Server hardware specs are subject to change and will not always include a GPU.

Accepted image and document media are jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif.

PSD is not accepted unless it is added later as an explicit supported media type.

External resources are allowed before entering the pipeline.

External resources such as Dropbox, cloud platform links, and direct HTTP links are allowed as input media only.

External image-like resources must be converted before the pipeline receives them.

Non-Excel resources must be converted to flat jpg data as a raw byte array or memory-backed stream and added to the image collection for the batch.

Zip resources must be unzipped before entering the pipeline.

Each valid image found inside a zip resource must be converted to flat jpg data before entering the image processing pipeline.

Each Excel file found inside a zip resource must be added to the Excel collection and processed later as part of the internal Excel model.

Once data is inside the pipeline, external resources are not permitted.
The only permitted pipeline exception is the external upscaling API at `www.letsenhance.ai`.

Missing Prism-owned configuration files or model files should fail fast and loud.


# prism
Rename images using excel files and transforming them using labeling/classification

* **Target user:** junior non-technical administrative support staff
* **Expected input per batch:**
  * One or more images and one or more excel files.
  * A heavy daily average per user can be about 10k images and 2 excel files.
  * In practice, 10k daily images means about 4 batches of 2500 images each.
  * PRISM should be built to handle up to 5000 images per batch with ease.
  * The normal configured cap is 2500 images per batch so PRISM is not constantly put under heavy stress.
  * The excel files might be in different languages
  * Accepted image and document media are jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif.
  * PSD is not accepted unless it is added later as an explicit supported media type.
  * No single file can be larger than 25 megabyte by default.
  * File and request limits are configured in `jb/src/core/Prism_Config.json`.
  * excel files can be...
    * written in any human language
    * an internal document from one of  many proprietary in-house data platforms
    * any format: PRISM should be agnostic about structure as excel files can come from any of +2000 suppliers. (A-list fortune500 Brands as well as mom&pop brands. FMCG, retail, fashion, ... anything)
    * single/multiple worksheets
    * multilingual
    * unstructured. Column header data might not be near the top of a worksheet and needs to be found per document.
    * unsorted
    * contain duplicates



**Desired output:** A collection of images + manifest.json file
  * Every image renamed by comparing the initial image filename with data from the accompanying excel file(s) looking for cells in a column called "familyID" (or similar)
  * Transformed according to an image typology that is well-defined in core/images/classify
    * should type an image based on amongst others:
      * "hasHuman?" (can a human be detected either partially or fully in view --> Inferred by looking for skintone inside the bounding box of the main object and an attempt to skeletonize the main object to see if it actually is a human)
        * Ideally a boolean, can be a double if the inference works better this way
      * "headVisible?" (if hasHuman and Intersection.TOP == false --> the head might be showing. Can we see ears, eyes, nose, or any other facial feature to confirm?)
        * Ideally a boolean, can be a double if the inference works better this
      * "Intersection (= indicate where (if at all) the image content touches a particular side of the image.
        * Intersection.TOP, Intersection.RIGHT, Intersection.BOTTOM, Intersection.LEFT
        * Values are boolean.
  * images that are product packshots are centered and have a consistent margin applied arround them
  * images that cannot be repositioned (like detail images or certain lifestyle images) should be cropped as well as possible and their background stretched in a pretty way respecting the images' Intersection values.
  * manifest.json contains a summary of the processed batch.
    * how many images?
    * how many excel files?
    * renamed: amount OK & KO
    * transformations: amount OK & KO
    * amount of images dropped (KO)
    * per image:
      * original filename
      * new filename
      * the processed image as an exported `.jpg` artifact or a JSON output reference
      * NOT the original image


## **High level flow:**

**This is the definitive, final, and only valid pipeline order:**
* imported > classified > matched > ordered > renamed > Generated > Transformed > Exported
  * Imported
    * importing media (excel,zip,images)
    * parsing to useful representation (internal excel model, unpacking zips, homogenize images into jpg representations)
  * Classify
    * deduplicate images (highest res wins, visual hash is key-comparer, filenames can be different)
    * apply image classification/labelling to every canonical image
  * match
    * tokenize the image using all matchers
    * compare all image labels and tokens against the Internal Excel Model to find a match with the FamilyID
    * Resolve until one familyID remains (top candidate above threshold)
  * Order:
    * Per FamilyID: Order all images
      * First: based on tokens indicating a shared ordering system between the images belonging to that FamilyID
        * scan original filename for suffix indicating order information:
          * keyword tokens (front,side,back, ...)
          * numerical suffix (..._1.jpg, ..._2.jpg, ...)
          * alphabetical suffix (..._A.jpg, ..._B.jpg, ...)
          * alphanumerical suffix (..._A1.jpg, ..._A2.jpg, ...B1.jpg, ...)
        * Use image classification labels to confirm / remove doubt (if label "front" is found, likelihood for lower-order image increases)
    * match and order are repeated until there are no more unmatched candidates or no more new matches are made in a pass.
  * Rename:
    * Collapse the probability of FamilyID+order into a filename and rename all files
  * For Families with a low image count, copy the hero image (front-facing product/model in full-est view) to generate an alternative version of that image either by cropping to a detail, by embedding the image on a different background using GenAI, or both.
  * Transform:
    * Per image: Transform the image using the transformations found in `C:\Users\JefB\Documents\JBGITROOT\prism\jb\src\core\Images\Transform`
      * Transformation parameters are guided using the per-image ImageNGP configuration.
  * Export:
    * Return all images together with a manifest.json file


Any frontend connects to Prism.cs
* Connecting happens through the API or direct
Prism.cs regulates the pipeline
* Code in Prism.cs reads like a story
* It is written chronologically
* It contains only "management code" that calls classes that do the real work
  * Call classes that ingest media
  * send to `Pipeline.cs`
    * `Pipeline.cs` uses the classes that perform...
      * Excel Modeling: Receives all collated excel worksheet into an internal, deduplicated model sorted by familyID
      * Generate tokens per image by using `jb\src\core\Pipeline\FilenameTokenizer.cs`
      * Image matching: `jb/src/core/Images/ImageMatcher.cs` calculates the most probable association between image and familyID using the image collection and the internal Excel model.  (More than one image can belong to a single familyID)
        * Contains scoring logic (rules and consequences written below per matcher)
          * It is imperative that the scoring logic is readable by a 10 year old and easy to update.
          * rules and their values are located near the top of the file and grouped per matcher class.
        * Loads matcher classes via strategy design pattern
          * `NumericMatcher.cs`: Contains all logic to perform numerical matching.
            * Parse any input string to a tokenized numerical-only string
            * Compare all tokens against the numerical columns of the excel model (InternalExcelModel.cs) by edit distance.
            * Before an image will be considered to be part of a familyID, an identical match is required.
              * The shortest distance between the entire input string and the familyID wins.
              * Matches with fewer tokens scores higher. For example:
                * List of FamilyIDs in the ExcelModel = "12345678", "23456789", "09876543"
                * Original filename of 2 images
                  * A:"1234_JEANS_5678_front.jpg", parsed into tokens: 12345678, 1234, 5678
                  * B: "1234_Armani_56_shirt_78_green_09876543_front.jpg", parsed into tokens: 1234, 56,78, 09876543
                  * A matches with "12345678" because token "12345678" is an identical match made up out of 1 token. It has the same length and the same characters, so the edit distance is 0.
                  * B matches with "12345678" and with "09876543".
                    * The match with "12345678" is obtained through combining tokens "1234", "56", and "78". Even though the match is identical, it still has an edit distance of 2 because it requires combining 3 tokens.
                    * The match with "09876543" is an identical match made up out of 1 token. The edit distance is 0, so this match has a higher score than the match for "12345678".
            * Numeric Scoring rules:
              * scoring starts at 100%
              * single token and identical match = -0% (ie keep 100%)
              * tokens count = deduct 5% * number of tokens - 1 (So 1 token = -0%, 2 tokens -5% etcetera)
              * edit distance = deduct edit distance divided by string length as percentage. Example: filename "ABC" and token "ABD" has a score of 67% because 'edit distance 1' divided by 'length 3' results in 1-1/3=67%.
              * length difference = if the tokenset is an identical match to the column token but the column token is longer than the filename token, subtract 1 minus the difference in length divided by total column length ("abcde" and "abcdefgh" would be 1-5/8=-0.375)
              * Only scores close to 100% (threshold set in ImageMatcher.cs) result in an image/familyID candidacy.


          * `StringMatcher.cs`: Contains all logic to perform string token and fuzzy string matching.
            * Parse the input string to logical string tokens
            * Compares all tokens belonging to the image filename against all categorical, descriptive, and mixed columns of the Internal excel model (InternalExcelModel.cs)
              * categorical columns
                * represent product type, material, or color.
                * cells contain up to 4 strings of low-cardinality
                * every string is between 3 and 12 characters, 5 or 6 being ideal string length
                * matches
              * Descriptive columns
                * contain product information, descriptions, washing instructions, materials used, marketing text, etcetera
              * Mixed columns
                * contain all columns that don't fit the categorical or descriptive criteria.
            * String scoring:
              * similar to numerical scoring
              * edit distance for categorical columns is penalized less (a spelling mistake "blue" vs "blu" is less severe than a discrepancy between serial numbers)
              * for categorical and descriptive columns: more string tokens matched = higher score

          * `ImageLabelingMatcher.cs`
            * analyzes the image and applies strings as tags to represent the image contents.
            * uses clip-vit-b32-uint8 found here `jb\src\core\Images\Classify\ONNX\clip-vit-b32-uint8\`
            * Example: an image showing the front of a human model head to toe wearing a blue jeans and red shirt will have the following tags:
              * "silhouette" indicating a human is fully within frame
              * "blue", "jeans" 2 tags linked to each other representing the blue jeans
              * "red", "shirt" 2 tags linked to each other representing the red shirt
              * "background", "#ffffff", 2 tags linked to each other indicating the background is entirely "flat" and has a single color,namely white.
              * "pose", "front" indicating the object (the human wearing the clothes) is facing the camera

        * `ImageOrderer.cs`: Orders the set of images associated to a single familyID using rules found here `jb\src\core\Images\Order\ImageNGP.json` as well as relevant information retrieved from matching.
          * images showing strong signs of the image depicting a frontal view, will be strongly associated to be the first image in a family and will be renamed with the suffix "_det0"
            * signs can be:
              * string tokens like "front", "frontal", "a", "1", "det0"
              * computer vision detecting the image is showing a frontal view (through image labels or otherwise, **help me decide**)
          * typically, the first image is the front view, the second image is the back view, the third image is a three quarter view or a detail picture, etcetera.
      * image transformation
        * Images are transformed image per image.
        * Every image is transformed based on the result of an image analysis enriched with the information from the image matching process.
        * Preprocessor.cs handles the actual image analysis (salient object detection, bounding box calculation, background identification)
        * Useful tags and tokens generated during image matching (coming from ImageMatcher.cs) are used to attenuate transformation parameters.
          * if the object of the image is a very light color (white, light gray, creme, ...) and the background is also a very light color, the object detection algorithm should use different parameters to improve bounding box calculation
        * if the object of the image exists the image frame at one or more image edges (ie, the object appears as cropped) a margin cannot be applied to the object in the direction of that image edge. (If the bottom image edge crops the object, a margin cannot be applied to the bottom of the object. The object should "stick" to the bottom image edge. )
        * When repositioning the object of an image, a margin should be applied to the object so there is whitespace between the object and the edge of the image. To do so, the original image is cropped using the bounding box coordinates and the desired margin value. If this repositioning-by-cropping would cause the original image to be moved so that new pixels need to be added to the image, those new pixels should be filled in such a way as to mimic the already-existing pixels that make up the background.
      * generation logic
        * if an image collection for a specific familyID has x or fewer images(configurable in `Prism_Config.json`), new images should be generated locally if the existing original images are high enough quality.
        * Recommended local path: run a small Stable Diffusion or SDXL Turbo workflow through ComfyUI on the same machine or LAN server, with ONNX Runtime considered later only if model conversion and quality are proven.
        * External SaaS generation services such as KREA.ai are examples of the kind of capability, not permitted pipeline dependencies.
  * receive output from pipeline.cs and return it to whatever frontend is applicable in the desired output format


#### Project Terminology/Vocabulary:

* **Request** is a suffix added to variables or names for something a client asks Prism.
* **Result** is the suffix added to variables or names for something a class sends back to the requesting class or something Prism sends back to a client.
* A **Job**: is the entire process including every single step start to finish.
* A **Batch**: the part of a job where the actual images are processed (classified, matched, ordered, renamed, generated, transformed, exported) as well as the term for the actual image collection (not a reference to the images, the complete collection of all image files in a job, including those found inside zip files or remote locations, regardless of their shape such as byte stream, memory-backed stream, artifact reference, or file on disk).
* **IEM** is short for "Internal Excel Model". The Internal Excel Model is made up out of all collated worksheets with deduplicated rows/columns.
*

## Completed Todo Decisions

### jb/src/workbench/wpf/

- Define direct core invocation: say how WPF calls the core library without API upload and download.
  - Answer:
    WPF calls the core library by constructing the same `PrismJobRequest` shape as the API and passing it directly to `Prism.Process`.

    WPF direct invocation rules:
    - WPF passes local file, folder, stream, Excel, and zip input descriptors directly instead of wrapping them as API upload objects.
    - WPF exposes all `PrismProcessingParameters` in one job-request UI location, with binary parameters grouped together.
    - WPF receives the same `PrismJobResult` contract as API callers.
    - WPF subscribes to the shared progress event stream rather than inventing WPF-only progress stages.
    - WPF must preserve API/workbench parity for validation semantics, stage order, KO grouping, manifest interpretation, diagnostics, and output preview.

### jb/src/core/

- Define `Prism.Process` input type: name the C# type that carries normalized images, Excel files, zip results, options, and cancellation into core.
  - Answer:
    Use a C# PODO named `PrismJobRequest`.

    `PrismJobRequest` is the structured job contract passed into `Prism.Process`. It represents one logical client-requested job after API, WPF, local path, stream, URL, multipart, and zip inputs have been structured into PRISM input records. `Prism.cs` acts as the outward-facing facade: it receives caller input and the PRISM-owned internal `JobID` assigned by the job acceptance boundary, builds or completes the `PrismJobRequest`, passes structured job input onward for processing, receives structured output from the processing/exporter flow, and returns the processed result to the requester.

    Required fields:
    - `Guid JobID`: PRISM-owned internal job ID assigned when the API/server accepts and queues the job, or when a direct WPF invocation creates its local PRISM job context. External caller-provided IDs must not become PRISM's internal job ID.
    - `string? ClientRequestToken`: optional caller-provided token. This is only echoed back to the requester so external systems can correlate the returned result with their original request.
    - `IReadOnlyList<ImageRecord_INPUT> ImageRecords`: image input records. `ImageRecord_INPUT.cs` is a PODO and carries image input metadata and normalized input references.
    - `IReadOnlyList<InputExcelFileRecord> ExcelRecords`: Excel input records. `InputExcelFileRecord.cs` is a PODO and carries Excel input metadata and input references.
    - `IReadOnlyList<InputZipFileRecord> ZipFileRecords`: zip input records. `InputZipFileRecord.cs` is a PODO and carries zip input metadata, extracted member information, and zip-level import state.
    - `PrismProcessingParameters PrismProcessingParameters`: per-job processing parameters, including output format, transform toggle, generation toggle, diagnostics settings, and `ReturnOriginalImages`.

    Processing rules:
    - `PrismJobRequest` is the contract between the outward-facing caller and the `Prism.cs` facade.
    - `Prism.cs` should read as simple orchestration code: structure input, use the PRISM-owned internal `JobID`, call pipeline, call explicit cleanup/error helpers, and return the result.
    - `Pipeline.cs` owns processing and disposal of pipeline resources. It receives structured input from `Prism.cs` and returns structured output through the exporter flow back to `Prism.cs`.
    - `Prism.cs` handles cleanup only by calling explicitly named helper classes or methods. Cleanup and error logic must live in dedicated classes such as `JobCleaner.cs`, `JobErrorHandling.cs`, or equivalent strategy/helper classes.
    - `PrismJobRequest` must not expose raw frontend upload objects, API-specific request types, WPF-specific objects, or platform-specific link objects.
    - There is no cancellation token and no cancellation path. Every accepted `Prism.Process` request is processed in the same way from start to natural completion.

    Validation rules:
    - `Prism.Process` receives and uses the PRISM-owned internal `JobID`; it must not create a second job ID.
    - Caller-provided job IDs or request IDs are never trusted or reused as PRISM job IDs. They remain correlation tokens only.
    - `Prism.Process` rejects a job before pipeline execution if the job has no accepted image records, no accepted Excel records, missing `PrismProcessingParameters`, or invalid input record structure.
    - User-file failures already discovered during import, stream handling, URL resolution, or zip handling remain attached to the relevant input records so they can be included in the final manifest.
    - Processing is never cancelled by request option. Any accepted request is handled start to finish and ends as a completed job with OK and/or KO records.

- Define `Prism.Process` output type: name the C# type that returns output images, manifest data, KO details, and processing diagnostics.
  - Answer:
    Use a C# PODO named `PrismJobResult`.

    `PrismJobResult` is the client-facing result returned by `Prism.Process` for one requested PRISM job. It must stay scoped to the result returned to the requester and must not define or mention any internal pipeline return type.

    Required result content:
    - `Guid JobID`: PRISM's internal job ID assigned before processing and carried through `Prism.Process`.
    - `string? ClientRequestToken`: optional caller-provided token echoed back unchanged when it was supplied on the request.
    - Job status: the final job state, such as completed, completed with KO records, or failed.
    - Output image records and/or exported artifacts prepared by the exporter according to `PrismProcessingParameters`.
    - `BatchManifest`: the canonical manifest for the completed job.
    - KO records: user-file and item-level failures that were collected during import, Excel parsing, classification, matching, ordering, renaming, generation, transformation, export, or cleanup.
    - Diagnostics: safe processing diagnostics, stage summaries, warnings, and optional diagnostic artifact references.
    - Export metadata: output format, filenames or artifact references, content types, byte counts, and any safe export-level metadata needed by API or workbench callers.
    - Original image data only when `PrismProcessingParameters.ReturnOriginalImages` is true.

    Result rules:
    - `PrismJobResult` uses `Job` because it is returned for the full client-requested job.
    - `Result` is used because the object is returned to the requesting caller.
    - Original input bytes are excluded by default and are never placed in `manifest.json`.
    - The manifest remains the audit contract; byte-heavy image payloads and exported artifacts stay in result/export-specific fields.

- Define pipeline stage order: list the exact chronological stages from import normalization through export.
  - Answer:
    Use this definitive chronological stage order for one PRISM job:

    `Imported > Classified > Matched > Ordered > Renamed > Generated > Transformed > Exported`

    1. `Imported`
       Import media, parse Excel into the internal Excel model, unpack zip members, and normalize accepted image media into JPG representations.

    2. `Classified`
       Deduplicate images by visual hash, keeping the highest-resolution canonical image, then apply image classification and labeling to every canonical image.

    3. `Matched`
       Tokenize each image using all matchers, compare image labels and tokens against the Internal Excel Model, and resolve until one FamilyID remains above threshold.

    4. `Ordered`
       Per FamilyID, order all images using original filename ordering tokens and image classification labels. Matching and ordering may repeat until no unmatched candidates remain or no new matches are made in a pass.

    5. `Renamed`
       Collapse the FamilyID plus order probability into the final filename.

    6. `Generated`
       For families with a low image count, copy the hero image and create an alternative generated version when generation is enabled and source quality is sufficient.

    7. `Transformed`
       Transform each image using the rules under `jb/src/core/Images/Transform`, guided by the per-image `ImageNGP` configuration.

    8. `Exported`
       Return all output images together with `manifest.json`.

    Stage rules:
    - The stage order above is the definitive and only valid route order for every accepted job.
    - Stages may internally process many items, but the stage sequence itself must remain chronological and predictable.
    - A stage can emit OK records, KO records, warnings, diagnostics, and progress events.
    - User-file KO records do not stop the job when valid work remains.
    - PRISM-owned failures such as missing config, missing model files, invalid internal settings, or exporter failure stop the job as `Failed`.
    - There is no cancellation stage. Accepted jobs run until natural completion.

- Define user-file failure policy: say which bad inputs become KO records and allow the batch to continue.
  - Answer:
    User-owned bad inputs become KO records when PRISM can still process valid remaining work. They do not stop the job by themselves.

    Continue the job and record KO for:
    - Unsupported media files that were submitted as standalone processable input. Harmless non-media zip members follow the ignored zip member API and manifest behavior decision.
    - Corrupt, unreadable, damaged, partially decoded, or conversion-failed images and documents.
    - Bad zip members, including members that cannot be extracted, decoded, normalized, or classified into accepted image or Excel input.
    - Excel rows with missing, malformed, or non-config-compliant primary key values.
    - Excel worksheets with no usable primary key column.
    - Images that cannot be matched to an acceptable FamilyID.
    - Images that cannot be generated or transformed into an acceptable output image.

    Continuation rules:
    - User-file KO records stay attached to the relevant file, zip member, worksheet, row, or image record.
    - Valid images and Excel records continue through the fixed pipeline stage order.
    - If no valid work remains after user-file validation and KO handling, the job ends naturally with KO records instead of producing OK image output.
    - KO details must use safe messages suitable for the requester and `manifest.json`.

- Define Prism-owned failure policy: say which missing configs, model files, or invalid internal settings stop the whole pipeline.
  - Answer:
    PRISM-owned failures fail fast and loud because retrying a single user item cannot fix missing or invalid system dependencies. These failures stop the job as `Failed`.

    Stop the job for:
    - Missing, unreadable, or invalid `Prism_Config.json`.
    - Missing, unreadable, or invalid required folder-local `..._config.json` files.
    - Missing, unreadable, invalid, or incompatible required model files.
    - Invalid internal settings, schemas, thresholds, tensor names, export settings, or configured limits.
    - Unavailable required job storage, temporary storage, output assembly storage, or cleanup-critical infrastructure.
    - Exporter failure that prevents PRISM from returning the requested output format.

    Failure rules:
    - These failures are not converted into per-image KO records.
    - The job result reports the job as failed with safe diagnostic details.
    - PRISM-owned failures should be detected before expensive work whenever possible.

- Define Prism configuration loading lifecycle: say when `Prism_Config.json` and folder-local configs are loaded and validated.
  - Answer:
    `Prism.cs` builds the PRISM configuration object on server startup. That configuration object loads `Prism_Config.json` and all required folder-local `..._config.json` files.

    Lifecycle rules:
    - Configuration is loaded before PRISM accepts jobs.
    - Configuration is validated before `Prism.Process` starts pipeline execution.
    - V1 job queue settings, including max queued jobs and max concurrent jobs, are loaded from the PRISM runtime configuration before PRISM accepts jobs.
    - Missing or invalid PRISM-owned configuration fails fast and marks the job as failed if discovered during job acceptance.
    - PRISM does not read mutable configuration in the middle of a stage.
    - Each job uses the effective configuration that was valid when the job was accepted.
    - The effective configuration snapshot, or safe summary of it, is available for manifest and diagnostics.
    - This lifecycle does not define the final ownership split for every config key; folder-local versus central config remains governed by the dedicated config ownership TODOs.

### jb/src/core/Excel/

- Define primary key config source: primary key rules come from `RecordPrimaryKey` and `FamilyIDProperties` in `ExcelConfig.json`.
  - Answer:
    Primary key rules come from `RecordPrimaryKey` and `FamilyIDProperties` in `ExcelConfig.json`.

- Define row ownership by FamilyID: every single data row belongs to one and only one FamilyID.
  - Answer:
    Every single data row belongs to one and only one FamilyID.

- Define duplicate FamilyID rule: duplicate FamilyID records cannot exist in the internal Excel model.
  - Answer:
    Duplicate FamilyID records cannot exist in the internal Excel model.

- Define duplicate row conflict handling: deduplicate the entire row when all other cells in the involved records contain duplicate information.
  - Answer:
    Deduplicate the entire row when all other cells in the involved records contain duplicate information.

- Define conflicting duplicate row handling: when the same FamilyID appears in multiple rows, merge all non-empty data into one FamilyID record, preserve unique values, and keep conflicting values as tokenized evidence instead of overwriting them.
  - Answer:
    When the same FamilyID appears in multiple rows, merge all non-empty data into one FamilyID record, preserve unique values, and keep conflicting values as tokenized evidence instead of overwriting them.

- Define conflicting duplicate column handling: when duplicate columns disagree for the same FamilyID, tokenize both non-empty cell values, merge unique normalized tokens into the canonical property, and keep the original cell values as conflict evidence for manifest/workbench review.
  - Answer:
    When duplicate columns disagree for the same FamilyID, tokenize both non-empty cell values, merge unique normalized tokens into the canonical property, and keep the original cell values as conflict evidence for manifest/workbench review.

- Define invalid primary key row handling: rows with missing, malformed, or non-config-compliant primary key values do not stop Excel parsing; skip the row and report it as KO in `manifest.json`.
  - Answer:
    Rows with missing, malformed, or non-config-compliant primary key values do not stop Excel parsing. Skip the row and report it as KO in `manifest.json`.

- Define missing primary key column handling: when a worksheet has no primary key column, skip that worksheet and report the worksheet as KO in `manifest.json`.
  - Answer:
    When a worksheet has no primary key column, skip that worksheet and report the worksheet as KO in `manifest.json`.

- Define canonical header source: use `HeaderRowIndicators` to find the header row before selecting canonical column names.
  - Answer:
    Use `HeaderRowIndicators` to find the header row before selecting canonical column names.

- Define canonical primary key header: when a detected header row contains a cell with edit distance 0 to `RecordPrimaryKey`, use that cell as the primary key column.
  - Answer:
    When a detected header row contains a cell with edit distance 0 to `RecordPrimaryKey`, use that cell as the primary key column.

- Define required indicator count for header row detection: at least 50% of columns in a candidate header row must match configured indicators.
  - Answer:
    At least 50% of columns in a candidate header row must match configured indicators.

- Define header indicator edit-distance cutoff: an edit distance greater than 12% means the cell does not qualify as an indicator match.
  - Answer:
    An edit distance greater than 12% means the cell does not qualify as an indicator match.

- Define header indicator score for edit distance 1: a match with edit distance 1 counts as 75% confidence.
  - Answer:
    A match with edit distance 1 counts as 75% confidence.

- Define header indicator score for edit distance 2: a match with edit distance 2 counts as 50% confidence.
  - Answer:
    A match with edit distance 2 counts as 50% confidence.

- Define column validity threshold: a column must contain non-null and non-empty values in at least 20% of its rows.
  - Answer:
    A column must contain non-null and non-empty values in at least 20% of its rows.

- Define empty column handling: drop columns that do not contain enough useful values.
  - Answer:
    Drop columns that do not contain enough useful values.

- Define empty cell handling: fill empty cells with an empty string after deciding that the column itself is valid.
  - Answer:
    Fill empty cells with an empty string after deciding that the column itself is valid.

- Define duplicate column detection by header: identical headers make two columns duplicate candidates.
  - Answer:
    Identical headers make two columns duplicate candidates.

- Define duplicate column detection by content: content must be identical before two columns are considered direct duplicates.
  - Answer:
    Content must be identical before two columns are considered direct duplicates.

- Define fuzzy duplicate column merge rule: if headers differ but more than 20% of cells appear in both columns, merge and deduplicate the cells.
  - Answer:
    If headers differ but more than 20% of cells appear in both columns, merge and deduplicate the cells.

- Define primary key numeric rule: a primary key cannot be accepted unless it matches the configured numeric requirement.
  - Answer:
    A primary key cannot be accepted unless it matches the configured numeric requirement.

- Define primary key length rule: a primary key cannot be accepted unless it is exactly 8 digits under the current config.
  - Answer:
    A primary key cannot be accepted unless it is exactly 8 digits under the current config.

- Define merged cell handling: only merge cells in the same column when their value is identical.
  - Answer:
    Only merge cells in the same column when their value is identical.

- Define worksheet provenance recording: do not keep provenance beyond processing because cleanup removes all temporary batch files.
  - Answer:
    Do not keep provenance beyond processing because cleanup removes all temporary batch files.

### jb/src/core/IO/

- Define path input handling: say whether core accepts local file paths directly and who checks.
  - Answer:
    Prism accepts local path descriptors before the pipeline starts. `Importer.cs` performs the first checks for existence, size, and extension before opening the file. Only inputs that pass those checks enter normalization. Importer turns the accepted input paths, whether folder or file, local or resolved remote, into two normalized collections: one image collection and one Excel collection. Paths that fail validation are skipped and logged to `manifest.json`. Import strategy classes handle content type and origin-specific parsing, including separate strategies for remote paths and platform links such as Dropbox.

- Define stream input handling: say how memory-backed streams enter Importer and who owns stream disposal.
  - Answer:
    Memory-backed streams enter Importer as input descriptors with source metadata, stream reference, and explicit ownership. Importer reads those descriptors into the same normalized image and Excel collections used for path inputs. If the descriptor says Importer owns the stream, Importer disposes it after normalization or KO handling; otherwise the caller remains responsible for disposal.

- Define multipart file input handling: say how API upload parts become Importer inputs before the pipeline starts.
  - Answer:
    API upload parts are converted before pipeline entry into importer input descriptors containing original filename, content type, byte length, source kind, and either a stream reference or a job-temp-file reference. The API performs edge validation first, then passes the descriptors to Importer so multipart uploads follow the same normalization path as local files and resolved remote inputs.

- Define logical job folder handling: say whether each batch gets a temporary folder and what gets stored there.
  - Answer:
    Each logical job gets a temporary folder that is cleaned up once output has been sent back to the requesting client or frontend. The folder is used as spill-to-disk storage for temporary inputs, downloaded files, extracted zip members, normalized JPGs, diagnostic snapshots, and output assembly as needed.

- Define directory input handling: local folders may be scanned recursively, but recursion stops for any folder whose total byte size is below `Input.Images.filesize.min`; every discovered file is still validated individually against configured file size, extension, request size, and batch image count limits.
  - Answer:
    Local folders may be scanned recursively, but recursion stops for any folder whose total byte size is below `Input.Images.filesize.min`. Every discovered file is still validated individually against configured file size, extension, request size, and batch image count limits.

- Define link input handling: remote URLs are fetched before pipeline entry, converted into temporary input descriptors, and then handled like local files by `Importer.cs`; use a generic direct-URL import strategy by default.
  - Answer:
    Remote URLs are fetched before pipeline entry, converted into temporary input descriptors, and then handled like local files by `Importer.cs`. Use a generic direct-URL import strategy by default.

- Define remote import strategies: implement generic direct-URL, Dropbox, and WeTransfer import strategies from the start; add other platform-specific strategies only when their links require custom resolution.
  - Answer:
    Implement generic direct-URL, Dropbox, and WeTransfer import strategies from the start. Add other platform-specific strategies only when their links require custom resolution.

- Define flat jpg conversion ownership: say which IO class converts external images, PDFs, and TIFF pages into jpg bytes or streams.
  - Answer:
    `Importer.cs` owns conversion of supported external image formats, PDFs, and TIFF pages into flat JPG artifacts. Media-specific import strategies perform the format-specific work, and Importer stores the normalized JPGs in the job temporary folder before adding them to the image collection.

- Define alpha handling after import: say what happens to transparency when images are flattened to jpg.
  - Answer:
    Transparent pixels are converted to `#ffffff` when images are flattened to JPG.

- Define EXIF orientation application after flat jpg conversion: say when orientation is applied.
  - Answer:
    EXIF orientation is applied during import normalization so the normalized image is oriented correct-side-up before downstream matching, classification, and transformation. If no EXIF orientation information is found, the image orientation is kept in its original state.

- Define EXIF orientation metadata recording for normalized jpg output: say whether the normalized jpg records that orientation was applied, missing, invalid, or unchanged.
  - Answer:
    EXIF orientation is pre-pipeline normalization, not a pipeline diagnostic contract.

    Input EXIF orientation is read before pipeline processing. Valid non-default orientation is applied while converting the input image to a normalized JPG. The normalized JPG is then written with default orientation semantics so the downstream image collection is consistent.

    Invalid, missing, and default EXIF orientation do not create separate diagnostic states. No dedicated orientation-status field and no pre-normalization orientation value are recorded in `ImageRecord_INPUT`, `ImageRecord_LAMBDA`, the manifest, or the frontend journey payload.

- Define corrupt image KO reasons: list the reason codes used when an image cannot be opened, decoded, or converted.
  - Answer:
    KO reasons for images are `500` for damaged files that could not be opened or fully decoded, `500` for corrupt files where part of the image is missing, and `541` for conversion failures. Relevant details are added as a safe description for the client, while information that could be abused is not disclosed. The message appears in the console log and as an entry in `manifest.json`.

- Define original image export policy: say whether original input bytes are ever included in output or manifest data.
  - Answer:
    Original input bytes are never included by default.

    Original images are included in the returned `PrismJobResult` only when `PrismProcessingParameters.ReturnOriginalImages` is true. Even when original images are returned, `manifest.json` must not contain original image bytes. The manifest keeps only safe provenance and output references, such as original filename, source kind, normalized/import status, output references, and safe diagnostics.

    `ReturnOriginalImages=true` affects the returned result payload, not the manifest contract.

### jb/src/core/Zip/

- Define zip output parity with JSON output: say which manifest fields must be identical between zip and JSON exports.
  - Answer:
    Zip and JSON output must project from one canonical `BatchManifest`.

    The manifest truth must be identical across zip and JSON exports for:
    - Summary counts.
    - Per-item manifest rows.
    - OK and KO status values.
    - KO groups and reason details.
    - Source metadata that is safe to expose.
    - Output filenames, output references, content types, and export metadata.
    - Effective configuration snapshot or safe configuration summary.
    - Safe diagnostics and stage summaries.

    Format-specific payload placement may differ. Zip output stores processed image files and `manifest.json`; JSON output stores manifest/result data and image bytes according to export parameters. Those delivery differences must not change the manifest values.

- Define zip layout folder configurability: say whether `OK`, `KO`, and `manifest.json` can change through `ZipLayout.json`.
  - Answer:
    The folder names are always `OK` and `KO`. The manifest is always called `manifest.json`.

### jb/src/api/

- Define request model and multipart field names for `POST /PRISM/process`: write the exact query parameters and multipart body parts the API accepts before it calls Prism core, including canonical form fields for images, Excel files, zip files, URLs, and processing options.
  - Answer:
    `POST /PRISM/process` accepts `multipart/form-data`.

    The public API keeps input submission as simple and uniform as possible:
    - Multipart part `request` contains the JSON request model.
    - Multipart part `input` is repeated for every uploaded file, regardless of whether the file is an image, `.xlsx` Excel file, or zip file.
    - `request.Input` contains remote input strings such as direct HTTP links, Dropbox links, WeTransfer links, or cloud-platform links.
    - Clients do not classify inputs as image, Excel, or zip. Prism/API ingress combines uploaded `input` files and remote `request.Input` entries into importer descriptors, then `Importer.cs` triages by accepted media type.

    The `request` JSON shape is:

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

    Processing options are carried in the JSON request model, not query parameters.

    Accepted `format` values are `"zip"` and `"json"`.

    Uploaded local files are sent as repeated multipart `input` parts:

    ```text
    request: JSON request model
    input: uploaded file bytes for img1.jpg
    input: uploaded file bytes for products.zip
    input: uploaded file bytes for sheet.xlsx
    ```

    At least one accepted image representation and one accepted `.xlsx` Excel file must be present after import. Those accepted inputs may come from uploaded files, remote resources, or files extracted from accepted zip inputs.

    Before calling `Prism.Process`, API ingress and `Importer.cs` resolve/download/open all inputs, validate them, convert them into Prism importer descriptors, and then build the `PrismJobRequest`. `PrismJobRequest` must not expose raw multipart objects, API-specific request types, WPF-specific objects, or platform-specific link objects.

- Define V1 concurrent job handling: choose how PRISM accepts many users while limiting active heavy processing work.
  - Answer:
    PRISM V1 uses a single-server in-process bounded job queue.

    Queue policy:
    - `POST /PRISM/process` accepts and validates the configured multipart request, creates the PRISM-owned `JobID`, creates the job record, and enqueues accepted work.
    - The queue is conceptually a bounded .NET `Channel<T>` consumed by a fixed number of background workers.
    - The number of active background workers is limited by configured concurrency.
    - The queue carries job references and metadata only, such as `JobID`, effective configuration snapshot reference, job folder reference, and requested output format.
    - Image bytes, Excel bytes, zip bytes, normalized JPGs, diagnostic snapshots, and result payloads are stored in the logical job folder or result storage, not inside the queue message.
    - When the queue is full, `POST /PRISM/process` rejects the request before job creation with a pre-core API error. No `manifest.json` is produced for queue-full rejection.
    - Queued and running jobs are process-local in V1. If the server process restarts, restart recovery for queued or running jobs is not guaranteed.

    RabbitMQ decision:
    - RabbitMQ is not used for V1.
    - RabbitMQ remains a future option only if PRISM needs durable queue recovery, multiple processing servers, or broker-backed distributed workers.

- Define API progress streaming behavior: choose how clients receive stage progress while a long batch is processing.
  - Answer:
    API progress uses an asynchronous job model with Server-Sent Events for web clients.

    Submission behavior:
    - `POST /PRISM/process` accepts the configured multipart request and starts a PRISM job by placing it on the in-process bounded queue.
    - The `POST` response returns a job-start envelope quickly instead of holding the HTTP connection until zip or JSON output is ready.
    - The job-start envelope includes `JobID`, `ClientRequestToken` when supplied, `progressUrl`, `resultUrl`, and initial job status.
    - The initial accepted job status is `Queued`.
    - PRISM keeps its own PRISM-owned internal `JobID`; caller-provided tokens remain correlation tokens only.

    Progress transport:
    - Web clients subscribe to `GET /PRISM/jobs/{JobID}/progress` using Server-Sent Events.
    - SSE is the primary web progress transport because progress is one-way server-to-client data.
    - Polling is not the primary progress transport.
    - WebSockets are not used for normal PRISM progress because the client does not need a bidirectional channel for stage updates.

    Progress event payload:
    - Each SSE progress event projects the shared `PipelineProgressEvent` fields.
    - Events include job ID, stage name, current item when available, completed count, total count, severity, safe message, timestamp, and optional diagnostic snapshot reference.
    - Queue, running, completion, and failure status events may appear around pipeline-stage events so clients can display accepted, waiting, active, and terminal states.
    - Stage names use the definitive route: imported, classified, matched, ordered, renamed, generated, transformed, exported.
    - Events are monotonic for one job and never invent API-only progress stages.

    Completion and result retrieval:
    - Final zip or JSON output is fetched from `GET /PRISM/jobs/{JobID}/result` after the job reaches a completed or failed final state.
    - Zip and JSON result payload details remain owned by the zip response and JSON response todos.
    - The progress stream sends completion or failure status but does not carry the full output archive or full JSON result.

    WPF behavior:
    - WPF does not use the API progress transport when running in-process.
    - WPF subscribes directly to the same shared core progress event stream.
    - WPF and web must preserve the same progress field meanings and stage order even though the transport differs.

- Define response model for zip output: say whether the HTTP response is a raw zip stream, what headers it uses, and where `manifest.json` is located.
  - Answer:
    `format="zip"` jobs expose the final zip artifact through `GET /PRISM/jobs/{JobID}/result`.

    The SSE completion event includes or points to `resultUrl`. Web clients use that result URL to trigger zip auto-download behavior after the job reaches a completed final state.

    The result endpoint returns a raw `application/zip` stream with normal download headers only. Do not add `X-Prism-JobID` or `X-Prism-ClientRequestToken` headers.

    The zip contains:
    - `manifest.json` at archive root.
    - `OK/` at archive root.
    - `KO/` at archive root.
    - The full first `.xlsx` file whose original workbook contained the first accepted `familyID` column or accepted alternative-familyID column. The Excel file keeps its original filename.

    `OK/` contains all OK renamed, ordered, transformed output images.

    `KO/` contains normalized JPG artifacts for received images that imported successfully but became KO later. Images that cannot be decoded or imported appear in `manifest.json` KO entries only and do not get a KO image artifact.

- Define response model for JSON output: list the top-level JSON fields returned when `format=json` is requested.
  - Answer:
    `format="json"` jobs expose the final JSON result through `GET /PRISM/jobs/{JobID}/result`.

    The result endpoint returns `application/json`.

    Top-level fields are:
    - `manifest`: the canonical `BatchManifest`. This is the summary and describes all OK and KO images, KO groups, route summaries, safe diagnostics, and export metadata.
    - `images`: grouped per-image journey entries for frontend visualization.
    - `originalImages`: optional and present only when `ReturnOriginalImages=true`.

    Do not add separate top-level `summary`, `ko`, or `diagnostics` fields. Those belong inside `manifest`.

    `images` contains grouped arrays:
    - `images.ok[]`: images with an exportable OK output.
    - `images.ko[]`: images that became KO while preserving their bounded pipeline journey.

    Each `images.ok[]` and `images.ko[]` item contains:
    - `sourceReference`: safe source reference such as original path, zip member path, stream label, or URL reference.
    - `lambda`: bounded `ImageRecord_LAMBDA` journey data, including route state, matching/classification/ordering/rename/generation/transform summaries, probabilities/scores, KO state, and diagnostic snapshot references.
    - `output`: `ImageRecord_OUTPUT` data when an exportable artifact exists, otherwise `null`.

    The default JSON journey payload does not embed image bytes. Heavy artifacts, intermediate images, raw model output, and deep debug data are linked through diagnostic snapshot or output references.

    `ReturnOriginalImages=false` excludes original image bytes from JSON. `ReturnOriginalImages=true` affects only allowed result payload fields and never places original image bytes inside `manifest`.

- Define health response model: list what `GET /PRISM/health` reports about config, model files, disk space, and pipeline availability.
  - Answer:
    Return a generic "Prism Health OK" message followed by more detail:
    - Whether processing can currently accept jobs.
    - Number of jobs being processed at the time the health request was made.
    - Number of queued jobs at the time the health request was made.
    - Configured `MaxQueuedJobs`.
    - Configured `MaxConcurrentJobs`.
    - Supported runtime providers.
    - Readiness fields for config validity.
    - Required model assets.
    - Temp disk availability.

- Define config response model: list which runtime config values `GET /PRISM/config` exposes to workbench and other clients.
  - Answer:
    Expose accepted media types, max file size, max request size, max image count, output formats, and visible feature flags while hiding local paths and private provider settings.

    Also expose any parameter safe to share found in any `..._config.json` file in the repo.

- Define error payload model: choose the JSON fields used when the API rejects a request before Prism core runs.
  - Answer:
    Pre-core API rejection payloads use this JSON shape:

    ```json
    {
      "correlationId": "1234567890",
      "code": "INVALID_PAYLOAD",
      "message": "Message that describes what is invalid.",
      "details": [
        "request.Input[0]=https://example.com/file.zip",
        "maxRequestBytes=2684354560"
      ],
      "fieldErrors": [
        "request.Input[0]:CONTENT_LENGTH_REQUIRED"
      ],
      "retryable": false
    }
    ```

    Field rules:
    - `correlationId`: string. Use a string even when the value is numeric-looking because trace IDs and GUIDs are not numbers.
    - `code`: stable top-level validation code for the rejection.
    - `message`: safe user-facing summary.
    - `details`: `string[]` with safe supporting facts such as sanitized input reference, configured maximum, observed byte count, required minimum, or support-request text.
    - `fieldErrors`: `string[]` with entries shaped as `<fieldPath>:<VALIDATION_CODE>`. This intentionally has the same JSON type as `details`.
    - `retryable`: boolean. For pre-core validation errors in this todo set, use `false`.

    Field paths:
    - `request`
    - `request.Input`
    - `request.Input[0]`
    - `multipart.input[0]`
    - `format`
    - `rename`
    - `transform`
    - `generation`
    - `ReturnOriginalImages`

    Validation codes:
    - `INCOMPLETE_PAYLOAD`: Describe the minimum image, zip, and xlsx requirements using `Prism_Config.Input`.
    - `CONTENT_LENGTH_REQUIRED`: List only the first remote file for which `Content-Length` was required but not provided.
    - `REQUEST_TOO_LARGE`: List total request size and maximum request size from `Prism_Config.json`.
    - `REDIRECT_NOT_ALLOWED`: Use the safe message "File a Fetcher support request by contacting Jef Bracke".
    - `UNSUPPORTED_URL`: Use the safe message "File a URL support request by contacting Jef Bracke".
    - `FILE_TOO_LARGE`: One uploaded, downloaded, zip, image, or Excel item exceeds its configured per-item limit.
    - `FETCH_TIMEOUT`: A fetch exceeds configured connect, response-header, idle-read, or total-fetch timeout.
    - `LOOPBACK_NOT_ALLOWED`: A normal-operation URL targets `localhost`, `127.0.0.0/8`, `::1`, or any DNS result containing a loopback address.

- Define pre-pipeline external URL validation: say which URL schemes and hosts are allowed before imported media enters Prism core.
  - Answer:
    External URL validation runs before media enters Prism core. The API/fetch layer accepts only remote resources that can be parsed, policy-checked, fetched by a known fetcher, size-bounded, timeout-bounded, and converted into temporary input descriptors for `Importer.cs`.

    URL policy is loaded from `jb/src/core/IO/cfg/HostRules.json`.

    `HostRules.json` uses typed URL policy fields:

    ```json
    {
      "allowedSchemes": ["http", "https"],
      "blockedSchemes": ["ftp"],
      "blockedHostPatterns": ["reddit.com", "*.reddit.com"],
      "redirects": {
        "allowGenericDirectFileRedirects": false,
        "allowFetcherOwnedRedirects": true
      },
      "networkRanges": {
        "allowPrivate": true,
        "allowLinkLocal": true,
        "allowLoopback": false,
        "rejectAnyLoopbackDnsResult": true
      },
      "timeouts": {
        "connectSeconds": 10,
        "responseHeaderSeconds": 15,
        "idleReadSeconds": 15,
        "totalFetchSeconds": 120
      },
      "testing": {
        "allowLocalhost": false
      }
    }
    ```

    Checks:
    - Scheme: allow `http` and `https` by default. Reject `ftp` and any scheme that is not in `allowedSchemes` or is present in `blockedSchemes`.
    - Host rules: normalize the host to lowercase ASCII/punycode form and check it against `blockedHostPatterns`.
    - Private network: deliberately allow private-network, link-local, and internal IP ranges because Prism input media may live on Prism-owned local servers. This is an explicit exception to common SSRF protection and must not bypass any other checks.
    - Loopback: reject literal loopback IPs in normal operation: `127.0.0.0/8` and `::1`. Reject any DNS result containing a loopback address in normal operation when `rejectAnyLoopbackDnsResult` is true. Allow `localhost` only behind the explicit development/test config flag.
    - Size: enforce `Prism_Config.Input.MAXIMUM_REQUEST_SIZE` and the relevant per-kind limits from `Prism_Config.Input.Images`, `Prism_Config.Input.ZIP`, and `Prism_Config.Input.EXCEL`.
    - Timeout: enforce `connectSeconds`, `responseHeaderSeconds`, `idleReadSeconds`, and `totalFetchSeconds` from `HostRules.json`.
    - Fetcher routing: route accepted URLs to a known fetcher. Unsupported or policy-rejected URL inputs are dropped without trace when the request still has enough valid input. If dropping those URLs means the request can no longer satisfy configured minimums, reject the request before job creation with the pre-core error payload.
    - Redirects: generic direct-file fetching follows no redirects when `allowGenericDirectFileRedirects` is false. Dedicated fetchers may handle only redirects required for their own supported platform/domain when `allowFetcherOwnedRedirects` is true. Any unsupported redirect becomes `REDIRECT_NOT_ALLOWED`.

    Fetcher routing:
    - Direct `http` and `https` file URLs route to `jb\src\core\IO\Fetchers\Fetch_HTTPS_DirectFile.cs` unless a more specific fetcher owns the URL.
    - Dropbox links route to `jb\src\core\IO\Fetchers\Fetch_DropBox.cs`.
    - WeTransfer links route to `jb\src\core\IO\Fetchers\Fetch_WeTransfer.cs`.
    - Links that do not match supported fetcher categories are dropped without manifest, KO, or `PrismJobRequest` trace when enough valid input remains.
    - If a diagnostic is unavoidable during development, `Console.WriteLine("drip");` may be used as non-contract debug output only.

    Validation order:
    1. Parse the URL as an absolute URI.
    2. Validate scheme against `HostRules.json`.
    3. Normalize and validate host against `HostRules.json`.
    4. Resolve DNS for loopback/private-network classification.
    5. Reject literal loopback and any loopback DNS result except for explicit localhost test mode.
    6. Allow private-network/link-local/internal ranges only after scheme, host, and loopback checks pass.
    7. Select a fetcher route.
    8. Apply fetcher-specific redirect policy.
    9. Enforce `Content-Length` policy before reading when required.
    10. Enforce observed-byte caps while streaming for every fetcher.
    11. Enforce timeout caps while connecting and reading.
    12. Convert accepted downloads into temporary input descriptors for `Importer.cs`.

- Define configured request size validation: say how the API calculates total request size and compares it to `Prism_Config.json`.
  - Answer:
    Actual binary bytes are used to calculate request size.

    Per-item checks and aggregate checks are separate:
    - `*.filesize.min` and `*.filesize.max` apply to each uploaded, downloaded, image, zip, or Excel item.
    - `Input.MAXIMUM_REQUEST_SIZE` applies to the summed submitted and downloaded binary bytes.
    - `*.amount.min` and `*.amount.max` apply to accepted media counts, not to individual files.

    Remote `Content-Length` policy:
    - Generic remote fetches require `Content-Length`.
    - Dedicated `Fetch_` classes may have platform-specific behavior.
    - Every fetcher still enforces observed-byte caps while reading and stops as soon as the request or per-item limit is exceeded.

    Zip rule:
    - Compressed zip bytes may not exceed `Input.MAXIMUM_REQUEST_SIZE` or `Prism_Config.Input.ZIP.filesize.max`.
    - Expanded zip bytes do not count against `Input.MAXIMUM_REQUEST_SIZE`.
    - Normalized image bytes do not count against `Input.MAXIMUM_REQUEST_SIZE`.

    Failure behavior:
    - Request-level failure stops job creation and returns the pre-core error payload.
    - A request-level failure occurs when edge validation means the request cannot still satisfy configured minimums.
    - Item-level failures are ignored or handled by the relevant import/zip policy when enough valid input remains.
    - A job can continue if enough valid input remains after all checks.
    - Input requirements are retrieved from `Prism_Config.json` under `Prism_Config.Input`.
    - API edge validation happens before core job creation.
    - A second validation stage in `Importer.cs` happens after zip decompression and import normalization.
    - The second validation stage rejects empty, corrupt, damaged, password-protected, unsupported, or non-well-formed processable inputs according to the relevant import and zip policies.
    - Media kind is triaged from bytes, not only filename or MIME type.
    - PDF and TIFF pages are rendered according to import rules.
    - Supported image/document media are normalized into Prism's flat JPG input representation.
    - Accepted Excel files are added to the Excel collection.
    - When remaining valid input no longer satisfies configured minimums or when a Prism-owned dependency/configuration fails, stop the job.

- Define ignored zip member API and manifest behavior: say whether non-image and non-Excel zip members submitted through API input are omitted, summarized in manifest diagnostics, or recorded as KO, and reconcile this with user-file KO policy.
  - Answer:
    Non-image and non-Excel zip members submitted through API input are omitted. No record or count of them needs to be kept or mentioned anywhere. Prism simply does not care about those harmless non-media archive members.

    Corrupt, password-protected, oversized, or malformed processable members are kept as KO in `manifest.json` only, whether they came from a zip or from another input source.

    This means the user-file KO policy applies to unsupported standalone processable inputs and bad processable members. It does not require harmless non-media files inside an otherwise usable zip to become KO records or manifest diagnostics.

### jb/src/core/Excel/

- Define header indicator score for exact matches: say whether edit distance 0 counts as 100% confidence.
  - Answer:
    The method with which to perform header scoring is here:`jb\src\core\Excel\TCD FOR EXCEL COLUMN HEADER.cs`
    The metric used is Tokenized Concatenation Distance, a version of Levenshtein distance that uses the Kendall Tau correlation coefficient to take into account amount of tokens as well as any reordering to achieve 100% confidence.

### jb/src/core/Images/Classify/

- Define orientation classification values: list allowed values such as front, back, left, right, top, bottom, and unknown.
  - Answer:
    Orientation options are found in `jb\src\core\Models\ImageNGP.cs` under `HERO_ORIENTATION`.

    Allowed values:
    - `FRONT`
    - `RIGHT`
    - `BACK`
    - `LEFT`
    - `TOP`
    - `BOTTOM`
    - `UNKNOWN`

- Define border intersection detection method: say how Prism decides that content touches top, right, bottom, or left.
  - Answer:
    - Use salient object bounds as a first stage to intersection detection.
    - The second stage is edge detection on a subsample of the image.
      - The subsample area covers the entire width for horizontal (top and bottom) edges, and the full height for vertical edges (left/right).
      - The other area dimension is set using a parameter called `SubSampleWidth` set to 10% of the smallest initial image dimension.
      - A Canny Edge is performed to detect Hough Line presence.
      - If Hough lines are detected and those lines leave the image frame, the salient object is considered to be intersecting.
    - Intersections can happen at zero, one, several, or all edges.
    - The consequence of the salient object intersecting with the image frame is that the salient object cannot be repositioned. Ie, the intersected images cannot be manipulated in any way.

- Define human detection method: say which model or heuristic decides whether a person is visible.
  - Answer:
    - First the histogram is scanned to see if it contains more than x% "Human skin color" x is a configurable parameter: `MinimumSkinToneArea`. The human skin color range should take into account all skin colors under all common lighting circumstances. The result is a property of the `ImageRecord_LAMBDA` class.
    - Next a Part Affinity Field-based pose estimation without detecting keypoints should be employed in an attempt to find a human skeleton (partial or full)
    - The algorithm uses the information of the border intersection detection performed prior to human detection to help predict whether the skeleton is partial or full. (intersections at the bottom imply the legs are likely to be cut off, while left/right intersections indicate that one of the arms might be cut off)

- Define head visibility detection method: say how eyes, nose, ears, face region, or crop position proves head visibility.
  - Answer:
    - Attempt to detect facial features by considering the image as a matrix and using kernels such as the Kernel Gabor-based Weighted Region Covariance Matrix (KGWRCM) optimized for facial feature detection.
    - Limit the detection area to the top-half of the image. Scale the kernel using the previously discovered skeleton so it matches the size of a human head given the anatomical proportions vs. the image size vs. the size of the skin color region, particularly by using the width of the single biggest blob of the skin color area located in the top third of the full original image.
    - Correlate the result with the result from image classification/labeling using the ONNX model located here:  `jb\src\core\Images\Classify\ONNX\clip-vit-b32-uint8`

- Define classification confidence values: say whether traits use booleans, percentages, or both.
  - Answer:
    Traits use both a numeric confidence score and a boolean derived from that score.

    Representation rule:
    - Store the raw trait confidence as a `double`.
    - Derive the boolean decision by comparing the score to the configured classification confidence threshold.
    - The current effective threshold is `0.9` from `jb\src\core\Prism_Config.json` at `Classifiction.Confidence_Threshold`.

- Define unknown classification states across `ImageNGP` and `ImageRecord_LAMBDA`: say how unavailable, below-threshold, or unsupported classification is represented when current image classification values lack an unknown state.
  - Answer:
    - All valid imported/canonical images stay in the image collection regardless of classification confidence issues.
    - Prism is not allowed to start without required model files, so model availability is handled as a Prism-owned startup/readiness requirement. Per-image classification can still be inconclusive.
    - Classification tags are split by configured thresholds:
      - Tags with confidence greater than or equal to `Classification.Confidence_Threshold` are stored in `ImageRecord_LAMBDA.Tags.Influential`.
      - Tags with confidence greater than or equal to `Classification.Cutoff_Threshold` and below `Classification.Confidence_Threshold` are stored in `ImageRecord_LAMBDA.Tags.Trivial`.
      - Tags below `Classification.Cutoff_Threshold` are discarded from normal matching, ordering, and transform evidence.
    - Trivial tags are retained as weak evidence and diagnostics, but they do not drive decisions by themselves.
    - Influential tags are accepted classification evidence and may influence matching, ordering, and transformation.
    - Every `ImageNGP` enum has an `UNKNOWN` value. When classification is not confident enough to choose a concrete enum value, the enum is set to `UNKNOWN` instead of defaulting to a false or arbitrary value.
    - All `ImageNGP.UNKNOWN` enum values are transformation-critical unknowns.
    - `ImageRecord_LAMBDA` carries both accepted `ImageNGP` enum values and the influential/trivial tag collections for downstream stages and diagnostics.
    - When transformation starts, `ImageTransformer.cs` checks for any `ImageNGP` enum value set to `UNKNOWN`. Images with any unknown classification enum are routed to `Tx_ProblemImageProcessor.cs` for conservative processing instead of using normal transform assumptions.

### jb/src/core/Images/Classify/ONNX/clip-vit-b32-uint8/

- Define model checksum: record a hash used to verify the local `.onnx` file before inference.
  - Answer:
    The local SHA-256 checksum for `jb/src/core/Images/Classify/ONNX/clip-vit-b32-uint8/model_uint8.onnx` is:

    `4AC011172C8C022937BB83DAD2E8FC207F52F19972B36E14808CC3C8042C4E60`

    The ONNX model provider/readiness check should verify this checksum before creating an inference session, so accidental model corruption or replacement fails fast as a PRISM-owned asset problem.

### jb/src/core/Images/Classify/ONNX/

- Define ONNX runtime provider policy: say whether CPU is always supported and when GPU providers may be used.
  - Answer:
    CPU execution is the required baseline for ONNX-backed classification and labeling.
    PRISM must run on local servers and laptops whose hardware may not include a GPU, so CPU-only execution is a supported path. The absence of a GPU by itself must not disable model-dependent stages and must not make a job fail.
    Only models that can run on CPU-only are permitted.
    A GPU can only be used to enhance the productivity of what could also be done using CPU only. GPU usage is considered a bonus resource. Missing, invalid, or incompatible required model files remain PRISM-owned failures that fail fast and loud; GPU absence alone is not such a failure.

- Define ONNX fallback behavior without GPU: say how Prism behaves on local servers and laptops with CPU only.
  - Answer:
    Prism must work without GPU. Fallback from GPU to CPU is not required.
    CPU-only execution is therefore a supported fallback for ONNX-backed classification/labeling. Missing or invalid required model files still fail fast and loud as PRISM-owned failures, but absence of a GPU by itself must not disable model-dependent stages or make the job fail.

### jb/src/core/Images/

- Define `ImageRecord_INPUT` source image state after import: list the fields an image has after IO normalization and before matching starts.
  - Answer:
    `ImageRecord_INPUT` carries the stable source image state after import normalization and before classification/matching starts.

    Required source state:
    - Original filename and safe source provenance.
    - Source kind, such as local file, folder member, stream, multipart upload, URL, or zip member.
    - Original content type when known, byte length when known, and accepted media classification.
    - Normalized JPG artifact reference created by `Importer.cs`.
    - Normalized dimensions when available.
    - Optional hash or visual-hash input metadata when available.
    - Import status and safe import diagnostics.
    - No EXIF orientation diagnostic state; orientation is normalized before pipeline entry.

    Original input bytes are not included in manifest data and are returned only when `PrismProcessingParameters.ReturnOriginalImages` is true.

- Define output filename stem rules: say which family identifier becomes the filename stem.
  - Answer:
    The output filename stem is the matched `FamilyRecord` FamilyID.

    Rename collapses the probability of FamilyID plus order into a filename. Source filenames, display labels, and non-FamilyID catalog properties do not become the final stem.

- Define unmatched image naming: say whether unmatched images keep original names, get KO names, or are excluded.
  - Answer:
    Images that cannot be matched to an acceptable FamilyID become KO records.

    Unmatched images keep their original filename as safe provenance in `manifest.json`, but they do not receive an OK FamilyID-based output filename and are excluded from OK output images. Any KO export placement is governed by zip/layout policy and must not make the item look like a successful product match.

- Define duplicate visual hash handling: say how visually duplicate images are detected and reported.
  - Answer:
    Visual duplicate handling runs in the `Classified` stage after import normalization.

    PRISM deduplicates images by visual hash, using the visual hash as the key comparer. When duplicate or visually duplicate images are found, the highest-resolution image becomes the canonical image that continues through matching, ordering, renaming, generation, transformation, and export.

    Non-canonical duplicates do not produce separate OK output images. They are reported with safe source provenance in manifest/workbench diagnostics so the user can see that duplicate images were dropped or grouped without losing the canonical output decision.

- Define output extension rules: say whether every output image uses `.jpg` or preserves another normalized extension.
  - Answer:
    Processed PRISM output images use JPG by default.

    Accepted external image-like resources are normalized into flat JPG representations before entering the image-processing pipeline. Exported processed image filenames therefore use the `.jpg` extension and `image/jpeg` content type unless a later explicit derivative-output format is added.

### jb/src/core/Images/Match/

- Define `MatchEvidence` retention shape: specify top candidate evidence, rejected near-tie evidence, token bags, classification labels, `ImageNGP` snapshot, scores, weights, and explanation text.
  - Answer:
    `MatchEvidence` is the bounded matching decision and explanation object embedded by `ImageRecord_LAMBDA` for normal route visualization, manifest projection, and workbench review.

    It retains:
    - Original image identifier or source filename reference.
    - Final candidate FamilyID when one is accepted.
    - Final score, threshold status, tie status, and safe decision explanation.
    - Top candidate evidence.
    - Rejected near-tie evidence when bounded and useful.
    - Numeric token evidence, string token evidence, and classification-label evidence.
    - Relevant `ImageNGP` summary used by matching, ordering, transformation, or diagnostics.
    - Scores, confidences, weights, and matcher names needed to explain the decision.

    Heavy raw model output, verbose rejected candidate lists, intermediate artifacts, and deep debug evidence are not embedded directly. They are linked through optional diagnostic snapshot references.

- Define numeric token combination rules: say when separate number tokens may be joined to match a FamilyID.
  - Answer:
    Numeric tokens may be joined when their filename order is preserved and the joined token can form a configured FamilyID candidate.

    Current FamilyIDs are 8 digits. A single 8-digit token that exactly matches a FamilyID is strongest and has edit distance `0`. Multiple numeric tokens may combine into an 8-digit FamilyID candidate, but the combination records a token-count cost: scoring starts at `100%`, then subtracts `5% * (number of tokens - 1)` before edit-distance and length penalties are considered.

    Example: `1234_JEANS_5678_front.jpg` can combine `1234` and `5678` into `12345678`, but that combined-token candidate scores lower than a single exact `12345678` token.

- Define numeric edit-distance scoring: specify how character differences reduce a numeric match score.
  - Answer:
    Numeric scoring starts at `100%`.

    Edit distance subtracts `edit distance / string length` as a percentage from the score. An exact match has edit distance `0` and keeps `100%` confidence before other penalties. For example, `ABC` compared to `ABD` has edit distance `1`; `1 / 3` is a `33%` penalty, so the score is `67%`.

- Define numeric length-penalty scoring: specify how shorter or longer numeric candidates are penalized.
  - Answer:
    Length differences are penalized after token-combination and edit-distance scoring.

    If the candidate token set is otherwise an identical match but the column token is longer than the filename token, subtract `1 - (length difference / total column length)` from the score. Example: `abcde` compared to `abcdefgh` has a length difference of `3` over total length `8`, producing `1 - 5/8 = -0.375` as documented in `jb\docs`.

    The current configured FamilyID pattern is exactly 8 digits, so non-8-digit candidates cannot become a clean exact FamilyID match unless they are valid ordered combinations that produce an 8-digit candidate.

- Define exact matcher threshold enforcement: set the maximum accepted TCD or minimum confidence that makes an image eligible for automatic FamilyID assignment.
  - Answer:
    Numeric matcher threshold enforcement uses Tokenized Concatenation Distance for exact-character numeric candidates, not classical Levenshtein typo tolerance.

    A single exact 8-digit FamilyID token has TCD `0` and is the strongest numeric identity evidence. Ordered numeric fragments may form an 8-digit FamilyID candidate only when their concatenation exactly equals the candidate ID and the resulting TCD is less than or equal to the numeric rule `maxDistance` in `MatchingConfig.json`.

    The current numeric `maxDistance: 1` allows low-fragmentation exact-character numeric combinations, but it does not allow a one-character Levenshtein mismatch. Any character difference from the candidate FamilyID remains rejected for automatic exact FamilyID assignment.

    Reordered, incomplete, character-mismatched, or above-threshold candidates are rejected or retained only as rejected evidence in `MatchEvidence`.

- Define string normalization: say how casing, accents, punctuation, separators, and whitespace are normalized before matching.
  - Answer:
    String matching normalizes tokens before comparing filename, classification, and catalog text.

    Normalization rules:
    - Convert casing to lowercase.
    - Convert alphabetical characters with diacritic modifiers to their base alphabetical character.
    - Split punctuation and separators consistently into token boundaries.
    - Collapse whitespace.
    - Preserve original token text in bounded evidence so diagnostics can explain what was matched.

- Define image-label trigger conditions: say when Prism runs vision labeling instead of relying only on filename and Excel text.
  - Answer:
    Prism runs image classification/labeling during the `Classified` stage for every canonical image after import normalization and visual-hash deduplication.

    Labeling is not only a fallback for ambiguous filename or Excel text. Classification labels are part of the definitive pipeline route and are available before matching, ordering, generation, transformation, and export.

- Define emitted image labels: list the label categories expected from `ImageLabelingMatcher`.
  - Answer:
    `ImageLabelingMatcher` emits bounded labels with confidence for visual evidence used by matching, ordering, generation, transformation, and diagnostics.

    Expected label categories:
    - Human/silhouette presence, including whether a human appears fully within frame when confidence supports it.
    - Clothing or product categories, such as `jeans`, `shirt`, or related product terms.
    - Product colors, such as `blue` or `red`.
    - Background labels, including flat/single-color background evidence and color when available.
    - Pose or orientation labels, such as `front`, that can confirm or reduce doubt in ordering.

    Labels are retained as classification evidence with confidence and can be summarized in `MatchEvidence` and `ImageRecord_LAMBDA`.

- Define categorical column matching strategy and weights: say how short low-cardinality product values influence FamilyID matching.
  - Answer:
    `ProductColor`, `ProductType`, and `ProductMaterial` matching rules in `MatchingConfig.json` are image classification/labelling evidence against catalog fields. They are not tokenized filename-to-FamilyID identity matching.

    Current image-label evidence weights are:
    - `ProductColor`: `1.0`, strongest because color is the most valuable visual catalog cue.
    - `ProductType`: `0.8`, strong supporting evidence.
    - `ProductMaterial`: `0.5`, weaker because material is more likely to be misclassified.
    - `ALL` image-label overlap: `0.6`, broad corroborating evidence.

    These categorical/image-label weights can support or weaken candidate confidence, but they do not override exact numeric identity evidence.

- Define waterfall matching gates: specify the hard-gate bracket order for numeric, string, descriptive, mixed, and image-label evidence.
  - Answer:
    Matching is a waterfall pipeline with hard gates.

    Stage 1 bracket: only permit numerical single-token matches with edit distance `0`.

    Stage 2 bracket: permit numerical multiple-token matches with final TCD distance `<= 2.55`.

    Stage 3 bracket: permit multiple-string-token matches only if the image matches exactly one FamilyID and that FamilyID does not already have a candidate image of that image type.

    Stage 4 cleanup: KO the remaining unmatched images. These images are not renamed or processed and are kept as KO in the manifest with their original filename.

    Stage 5 finalize: finalize the image-to-FamilyID combinations, thereby clustering the image collection into FamilyID image clusters.

    After Stage 5, matching is done and PRISM moves on to det-ordering inside each FamilyID image cluster.

    After every bracket, the image collection is updated so already-matched images are not considered in following brackets.

    TCD applies to numeric matching gates. Stage 1 requires edit distance `0`; Stage 2 permits final TCD distance `<= 2.55`.

    Image-label confidence determines which labels are meaningful labels for that image.

    Filename tokens are not cleaned with `NoiseFilter.cs`. They are tokenized normally, but the NoiseFilter is not applied to image filenames.

    Excel rows from numeric columns are not cleaned with `NoiseFilter.cs`.

    Excel rows from stringcategory columns are not cleaned with `NoiseFilter.cs`.

    Excel rows from mixed columns are cleaned with `NoiseFilter.cs` before being treated as strings for matching.

    A filename string token must match internal Excel data exactly to count as matching evidence.

    Descriptive text is sanitized with `NoiseFilter.cs`, then searched with all image tokens.

    Mixed-column cells are treated like strings after NoiseFilter cleanup. All image tokens except `ImageRecord_LAMBDA.Tags.Trivial` are searched against the cleaned mixed-column text.

    If enough image tokens, including string, numeric, and classification tokens, match so there is only one Excel row remaining, there is a match.

- Define matcher tie-breaking: say how Prism chooses between competing product candidates.
  - Answer:
    Matcher tie behavior follows the waterfall pipeline.

    Stage 1 accepts only numerical single-token matches with edit distance `0`.

    Stage 2 accepts numerical multiple-token matches with final TCD distance `<= 2.55`.

    Stage 3 accepts multiple-string-token matches only when the image matches exactly one FamilyID and that FamilyID does not already have a candidate image of that image type.

    After every bracket, already-matched images are removed from consideration in later brackets.

    If an image remains a candidate for multiple FamilyIDs after all remaining images and Excel model rows have been evaluated, KO the image unless it can sit at the exact same `_det` order position in every matching FamilyID.

    Remaining unmatched images after the matching stages are KO, kept in the manifest with original filename, and are not renamed or processed.

- Define descriptive column matching: say how long product descriptions are searched without overwhelming stronger evidence.
  - Answer:
    The entire product description is read.

    Before matching, descriptive text is sanitized with `NoiseFilter.cs`.

    After sanitization, all image tokens can be searched against the descriptive text.

    If an image token matches part of the descriptive text, that is an indication that the image can match that Excel row.

    More salient tokens are more valuable.

    Longer tokens are more valuable.

    If descriptive evidence plus the other image token evidence leaves one Excel row remaining, that row's FamilyID is a candidate match.

    If this contradicts the older descriptive text statement in `jb\docs` line 184, this answer is the new truth.

- Define mixed column matching: say how columns containing letters and digits feed both numeric and string matchers.
  - Answer:
    A cell or row in a mixed column is treated like a string.

    `NoiseFilter.cs` is applied to remove configured noise patterns from that string.

    After noise removal, image tokens are searched against the cleaned mixed-column text.

    Trivial classification tags from `ImageRecord_LAMBDA.Tags.Trivial` are excluded from mixed-column matching.

    String, numeric, and non-trivial classification tokens can participate in mixed-column matching as searchable image tokens.

    If any combination of image tokens leaves a single FamilyID row for that image, that FamilyID is considered a candidate match.

- Define numeric false-positive handling for dimensions, dates, and units: say how measurement-like, date-like, and unit-adjacent numbers avoid matching product IDs.
  - Answer:
    Obvious numeric noise is excluded from FamilyID matching evidence before it can score as product identity.

    Numeric noise includes:
    - Dimension patterns such as `800x1200`.
    - Date-like values such as `2024-05-18`, `18/05/2024`, or `05.18.24`.
    - Unit-adjacent numbers such as `25cm`, `2 kg`, `100%`, `30mm`, or `5m`.
    - Numbers directly tied to date words such as `date 2024` or `expires 05`.

    Trusted identifier columns are preserved:
    - FamilyID.
    - FamID.
    - EAN.
    - SKU.
    - Ref.
    - Reference.

    `NoiseFilter.cs` owns the filtering code. Mixed-column and descriptive text matching use it before treating text as searchable evidence. Trusted numeric ID columns are not cleaned as noise.

- Define language handling: say whether strings are matched language-agnostically or through configured language rules.
  - Answer:
    String matching uses exact normalized token matching first.

    Configured multilingual synonyms can then count as matching evidence for known product words, especially product colors, product materials, and product types.

    Synonym code and synonym mapping files live in `jb/src/core/Images/Match/Translate`.

    The synonym dictionary is JSON-owned by `jb/src/core/Images/Match/Translate/TranslationConfig.json`.

    Automatic language detection or translation is not part of this decision.

- Define stop word handling: say which common words are ignored and where that list is configured.
  - Answer:
    Stop words live in `jb/src/core/Images/Match/Translate/TranslationConfig.json`.

    The config separates general stop words from domain stop words. General stop words include words such as `the`, `and`, `of`, `de`, `la`, and `les`. Domain stop words include broad product words such as `product`, `image`, `style`, `size`, `color`, and `collection`.

    Stop words are ignored by string matching but remain available in diagnostic evidence when diagnostics request ignored-token details.

    Trivial classification tags are still represented separately by `ImageRecord_LAMBDA.Tags.Trivial`, and those tags remain excluded from mixed-column matching.

### jb/src/core/Images/Order/

- Define `_det` suffix assignment and output filename suffix rules: say whether numbering is always zero-based, whether gaps are allowed, and how `_det` numbers are assigned after image ordering.
  - Answer:
    Det suffix is always zero-based.

    Order gaps are allowed between original images if images belonging to the family collection that can fulfill the role of det0, det1, or det2 (see imageNGP) can be copied and transformed into an image that can perform the role of the image that should have that order.

    When the renaming is performed, any remaining gaps are then closed.

### jb/src/core/Images/Transform/

- Define classification tag output: say which labels from matching/classification are available to transform decisions.
  - Answer:
    Transform decisions can consume bounded classification and matching tags already stored or linked on `ImageRecord_LAMBDA`.

    Available transform-facing tags include:
    - `ImageNGP.TypeOfShot` when available.
    - `ImageNGP.HERO_ORIENTATION`, including `UNKNOWN` when orientation evidence is unknown.
    - Human detection, head visibility, skin-tone, and related measured per-image state.
    - Border-intersection flags for top, right, bottom, and left.
    - Background labels, including flat or single-color background evidence and background color when available.
    - Product/category, color, material, pose, and orientation labels emitted by image classification or retained as matching evidence.

    These tags are optional decision modifiers for transformation. Core geometry such as salient object bounds and border intersections remains the primary transform input.

- Define center-and-stretch background extension for eligible images: say how new background pixels are filled around centered objects when border-intersection rules do not block repositioning.
  - Answer:
    For eligible images, repositioning centers the object by cropping or expanding geometry so the configured margin can exist between the object and the image edge.

    If repositioning causes the original image to move in a way that creates new pixels, those pixels are filled to mimic the already-existing background. Object geometry must be preserved. Images whose content intersects a border remain governed by the border-intersection no-reposition rule and are not manipulated in the blocked direction.

### jb/src/core/IO/

- Define JSON export MIME metadata: say how output content type and file extension are represented in JSON.
  - Answer:
    JSON image journey output metadata uses the same export metadata already defined for `ImageRecord_OUTPUT`.

    When a JSON image journey item has an `output` object, that output records:
    - `contentType`: the output MIME type, `image/jpeg` by default for processed PRISM output images.
    - `extension`: the output file extension, `.jpg` by default for processed PRISM output images.
    - `byteLength`: the processed output byte length when known.
    - Output artifact or preview reference when available.

    Original image bytes are still excluded by default and are never placed in `manifest.json`.

- Define JSON export property names: list the exact names for the frontend image journey payload.
  - Answer:
    The export JSON is used to build a frontend visualization of what happened to every image inside the pipeline.

    The top-level JSON fields are:
    - `manifest`: the canonical `BatchManifest`.
    - `images`: grouped per-image journey entries.
    - `originalImages`: optional and present only when `ReturnOriginalImages=true`.

    `images` contains two arrays:
    - `ok`: images with an exportable OK output.
    - `ko`: images that became KO, while preserving their bounded pipeline journey.

    Each journey item contains:
    - `sourceReference`: full original file path, zip member path, stream label, or URL reference that identifies the source without using it as a JSON object key.
    - `lambda`: bounded `ImageRecord_LAMBDA` journey data, including route state, matching/classification/ordering/rename/generation/transform summaries, probabilities/scores, KO state, and diagnostic snapshot references.
    - `output`: `ImageRecord_OUTPUT` data when an exportable artifact exists, otherwise `null`.

    ```json
    {
      "manifest": {...},
      "images": {
        "ok": [
          {
            "sourceReference": "full/original/path/or/url/to/image.jpg",
            "lambda": {...},
            "output": {...}
          }
        ],
        "ko": [
          {
            "sourceReference": "full/original/path/or/url/to/image.jpg",
            "lambda": {...},
            "output": null
          }
        ]
      }
    }
    ```

    Detailed KO reasons remain in `manifest`; `images.ko[]` exists so the frontend can show where and why the image journey stopped. Default JSON export does not embed image bytes.

### jb/src/core/Models/

- Define fields for `FamilyRecord.cs`: list the FamilyID, canonical properties, column classes, and source Excel values it stores.
  - Answer:
    `FamilyRecord` is the canonical catalog entity produced from the Internal Excel Model.

    It stores:
    - FamilyID as the product/family identifier.
    - Canonical properties derived from dynamic Excel columns.
    - Column classifications used by matching, such as primary key, categorical, descriptive, and mixed columns.
    - Normalized tokens used by numeric and string matchers.
    - Original source cell values that are safe and useful for evidence.
    - Conflict evidence from merged duplicate rows or duplicate columns.

    Duplicate FamilyID records cannot exist in the internal Excel model. When duplicate rows or columns conflict for the same FamilyID, preserve unique values and retain the conflicting source values as tokenized evidence instead of overwriting them.

- Define fields for `ImageRecord_INPUT.cs`: list original name, normalized bytes or stream, media type, source kind, and import status.
  - Answer:
    `ImageRecord_INPUT` is the handoff between import normalization and the image-processing route.

    It stores:
    - Original filename and safe source provenance.
    - Source kind, such as local path, folder member, stream, multipart upload, URL, or zip member.
    - Original content type when known and byte length when known.
    - Normalized JPG artifact reference created by `Importer.cs`.
    - Normalized dimensions when available.
    - Optional hash or visual-hash input metadata when available.
    - Import status and safe import diagnostics.
    - No EXIF orientation diagnostic state; orientation is normalized before pipeline entry.

    Original image bytes are excluded by default and are never placed in `manifest.json`.

- Define fields for `ImageRecord_LAMBDA.cs`: list links to input image, matched family, match evidence, classification state, transformation result, output name, and KO state.
  - Answer:
    `ImageRecord_LAMBDA` is the lifecycle hub for one canonical image through the definitive route: imported, classified, matched, ordered, renamed, generated, transformed, exported.

    It stores:
    - Stable link to `ImageRecord_INPUT`.
    - Optional matched `FamilyRecord` ID once matching succeeds.
    - Bounded `MatchEvidence` summary once matching has run.
    - Classification state from `ImageNGP` and measured per-image traits.
    - Ordering result and final rename data, including FamilyID, `_det` order, and final filename when available.
    - Generation route state and optional references to generated child records owned by `ImageRecord_GENERATED`.
    - Bounded `ImageTransformationResult` summary once transformation has run.
    - Optional `ImageRecord_OUTPUT` link once exportable output exists.
    - Current lifecycle status plus KO/failure state when the image cannot continue.

    It exposes an ordered per-image route list for web visualization. Each route entry has stage name, sequence, status, safe message, optional KO reason, optional bounded evidence summary, and optional diagnostic snapshot reference.

    Normal match, classification, transform, route, and naming summaries are embedded when bounded and always useful. Heavy artifacts such as intermediate images, raw model outputs, verbose rejected evidence, and debug snapshots are linked through optional diagnostic snapshot references.

    Generation-specific details remain owned by `ImageRecord_GENERATED`; `ImageRecord_LAMBDA` only records whether generation was skipped, created child records, or failed.

- Define fields for `BatchManifest.cs`: list batch summary counts, image rows, KO groups, config snapshot, and output format metadata.
  - Answer:
    `BatchManifest` is the canonical audit and export contract for a completed job.

    It stores:
    - Batch/job identifier and optional client request token when safe to echo.
    - Summary counts for images, Excel files, OK renamed records, KO renamed records, OK transformed records, KO transformed records, dropped/KO images, and generated records when applicable.
    - Per-image manifest rows projected from `ImageRecord_LAMBDA`.
    - KO groups and safe reason details for user-file and item-level failures.
    - Effective configuration snapshot or safe configuration summary.
    - Stage/route summaries, warnings, diagnostics, and optional diagnostic artifact references.
    - Output format metadata, filenames or artifact references, content types, byte counts, and export metadata.

    Zip and JSON output both project from this one canonical manifest. Original image bytes are never placed in `manifest.json`.

- Define KO and failure fields for import/export exceptions and manifest reasons: list reason code, human-readable message, source stage, source file, and whether the batch continues.
  - Answer:
    KO and failure records use safe, stable fields that can be projected into `BatchManifest`, API errors, and workbench diagnostics.

    Required fields:
    - Stable reason code.
    - Safe human-readable message.
    - Source stage using the definitive route when item-level, or the owning boundary for API/import/export failures.
    - Source file, zip member, worksheet, row, image record, or output artifact reference when available.
    - Item ID when available.
    - Retryable flag.
    - Batch-continues flag.
    - Safe details that do not expose abusable internals.

    User-owned bad inputs become KO records when valid work remains. PRISM-owned failures such as missing config, missing model files, invalid internal settings, or exporter failure stop the job as `Failed`.

- Define fields for `PipelineProgressEvent.cs`: list stage name, current item, item counts, message, severity, and optional snapshot reference.
  - Answer:
    `PipelineProgressEvent` is the shared progress contract consumed by API progress transport, WPF direct invocation, and workbench route visualization.

    It stores:
    - Job ID.
    - Stage name from the definitive route: imported, classified, matched, ordered, renamed, generated, transformed, exported.
    - Current item ID or safe current item name when available.
    - Completed count and total count when known.
    - Severity.
    - Safe message.
    - Timestamp.
    - Optional diagnostic snapshot reference.

    WPF subscribes directly to core progress events. Web clients receive the same event fields through API progress transport once that transport is defined.

- Define `InternalExcelModel` to `FamilyRecord` mapping: say how dynamic Excel columns become canonical family properties.
  - Answer:
    The Internal Excel Model maps each valid FamilyID to exactly one `FamilyRecord`.

    Mapping rules:
    - Use the configured primary key source from `ExcelConfig.json`: `RecordPrimaryKey` and `FamilyIDProperties`.
    - A primary key must satisfy the configured numeric requirement and must be exactly 8 digits under the current config.
    - Every valid data row belongs to one and only one FamilyID.
    - Duplicate FamilyID rows merge into one `FamilyRecord`.
    - Empty cells become empty strings after the column is accepted as valid.
    - Columns without enough useful values are dropped.
    - Duplicate columns with identical headers or matching content are deduplicated or merged according to the completed Excel decisions.
    - Conflicting duplicate rows or columns preserve unique values and retain conflicting values as tokenized evidence.
    - Invalid primary-key rows and worksheets without a usable primary-key column are skipped and reported as KO in `manifest.json`.

- Retire separate `MatcherResult.cs` model: `MatchEvidence.cs` now owns the combined matching decision.
  - Answer:
    `MatcherResult.cs` is obsolete in the current repo shape. The combined matching decision belongs in `jb\src\core\Images\Match\MatchEvidence.cs`, including candidate FamilyID, score, threshold status, tie status, runner scores, selected evidence, and decision explanation.

- Define fields for `MatchEvidence.cs`: list final candidate FamilyID, score, threshold status, tie state, selected evidence, rejected near ties, token bags, `ImageNGP` snapshot, and explanation text.
  - Answer:
    `MatchEvidence` is the bounded matching decision and explanation object embedded by `ImageRecord_LAMBDA`.

    It stores:
    - Original image identifier or source filename reference.
    - Final candidate FamilyID when accepted.
    - Final score.
    - Threshold status.
    - Tie status.
    - Runner scores or bounded candidate summaries when useful.
    - Top candidate evidence.
    - Rejected near-tie evidence when bounded.
    - Numeric token evidence, string token evidence, and classification-label evidence.
    - Relevant `ImageNGP` summary used by matching, ordering, transformation, or diagnostics.
    - Matcher names, scores, confidences, weights, and safe explanation text.
    - Optional diagnostic snapshot references for heavy or verbose evidence.

- Define `ImageRecord_LAMBDA` matcher evidence reference: say whether evidence is embedded, linked by id, or stored as a list.
  - Answer:
    `ImageRecord_LAMBDA` embeds bounded `MatchEvidence` summary data that is needed for normal route visualization, manifest projection, and workbench explanation.

    Heavy matching diagnostics are not embedded directly. Raw model outputs, verbose rejected candidate lists, intermediate artifacts, and deep debug evidence are linked through optional diagnostic snapshot references on the relevant route entry or evidence summary.

    This keeps each image route easy to display on the web page while preventing large batches from carrying unbounded evidence inside every `ImageRecord_LAMBDA`.

- Define classification storage split between `ImageNGP.cs` and `ImageRecord_LAMBDA.cs`: list which values are canonical taxonomy and which values are measured per-image state.
  - Answer:
    `ImageNGP.cs` owns reusable classification taxonomy and transform-facing image permutations.

    `ImageNGP` currently contains canonical taxonomy for:
    - Lighting.
    - Background.
    - Type of shot.
    - Hero orientation.
    - Hero head visibility.
    - Hero human presence.

    `ImageRecord_LAMBDA.cs` owns measured per-image state and route summaries that are specific to one canonical image.

    `ImageRecord_LAMBDA` stores or links measured state for:
    - Border intersections.
    - Human detection output.
    - Head visibility output.
    - Skin-tone area or related measured signals.
    - Per-trait confidence scores and derived booleans.
    - Unknown/unavailable reasons when defined.
    - The selected `ImageNGP` classification summary for that image.

    The removed `ImageClassificationTraits.cs` placeholder is not the storage owner. The unresolved `TypeOfShot` `UNKNOWN` question remains owned by the frozen image-type todo.

- Define fields for `ImageTransformationResult.cs`: list crop box, resize data, output size, background fill method, warnings, and failure reason.
  - Answer:
    `ImageTransformationResult` is the bounded transformation summary embedded by `ImageRecord_LAMBDA` and projected into manifest/workbench views.

    It stores:
    - Transformation status.
    - Input dimensions and output dimensions.
    - Crop rectangle or crop decision summary when a crop occurs.
    - Resize mode, scale factor, and target size when a resize occurs.
    - Background fill method or no-fill/no-reposition state.
    - Warnings.
    - Failure reason when transformation becomes KO.
    - Safe summary text for workbench/manifest display.
    - Optional diagnostic snapshot references for heavy artifacts such as masks, intermediate images, or preprocessing debug output.

- Define `ImageRecord_LAMBDA` transform result reference: say whether transform results are embedded or shared with manifest rows.
  - Answer:
    `ImageRecord_LAMBDA` embeds the bounded `ImageTransformationResult` summary needed for normal route visualization, manifest projection, and workbench explanation.

    Heavy transform diagnostics are not embedded directly. Intermediate images, masks, raw preprocessing artifacts, and deep debug snapshots are linked through optional diagnostic snapshot references on the transform route entry or transform summary.

    Manifest rows project selected safe fields from the embedded bounded summary and may link to diagnostic artifacts when diagnostics are enabled.

- Define fields for `ImageRecord_OUTPUT.cs`: list final filename, extension, MIME type, byte source, dimensions, and export status.
  - Answer:
    `ImageRecord_OUTPUT` defines an exportable processed image artifact.

    It stores:
    - Final filename.
    - Extension, `.jpg` by default for processed PRISM output images.
    - MIME type, `image/jpeg` by default for processed PRISM output images.
    - Artifact or byte source reference used by exporters.
    - Width and height.
    - Byte length when known.
    - Checksum when available.
    - Export status.
    - Safe export metadata needed by zip, JSON, manifest, API, or workbench consumers.

- Define manifest row projection: say which processed image fields are copied into the exported manifest.
  - Answer:
    Manifest rows project safe, stable fields from `ImageRecord_INPUT`, `ImageRecord_LAMBDA`, `MatchEvidence`, `ImageTransformationResult`, and `ImageRecord_OUTPUT`.

    Manifest row fields include:
    - Original filename and safe source provenance.
    - Final filename when an OK output exists.
    - Current/final status.
    - KO reason when applicable.
    - Matched FamilyID when accepted.
    - Route-stage summaries for imported, classified, matched, ordered, renamed, generated, transformed, and exported.
    - Bounded matching evidence summary and scores.
    - Bounded classification summary and confidence state.
    - Bounded transformation summary.
    - Output metadata such as extension, MIME type, dimensions, byte length, checksum, and export status when available.
    - Safe diagnostics and optional diagnostic artifact references.

    Original image bytes are never included in `manifest.json`.

- Define fields for `ImageRecord_GENERATED.cs`: list source FamilyID, source image references, generation method, output image, and quality decision.
  - Answer:
    `ImageRecord_GENERATED` owns generation-specific details for generated child images.

    It stores:
    - Source FamilyID.
    - Source hero image or source image references.
    - Generation method, such as detail crop, GenAI background variation, or both.
    - Generation parameters or safe configuration snapshot.
    - Quality decision.
    - Generated output image reference when accepted.
    - KO/failure reason when rejected.
    - Safe diagnostics and optional diagnostic artifact references.

    `ImageRecord_LAMBDA` only records whether generation was skipped, created generated child records, or failed; generation-specific details remain here.

### jb/src/

- Define source tree ownership rules: state which top-level folder owns API notes, core pipeline notes, workbench notes, shared docs, and test fixtures.
  - Answer:
    Source tree ownership follows the folder-local todo structure and the current reload index:
    - `jb/src/core` owns pipeline behavior, model contracts, image processing, import/export, zip, and runtime configuration decisions.
    - `jb/src/api` owns HTTP contracts, request/response models, API validation, health/config endpoints, and progress transport.
    - `jb/src/workbench` owns shared UI/workbench behavior across web and WPF.
    - `jb/src/workbench/web` owns browser-specific upload, API client, layout, progress, and validation behavior.
    - `jb/src/workbench/wpf` owns desktop-specific file selection, direct core invocation display, layout, and WPF parity behavior.
    - Root `jb/src` todos own cross-cutting source organization and fixture placement decisions.
    - `jb\docs` owns established accepted project knowledge; folder-local `jbtodo.md` files are temporary working notes for unresolved or pending decisions.

### jb/src/workbench/

- Define shared web and WPF behavior: list the pipeline views both workbenches must show identically.
  - Answer:
    Web and WPF workbenches must show the same PRISM job semantics even when their input transport differs.

    Both workbenches show:
    - The per-image route from `ImageRecord_LAMBDA` in definitive order: imported, classified, matched, ordered, renamed, generated, transformed, exported.
    - Excel model summary.
    - Image collection and source/import state.
    - Bounded matching evidence.
    - Classification summaries.
    - Ordering and rename decisions.
    - Generation state.
    - Transformation summaries.
    - KO records and safe failure reasons.
    - Output preview.
    - The same `PrismProcessingParameters` controls in one job-parameter location, with binary parameters grouped together.

- Define diagnostic snapshot display: say how intermediate images, matcher evidence, and transform decisions are shown.
  - Answer:
    Diagnostic display is route-based and uses `ImageRecord_LAMBDA` plus optional diagnostic snapshot references.

    Both workbenches show bounded per-image route diagnostics for imported input, classification, matching, ordering/rename, generation, transformation, and exported output. Normal summaries are embedded on the route/evidence records. Heavy artifacts such as intermediate images, raw model outputs, masks, verbose rejected evidence, and deep debug data are linked through optional diagnostic snapshot references.

    Workbenches must label displayed values by source stage and link back to manifest rows where applicable.

- Define no-hidden-behavior rule: say how workbench proves it is showing raw pipeline decisions without simplifying them.
  - Answer:
    Workbench views must display PRISM-owned route, evidence, status, score, and KO data from `ImageRecord_LAMBDA`, `MatchEvidence`, `ImageTransformationResult`, `BatchManifest`, and diagnostic snapshot references without replacing those facts with UI-only interpretations.

    Rule:
    - Label displayed values by source route stage.
    - Render raw reason codes, scores, thresholds, statuses, and safe messages when available.
    - Allow friendly UI text only as an additional display layer.
    - Keep diagnostic snapshot links traceable to the source stage or manifest row.
    - Do not hide failed stages, KO reasons, rejected evidence summaries, or route states because they are inconvenient for presentation.

- Define allowed web and WPF differences: state which differences are allowed because web uploads while WPF can use local files.
  - Answer:
    Web and WPF may differ only at input selection and transport.

    Allowed differences:
    - Web sends uploads and URLs through the API.
    - WPF may pass local file, folder, stream, Excel, and zip input descriptors directly to `Prism.Process`.
    - Web receives progress through API progress transport once defined.
    - WPF may subscribe directly to the shared core progress event stream.

    Not allowed to differ:
    - `PrismJobRequest` meaning.
    - `PrismProcessingParameters` availability.
    - Validation semantics.
    - Definitive route order.
    - KO grouping.
    - Manifest interpretation.
    - Evidence display semantics.
    - Output preview semantics.

### jb/src/workbench/web/

- Define progress visualization behavior: say which stages are visible and what data appears while a batch runs.
  - Answer:
    The web workbench renders the definitive route order from progress events and `ImageRecord_LAMBDA`: imported, classified, matched, ordered, renamed, generated, transformed, exported.

    Visible progress data:
    - Stage name.
    - Current item when available.
    - Completed count and total count when known.
    - Severity.
    - Safe message.
    - Optional diagnostic snapshot references.
    - Per-image route state when available.

    The API progress transport remains separately open; this todo defines what the web page displays once progress data is available.

- Define section data shapes: list the data each section expects for uploader, Excel model, image collection, match results, and output preview.
  - Answer:
    Web section data is derived from API/core contracts and must preserve raw PRISM facts.

    Sections expect:
    - Uploader: selected image sources, Excel sources, zip sources, URL sources, local validation state, and `PrismProcessingParameters`.
    - Excel model: summary of accepted Excel inputs, FamilyID counts, skipped worksheets/rows, and safe KO details.
    - Image collection: `ImageRecord_INPUT` import state and `ImageRecord_LAMBDA` route state.
    - Match results: bounded `MatchEvidence` summaries and optional diagnostic snapshot references.
    - Classification/order/rename/generation/transform route: per-stage summaries from `ImageRecord_LAMBDA`.
    - Output preview: `ImageRecord_OUTPUT` metadata, final filenames, previewable output references, and manifest row links.
    - KO groups: safe KO/failure fields from manifest projection.

- Define upload component behavior: say how drag-and-drop collects files and external URLs for `/PRISM/process`.
  - Answer:
    Web upload keeps selected files and external URL references on the client until the user starts the job.

    Upload behavior:
    - Enable `Start Prism Job` only after the minimum accepted image source and Excel source criteria are met.
    - Keep `Start Prism Job` disabled until at least one valid Excel source and one valid image source are present.
    - When one valid Excel source and one valid image source are present, no currently allowed processing option combination is incompatible.
    - Collect URL text separately from file drops.
    - Collect images, Excel files, zip files, URL text, and all job parameters into the canonical request model.
    - Keep job parameters in one UI location, with binary parameters grouped together.
    - Leave authoritative validation to the server.
    - Treat a server validation error as making the affected source invalid, and show the safe reason from the API error payload.
    - Do not start the job until upload submission is complete and URL/zip inputs have been pushed to the backend for PRISM processing.

- Define API client behavior: say how the web app separates job start, progress events, completed manifest data, result downloads, and API errors.
  - Answer:
    Implement one typed API client layer for web submission, progress, result retrieval, downloads, and pre-core API errors.

    API client behavior:
    - Submit canonical multipart requests to `POST /PRISM/process` with the `request` JSON part, repeated `input` file parts, URL input entries, and the complete `PrismProcessingParameters` payload.
    - Treat the job-start envelope as submission acknowledgment only; it provides `JobID`, `progressUrl`, `resultUrl`, and initial status, not completed manifest data.
    - Track progress through the returned SSE `progressUrl`.
    - Fetch completed or failed job output only through the returned `resultUrl` after the progress stream reports a terminal state.
    - For `format="zip"`, handle the response as a binary zip download that contains `manifest.json`.
    - For `format="json"`, handle the response as JSON and read completed `BatchManifest` data from the `manifest` field.
    - Map pre-core API error payloads to visible upload or job-start error states before any manifest is available.

- Define drag-and-drop error states: say what users see for unsupported drops, invalid URLs, accepted-type files that fail size limits, and authoritative server rejections.
  - Answer:
    Unsupported dropped items are not submitted. Zip, Excel, and media rejection messages are grouped by category, not repeated per input item. URL validation and remote fetch validation show safe per-URL detail.

    Error display:
    - Authoritative server rejections use the documented pre-core API error payload fields for visible UI states.
    - Excel rejection message: "Excel file is corrupt, damaged, or password protected."
    - Zip rejection messages include "Zip file is too big" and "Zip file is corrupt, damaged, or password protected."
    - Unsupported media message: "Only jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif are supported."
    - Oversized media message: "Image(s) that are too big are ignored. Max size = <value from config>."

- Define Next.js project layout: choose where pages, sections, components, API client code, and CSS files live.
  - Answer:
    Use the existing structure inside `jb/src/workbench/web`.

    Layout rules:
    - Keep route files thin.
    - Isolate feature sections.
    - Keep reusable UI primitives in predictable shared locations.
    - Keep styles in predictable folders.

- Define CSS variable file: name the file that contains colors, fonts, spacing values, and other design tokens.
  - Answer:
    Web workbench design tokens belong in `PRISM-theme.css`.

- Define CSS class file: name the file that contains reusable classes for the web workbench.
  - Answer:
    Keep reusable layout and state classes in one workbench CSS file. Component-specific styles sit near their components. All colors and fonts belong in `PRISM-theme.css`.

### jb/src/workbench/wpf/

- Define progress visualization behavior: say how WPF displays the same stages and evidence as the web workbench.
  - Answer:
    WPF renders the definitive route order from shared core progress events and `ImageRecord_LAMBDA`: imported, classified, matched, ordered, renamed, generated, transformed, exported.

    WPF shows the same progress fields and evidence groupings as the web workbench:
    - Stage name.
    - Current item when available.
    - Completed count and total count when known.
    - Severity.
    - Safe message.
    - Optional diagnostic snapshot references.
    - Per-image route state when available.

- Define diagnostic snapshot display: say how WPF shows intermediate images and matcher or transform decisions.
  - Answer:
    WPF shows route-based diagnostic snapshots for imported input, classification, matching, ordering/rename, generation, transformation, KO reason, and final output.

    Normal summaries are embedded on `ImageRecord_LAMBDA`, `MatchEvidence`, and `ImageTransformationResult`. Heavy artifacts are loaded only through optional diagnostic snapshot references so WPF does not keep unbounded image histories in memory.

- Define parity requirements with web: list what must behave identically between WPF and web workbench.
  - Answer:
    WPF and web must preserve identical PRISM semantics.

    Identical behavior is required for:
    - Input validation semantics.
    - Job-parameter availability and `PrismProcessingParameters` meaning.
    - Definitive route order.a
    - Progress field meanings.
    - Evidence display semantics.
    - Diagnostic snapshot semantics.
    - Output preview semantics.
    - KO grouping.
    - Manifest interpretation.

    Transport may differ: web uses API upload/progress transport, while WPF may call `Prism.Process` directly and subscribe to core progress events.

- Define local file selection behavior: say how users choose files, folders, zips, and Excel documents locally.
  - Answer:
    WPF may pass local descriptors directly to `Prism.Process` instead of wrapping them as API upload objects.

    Local selection supports:
    - Local image files.
    - Local folders.
    - Local Excel files.
    - Local zip files.
    - Memory-backed streams when supplied by the WPF flow.

    WPF converts selected items into the same structured input meaning used by `PrismJobRequest`, then displays accepted/rejected validation results using the same safe KO/failure semantics as API uploads. WPF transport differs from web transport, but import validation, route behavior, KO grouping, manifest interpretation, and output preview semantics must remain identical.
