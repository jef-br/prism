/*
An image transformation used when the bounding box surrounding the most salient object in the image touches one or more sides of the image.
Most often this is a close-up picture or an image showing a product detail.

    - image edges touched by the bounding box serve as an anchor.
        -> If the bounding box touches an edge, the bounding box coordinate that matches with the value of that image edge  (top & left: 0 pixels, bottom&right: width/height) is used to define the cropping coordinates.
    - image canvas is cropped to a square
    - when calculating the cropping coordinates, the image content is considered.
        - If the user passed a "headcut" parameter to PRISM for a batch, DetailCropper.cs calculates the top coordinate by detecting the y coordinate of the region between the upper lip and the bottom of the nose.
        - Without "headcut" parameter, the crop behaves "greedily".
            - crop tries to retain as much of the original image as possible
            - cropping into the top of the hair is ok, the face isn't cut off
            - if it is more aesthetically pleasing, the crop will add pixels to one dimension even to get as much of the information of the perpendicular dimension included in the final crop.
            - DetailCropper will try to crop so that at least one dimension is covered by 80% of the bounding box region. The remaining 20% can be made up out of existing pixels outside of the bounding box or "created" by stretching/generating/replicating/cloning...
                - the amount of bounding box region required in the final crop is configurable. See `jb\src\core\Prism_Config.json`
                    "Coverage": The value for how big a distance the smallest dimension should occupy in the new image.
                        Example:
                            - original image = 1000x1000px
                            - bounding box [t:264,l:573,b:1000,r:1000] (width: 426px, height: 736px)
                            - The final image size needs to be between Output.Images.Processed.MINIMUM_SIZE_IN_PIXELS and Output.Images.Processed.MAXIMUM_SIZE_IN_PIXELS (also found in `jb\src\core\Prism_Config.json`)
                            - A saliency map of the bounding box shows [x:235,y:233] as the "Center of Gravity" (CoG) for the most salient region in the bounding box
                            - The smallest dimension of the bounding box needs to be resized so it is as big as coverage
                            - The bounding box width of 426px is not enough to meet the minimum size of 800 pixels, so the smallest dimension (426px) needs to expand until it reaches the minimum value.
                                - first, we calculate how much the bounding box area would have to be upscaled to meet the minimum size requirement.
                                    - Upscaling is used conservatively, just to reach the minimum size, never "as much as possible".
                                    - The maximum factor by which an image can be upscaled is found here: Output.Images.Resize.MAXIMUM_UpScale (1.42 times its original size)
                                    - Coverage (Images.Transformation.Cropping.Coverage) indicates the smallest bounding box dimension needs to cover at least 80% of that dimension in the cropped image.
                                    - For this example (426px * 1.42 = 604.92 rounded down) 604px is not enough to cover 80% of the dimension (800px MinSize* .8 coverage = 640px required)
                                    - After upscaling, we still need to add 36px to the width, so we should expand the bounding box on the original image by 26px (amount divided by the upscale factor rounded up, plus 1px for safety --> ceil(36/1.42)+1
                                      --> These pixels all need to be added on the left, as the right of the bounding box touches the image edge.
                            - The new bounding box is now [t:264,l:547,b:1000,r:1000] (width: 453px, height: 736px)
                            - Now we reconsider the saliency map. We want to cut as little salient information as possible and arrive at a 1:1 aspect ratio of width & height.
                                - The most salient point is at [x:235,y:233]
                                - we need to set the height to 453px
                                - Instead of naively cropping by using the CoG as the height's midpoint, we check the full saliency map and position the final (square!) cropping window so we capture as much salient information as possible.
                                    - Special case: When this would result in the top edge cutting into a human face, we either cut above the face and in the hair, or we cut at the y-coordinate halfway between the lowest part of the nose and the highest point of the upper lip. This applies with or without headcut parameter
        - if no human is present, the same rules as above apply.
            - greedy crop
            - include as much information as possible

    - the primary subject (product/model) is centered as much as possible
    - if a bounding box edge touches an image edge, that edge is locked. Its coordinate cannot be modified when updating the cropping window.
    - whitespace margin between the product and the image edges is applied
    - background is stretched or otherwise filled in so that the entire image canvas is "covered" (seam-carving, generative fill, ...)
*/