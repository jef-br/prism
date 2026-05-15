/*

- opens zip files
- handles nested zips until 5 levels deep
- preserves initial folder structure while unzipping
- duplicate filenames are allowed
- validates FullName and target directory
- returns all images found inside
- can also generate zip files. Uses ZipLayout.json for determining zipfile layout
- loudly rejects password-protected, corrupt and large zips: max size per zip file (default 2GB per zip, configurable in Prism_Config.json)
- all 'healthy' zip, excel and image files are handled
- all other files are silently ignored


- generated zips all have this structure:
    - zip filename: either provided by the user as parameter or the name of the excel file with the first familyID column
    - OK folder: one folder containing all renamed, ordered, and transformed images
    - KO folder: contains FullName of all images where something went wrong
    - manifest.json at zip root level: described the operation.
        - amount of Images KO/OK
        - OK images: (keyvalue) new filename / initial filename (ex: "98765432_det0.jpg": "path/initialFilename.jpg")
        - KO images: grouped per KO: the FullName of the image
            - KO example reasons:
                - corrupt images: <list of all filenames including the fullpath>
                - zero familyID matches: <list of all filenames including the fullpath>
                - familyID+order collission
                - duplicate visual HASH (= duplicate image, possibly a different resolution)

                


*/