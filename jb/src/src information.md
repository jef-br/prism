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

External resources such as Dropbox, WeTransfer, cloud platform links, and direct HTTP links are allowed as input media only.

External image-like resources must be converted before the pipeline receives them.

Non-Excel resources must be converted to flat jpg data as a raw byte array or memory-backed stream and added to the image collection for the batch.

Zip resources must be unzipped before entering the pipeline.

Each valid image found inside a zip resource must be converted to flat jpg data before entering the image processing pipeline.

Each Excel file found inside a zip resource must be added to the Excel collection and processed later as part of the internal Excel model.

Once data is inside the pipeline, external resources are not permitted.

The only permitted pipeline exception is the external upscaling API at `www.letsenhance.ai`.

Missing Prism-owned configuration files or model files should fail fast and loud.

#### Project Terminology/Vocabulary:

* **Request** is a suffix added to variables or names for something a client asks Prism.
* **Result** is the suffix added to variables or names for something a class sends back to the requesting class or something Prism sends back to a client.
* A **Job**: is the entire process including every single step start to finish.
* A **Batch**: the part of a job where the actual images are processed (matched, ordered, renamed, transformed, generated) as well as the term for the actual image collection (not a reference to the images, the complete collection of all image files in a job, including those found inside zip files or remote locations,  regardless of their shape (bytestream,base64 string, file on disk, ...)). 