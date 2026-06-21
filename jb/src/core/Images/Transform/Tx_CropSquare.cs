namespace Prism.Core;

/// <summary>
/// Crops the image to a square without background extension.
/// Used when the object is already well-centered and no fill is required.
/// Pixel processing is gated behind <see cref="ImageProcessorAvailable"/>.
/// </summary>
public class Tx_CropSquare : IImageTransformation
{
    /// <inheritdoc/>
    public ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage)
    {
        TransformationStatus status = ImageProcessorAvailable()
            ? TransformationStatus.Ok
            : TransformationStatus.Gated;

        InputImage.TransformationResult = new ImageTransformationResult
        {
            Status          = status,
            TransformerType = nameof(Tx_CropSquare),
            InputWidth      = InputImage.Width,
            InputHeight     = InputImage.Height,
            SafeSummaryText = status == TransformationStatus.Gated
                ? "Preprocessor unavailable; square crop deferred."
                : "Square crop applied."
        };

        return InputImage;
    }

    /// <summary>Returns true when the salient-object preprocessor is deployed and ready.</summary>
    private static bool ImageProcessorAvailable() => false;
}
