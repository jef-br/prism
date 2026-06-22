namespace Prism.Core;

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



/*
Here is a script that shows you how to get the bounding box using opencv/python
I want this. In dotnet if needed.

``` python
import cv2
import requests
import numpy as np
import matplotlib.pyplot as plt

def refined_edges(url):
    response = requests.get(url)
    image = np.asarray(bytearray(response.content), dtype=np.uint8)
    image = cv2.imdecode(image, cv2.IMREAD_COLOR)

    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    g = gray.astype(np.float32)

    local_mean = cv2.blur(g, (31, 31))
    local_sqmean = cv2.blur(g * g, (31, 31))

    local_contrast = np.sqrt(np.maximum(local_sqmean - local_mean * local_mean, 0))
    local_contrast = local_contrast - local_contrast.min()
    local_contrast = local_contrast / (local_contrast.max() + 1e-6)

    edges = cv2.Canny(gray, 80, 160).astype(np.float32) / 255.0

    edges_u8 = (edges * 255).astype(np.uint8)
    kernel = np.ones((7, 7), np.uint8)
    edges_dilated = cv2.dilate(edges_u8, kernel, iterations=1).astype(np.float32) / 255.0

    spatial = edges_dilated * local_contrast
    spatial = spatial - spatial.min()
    spatial = spatial / (spatial.max() + 1e-6)

    mask = 1.0 / (1.0 + np.exp(- (spatial - 0.5) / 0.15))

    refined_edges = edges * mask
    binary = (refined_edges > 0.2).astype(np.uint8) * 255

    return binary


url = "https://images.unsplash.com/photo-1444464666168-49d633b86797?w=800&auto=format&fit=crop&q=60"
result = refined_edges(url)

plt.imshow(result, cmap="gray")
plt.axis("off")
plt.show()
```



*/