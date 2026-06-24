namespace Prism.Core;

/// <summary>
/// Crops the image to a square without background extension.
/// The crop is centered on the image canvas; no fill, no saliency required.
/// Used as a fallback for intersecting images that do not qualify for <see cref="Tx_DetailCropper"/>,
/// and internally by <see cref="Tx_DetailCropper"/> when a no-reposition case is detected.
/// Pixel processing is gated behind <see cref="ImageProcessorAvailable"/>.
/// </summary>
public class Tx_CropSquare : IImageTransformation
{
    /// <inheritdoc/>
    public ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage)
    {
        BoundingBox crop = ComputeCenteredSquareCrop(InputImage.Width, InputImage.Height);

        TransformationStatus status = ImageProcessorAvailable()
            ? TransformationStatus.Ok
            : TransformationStatus.Gated;

        InputImage.TransformationResult = new ImageTransformationResult
        {
            Status          = status,
            TransformerType = nameof(Tx_CropSquare),
            InputWidth      = InputImage.Width,
            InputHeight     = InputImage.Height,
            CropRectangle   = crop,
            OutputWidth     = status == TransformationStatus.Ok ? crop.Width  : null,
            OutputHeight    = status == TransformationStatus.Ok ? crop.Height : null,
            SafeSummaryText = status == TransformationStatus.Gated
                ? "Preprocessor unavailable; square crop deferred."
                : "Square crop applied."
        };

        return InputImage;
    }

    /// <inheritdoc/>
    public byte[] Process(byte[] arr, int stride, float upscale_factor)
        => throw new System.NotSupportedException($"Pixel processing not yet implemented for {nameof(Tx_CropSquare)}.");

    //  Helpers 

    /// <summary>Computes a centered square crop rectangle from the given image dimensions.</summary>
    private static BoundingBox ComputeCenteredSquareCrop(int width, int height)
    {
        int side = System.Math.Min(width, height);
        int x    = (width  - side) / 2;
        int y    = (height - side) / 2;
        return new BoundingBox
        {
            X      = x,    Y      = y,
            Width  = side, Height = side,
            Left   = x,    Top    = y,
            Right  = x + side, Bottom = y + side
        };
    }

    /// <summary>Returns true when the image preprocessor is deployed and ready.</summary>
    private static bool ImageProcessorAvailable() => true;
}
