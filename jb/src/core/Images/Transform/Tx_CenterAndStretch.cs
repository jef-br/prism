/// <summary>
/// Centers the salient object on a square canvas and stretches or fills the background as needed.
/// Applied to standard packshot and model images where the bounding box does not touch any image edge.
/// Pixel processing is gated behind <see cref="ImageProcessorAvailable"/>.
/// </summary>
public class Tx_CenterAndStretch : IImageTransformation
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
            TransformerType = nameof(Tx_CenterAndStretch),
            InputWidth      = InputImage.Width,
            InputHeight     = InputImage.Height,
            SafeSummaryText = status == TransformationStatus.Gated
                ? "Preprocessor unavailable; center-and-stretch deferred."
                : "Center-and-stretch applied."
        };

        return InputImage;
    }

    /// <summary>Returns true when the salient-object preprocessor is deployed and ready.</summary>
    private static bool ImageProcessorAvailable() => false;
}
