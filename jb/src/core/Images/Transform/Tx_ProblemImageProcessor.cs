namespace Prism.Core;

/// <summary>
/// Conservative transform path for images whose critical features (salient object bounds,
/// background type, or phenotype) are unknown or below confidence thresholds.
/// Records the routing decision and defers pixel work until the preprocessor is available.
/// Pixel processing is gated behind <see cref="ImageProcessorAvailable"/>.
/// </summary>
public class Tx_ProblemImageProcessor : IImageTransformation
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
            TransformerType = nameof(Tx_ProblemImageProcessor),
            InputWidth      = InputImage.Width,
            InputHeight     = InputImage.Height,
            Warnings        = ["Image routed to conservative processor: critical transform features are unknown or missing."],
            SafeSummaryText = status == TransformationStatus.Gated
                ? "Preprocessor unavailable; conservative processing deferred."
                : "Conservative processing applied."
        };

        return InputImage;
    }

    /// <summary>Returns true when the salient-object preprocessor is deployed and ready.</summary>
    private static bool ImageProcessorAvailable() => false;
}
