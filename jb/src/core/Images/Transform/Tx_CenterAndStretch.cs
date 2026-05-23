/*
An image transformation that calculates the bounding box surrounding the most salient object in the image.
Most often this is a product packshot or a human model wearing an outfit.

The transformation changes the original image so that the ...
    - image canvas becomes square
    - product is centered,
    - whitespace margin between the product and the image edges is applied
    - background is stretched or otherwise filled in (seam-carving, generative fill, ...) if needed
*/

public class Tx_CenterAndStretch : IImageTransformation
{
    public ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage)
    {
        
    }
}