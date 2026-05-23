/*
    This class should be called for any image transformation.
    It uses a strategy design pattern to load the actual transformation.
    Actual transformations are contained in `Tx_....cs` classes


    1. ImageTransformer accepts ImageRecord_LAMBDA instances as input.
       ImageRecord_LAMBDA instances hold all data related to a specific canonical image, including the actual image data.
    3. The actual image data gets transformed by the Tx_... classes
    4. Tx_... classes return the transformed image data to ImageTransformer
    5. ImageTransformer updates the ImageRecord_LAMBDA record by replacing the old image with the new image.
    6. The ImageTransformer returns the updated ImageRecord_OUTPUT object to its caller

*/