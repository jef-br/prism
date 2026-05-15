/*

The in and out point of everything. The main class in this library.

It is the pipeline that chains all the steps from start to finish.

It is imperative that this class contain as little complex-looking code as possible.
Code written here should read like a story and should be understandable by a five year old as much as possible.

"Real code" handling more complex tasks like Asynchronous behavior, threads, semaphores, and whatnot... All should be "hidden" in a class.

- Accepts the input data using Importer.cs
- Receives the excel model from ModelBuilder.cs
- Sends all incoming Zip files to ZipHandler for unpacking
- Holds all the images returning from ziphandler in a big list
    - Memory and scalability policy:
        - PRISM should be built to handle batch sizes between 1 and 5000 images at 2MB per image with ease.
        - In practice, the configured batch cap is 2500 images so PRISM is not constantly put under heavy stress.
        - there are about 500 users, each uploading maximum 4 batches per day.
        - the maximum image size is found in Prism_Config.json, it can be changed but default is 25MB max filesize.
        - Spill to disk as soon as necessary
- Loads Prism_Config.json and exposes the resulting runtime configuration to callers such as the API.
- Receives only normalized input collections once pre-pipeline external resources have been downloaded or opened.
- External resources are not permitted inside the pipeline except the approved external upscaling API.
- sends the images away for analysis and processing
    - for every image, an analysis is performed using different Matchers
        - the matchers are tasked with a specific kind of matching: numeric matching, (fuzzy)string matching, computer vision matching (through labelling+classification)
        - each matcher compares its results against the Internal Excel Model containing the collated worksheets of all excel files that holds one record per FamilyID/veepee reference (see ExcelConfig.json) found.
            - numeric and alpha matching happens against the initial image filename 
            - computer vision matching happens in case:
                - the filename also contains natural language words
                - the internal excel model holds cells with longer strings made up out of natural language (ie a product description)
                - or holds cells that have a strings that can be interpreted as a color (blue, rojo, azul, groen, verde, cerveny, ...)                
    - every image is classified into an ImageType depending on the traits an image is showing.
        - The traits for now:
            - Borders: enumeration: None (= plein-pied), Bottom (= bottom border touched, assume plan americain), Left (= left border only touched, assume plein-pied with an arm cut-off), top, right
            - Human: Yes/No (has a human present in the image)
            - Head visible: Yes/No (if the Borders property contains "top" this indicates a head is visible but already cutoff somehow)
            - Orientation: enumeration: Front, back, left, right, top, bottom, ...
            - type: packshot, clothing, detail, ambiance, illustration, ...
            - ...
    - once an image is classified, it is processed so the main object in the image is centered in frame with a whitespace margin surrounding it. If needed, the background is stretched and cleaned up.

- once all images are processed, renames all image in one go by using the matching scores to determine the best filename candidate.
    - the filename consists out of a stem and a suffix.
        - the stem is a link to the familyID found in the internal excel model
        - the suffix is used to determine the image rank. Ie, the order in which the image will be shown on the website. The suffix consists out of "_det" followed by a zero-based count.
- once renaming is done, the images are packaged using the Exporter.
    - either they are packaged in a zipfile, or encoded as byte64 strings as part of a json object literal that represents the entire transformation.
*/
