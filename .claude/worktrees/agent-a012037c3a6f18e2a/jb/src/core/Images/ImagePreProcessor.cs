/*
    PreProcessor.cs is the first class that touches the image with regards to image transformation.
    It is used to identify the image content and type
    after which it homogenizes the data so that when an image leaves preprocessor.cs we know the general shape and type of an image.

    - apply any EXIF Orientation
    - convert any image to a flat (single layer) JPG representation
    - calculates the bounding box of the most salient object in the image
    - image labelling & classification information is read, including the link to the ImageNGP.
    - if the largest dimension **of the object bounding box** is too large or too small, the image is resized
        - too large: resize to 2000px
        - too small: prefer local upscaling on the same machine or LAN server.
        - the only approved external pipeline exception is the upscaling API at www.letsenhance.ai.
    - if there is excess background surrounding the most salient object, the image is cropped
*/
