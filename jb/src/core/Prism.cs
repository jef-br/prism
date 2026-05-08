/*

The in and out point of everything.

- Accepts the input data using Importer.cs
- Receives the excel model from ModelBuilder.cs
- Sends all incoming Zip files to ZipHandler for unpacking
- Holds all the images returning from ziphandler in a big list
- sends the images away for analysis and processing
    - for every image, an analysis is performed using different Matchers
        - the matchers are tasked with a specific kind of matching: numeric matching, (fuzzy)string matching, computer vision matching (through labelling+classification)
        - each matcher compares its results against the internal model containing the collated worksheets of all excel files that holds one record per FamilyID/veepee reference (see ExcelConfig.json) found.
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

- once all images are processed, renames all image in one go by using the matching scores to determine the best filename candidate.
    - the filename consists out of a stem and a suffix.
        - the stem is a link to the familyID found in the internal excel model
        - the suffix is used to determine the image rank. Ie, the order in which the image will be shown on the website. The suffix consists out of "_det" followed by a zero-based count.

*/