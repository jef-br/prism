/*

Handles the importing of all data

- excel files (only .xlsx)
- zip files (only .zip)
- image files
    - permitted mediatypes: jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif
        - MIME types are the typical ones associated with JPEG, TIFF, PNG, PDF, WebP, BMP, and GIF.
        - filenames are treated case-insensitive
        - multipage tiffs and pdfs are rendered as one image per page.
        - if a multipage document has problems, first try to render and export the first page as a flat jpg.
        - if first-page rendering also fails, flag the file as KO and drop it.
        - alpha-channels are kept as long as possible.
        - images that are in any way corrupt are flagged as "KO" without further processing
        - EXIF orientation is handled gracefully. Missing EXIF orientation info renders the file "as-is"

External resources:
    * External resources are allowed before entering the pipeline.
    * Dropbox, WeTransfer, cloud platform links, and direct HTTP links are input media only.
    * Non-Excel resources are converted to flat jpg data as raw byte arrays or memory-backed streams before being added to the image collection.
    * Zip resources are unzipped normally before pipeline entry.
    * Valid images found inside a zip are converted to flat jpg data before being added to the image collection.
    * Excel files found inside a zip are added to the Excel collection and processed later as part of the internal Excel model.
    * Once data is inside the pipeline, external resources are not permitted except the approved external upscaling API.


### Data privacy and cleanup rules
    * all imported files (images, zip, xlsx) belonging to a batch should be deleted once the output has been sent to the requesting client.
    * prism does not attempt to delete the files from their original location (in many cases this is actually impossible)
    * temp storage is located according to industry best practices:
        * if a batch is small enough, keep it in-memory (RAM cache)
        * To decide check available memory at the right moment.
        * If not enough room is available or when in doubt, use a local on-disk /tmp folder.
    * after export: remove all traces of imported files.

### Validation behavior:
    * When PRISM files are missing, fail loud and hard.
        - invalid config
        - missing model files
        - ... anything belonging to prism itself fails loud and hard and pauses the pipeline until "order is restored".

    * Files sent to the prism pipeline (zips, excel, images) are checked prior to opening:
        - files are approached carefully. Any file causing any problems is flagged as KO in manifest.json and dropped with a verbose reason for why it was dropped.
        - empty, unsupported, damaged, or non-well-formed files are dropped as KO as well.
        - no-match images are considered KO and the manifest.json that is part of the output has an entry to group all no-match images
*/
