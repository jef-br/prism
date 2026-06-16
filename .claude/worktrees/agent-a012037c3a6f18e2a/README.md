Human facing description of PRISM.

= A data aggregator and image processing pipeline
    - Ingress requires At least one Excel file and one image
      - can include dropbox, wetransfer, local, remote, and zipped files
    - Turn all image data into jpg representations
    - deduplicates images prior to processing using both perceptive duplicate detection via hashing as well as flagging suspected duplicates based on metadata (filename+resolution+filesize extension-agnostic)
    - renames all images using the excel files
      - collates all worksheets
      - deduplicates columns and rows
    - transforms input images if requested
    - generates extra images if requested and allowed
    - Returns data as a json containing a manifest and a link to the processed image, or a zip file with a manifest.json and 2 folders:
      - OK: the fully processed images
      - KO: all images that could not be processed


  - Core principle :
    - three distinct things that are modeled independently, then linked:
    - ImageNGP: what an image is showing (head? human? front? back? packshot? ...)
    - DetOrderSchemes: how images should be sorted in the family, given the ImageNGP (det0:front, det1:...)
    - ProductTypeToImageMapping: image order per product type + 1 default order