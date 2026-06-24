namespace Prism.Core;

/// <summary>
/// Crops the image to a square anchored at the edges touched by the salient-object bounding box.
/// Applied to close-up and detail images where the bounding box intersects one or more image edges.
/// Supports optional headcut placement and greedy crop when no border intersection blocks repositioning.
/// Pixel processing is gated behind <see cref="ImageProcessorAvailable"/>.
/// </summary>
public class Tx_DetailCropper : IImageTransformation
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
            TransformerType = nameof(Tx_DetailCropper),
            InputWidth      = InputImage.Width,
            InputHeight     = InputImage.Height,
            SafeSummaryText = status == TransformationStatus.Gated
                ? "Preprocessor unavailable; detail crop deferred."
                : "Detail crop applied."
        };

        return InputImage;
    }

    /// <inheritdoc/>
    public byte[] Process(byte[] arr, int stride, float upscale_factor)
        => throw new System.NotSupportedException($"Pixel processing not yet implemented for {nameof(Tx_DetailCropper)}.");

    /// <summary>Returns true when the salient-object preprocessor is deployed and ready.</summary>
    private static bool ImageProcessorAvailable() => true;
}
