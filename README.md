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
      * the processed image as X (X being either a BASE64 string or a .jpg image)
      * NOT the original image


## **High level flow:**
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
      * Generate tokens per image by using `FilenameTokenizer.cs`
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
              * for categorical and columns: more string tokens matched = higher score

          * `ImageLabelingMatcher.cs`
            * analyzes the image and applies strings as tags to represent the image contents.
            * uses clip-vit-b32-uint8 found here `jb\src\core\Images\Classify\ONNX\clip-vit-b32-uint8\`
            * Example: an image showing the front of a human model head to toe wearing a blue jeans and red shirt will have the following tags:
              * "silhouette" indicating a human is fully within frame
              * "blue", "jeans" 2 tags linked to each other representing the blue jeans
              * "red", "shirt" 2 tags linked to each other representing the red shirt
              * "background", "#ffffff", 2 tags linked to each other indicating the background is entirely "flat" and has a single color,namely white.
              * "pose", "front" indicating the object (the human wearing the clothes) is facing the camera

        * `ImageOrdering.cs`: Orders the set of images associated to a single familyID using rules found here `jb\src\core\Images\Order\ImageNGP.json` as well as relevant information retrieved from matching.
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
