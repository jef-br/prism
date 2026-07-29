using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Prism.Services.Transform;

/// <summary>
/// Crops the image to a square without background extension.
/// The crop is centered on the image canvas; no fill, no saliency required.
/// Used as a fallback for intersecting images that do not qualify for <see cref="Tx_DetailCropper"/>,
/// and internally by <see cref="Tx_DetailCropper"/> when a no-reposition case is detected.
/// <para>
/// <see cref="Transform"/> records the crop rectangle and output dimensions on the Lambda record.
/// <see cref="Process"/> is the stateless webservice byte path.
/// </para>
/// </summary>
public class Tx_CropSquare : IImageTransformation {
    private readonly OutputConfig _cfg;

    public Tx_CropSquare(OutputConfig cfg) {
        this._cfg = cfg;
    }

    /// <inheritdoc/>
    public ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage) {
        byte[]? bytes = InputImage.ProcessedBytes;

        if (bytes is null) {
            InputImage.OutputRecord = new ImageRecord_OUTPUT {
                TransformStatus = TransformationStatus.Ko,
                TransformerType = nameof(Tx_CropSquare),
                InputWidth = InputImage.Width,
                InputHeight = InputImage.Height,
                FailureReason = "ProcessedBytes is null.",
                SafeSummaryText = "Square crop skipped: no preprocessed bytes."
            };
            return InputImage;
        }

        // Crop against the bytes' own dimensions, not the record's ingress Width/Height: preprocessing
        // may have enlarged the image, and the record keeps its original-resolution values.
        using Image img = Image.Load(bytes);
        BoundingBox crop = ComputeCenteredSquareCrop(img.Width, img.Height);
        img.Mutate(x => x.Crop(new Rectangle(crop.X, crop.Y, crop.Width, crop.Height)));

        using MemoryStream ms = new();
        img.Save(ms, new JpegEncoder { Quality = this._cfg.JpegOutputQuality });
        InputImage.ProcessedBytes = ms.ToArray();

        InputImage.OutputRecord = new ImageRecord_OUTPUT {
            TransformStatus = TransformationStatus.Ok,
            TransformerType = nameof(Tx_CropSquare),
            InputWidth = InputImage.Width,
            InputHeight = InputImage.Height,
            CropRectangle = crop,
            OutputWidth = crop.Width,
            OutputHeight = crop.Height,
            ResizeMode = "none",
            ScaleFactor = 1.0,
            SafeSummaryText = "Square crop applied."
        };

        return InputImage;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Stateless webservice entry point. Computes a centered square crop from the decoded image,
    /// applies the crop, then scales the result by <paramref name="upscale_factor"/> when non-unity.
    /// <paramref name="stride"/> is reserved for caller-side alignment and is not used in crop logic.
    /// Input bytes: format auto-detected. Output: JPEG at quality 90.
    /// </remarks>
    public byte[] Process(byte[] arr, int stride, float upscale_factor, ImageRecord_LAMBDA? lambda = null) {
        // Input: raw image bytes (format auto-detected by ImageSharp).
        using Image img = Image.Load(arr);

        BoundingBox crop = ComputeCenteredSquareCrop(img.Width, img.Height);

        // Apply centered square crop.
        img.Mutate(x => x.Crop(new Rectangle(crop.X, crop.Y, crop.Width, crop.Height)));

        // Scale the square output when the caller requests a different size.
        if (upscale_factor != 0f && upscale_factor != 1f) {
            int scaledSide = (int)Math.Ceiling(crop.Width * upscale_factor);
            img.Mutate(x => x.Resize(scaledSide, scaledSide, KnownResamplers.Lanczos3));
        }

        using MemoryStream ms = new();
        img.Save(ms, new JpegEncoder { Quality = this._cfg.JpegOutputQuality });
        return ms.ToArray();
    }

    //  Helpers

    /// <summary>Computes a centered square crop rectangle from the given image dimensions.</summary>
    private static BoundingBox ComputeCenteredSquareCrop(int width, int height) {
        int side = Math.Min(width, height);
        int x = (width - side) / 2;
        int y = (height - side) / 2;
        return new BoundingBox {
            X = x,
            Y = y,
            Width = side,
            Height = side,
            Left = x,
            Top = y,
            Right = x + side,
            Bottom = y + side
        };
    }

    /// <summary>Returns true when the image preprocessor is deployed and ready.</summary>
    private static bool ImageProcessorAvailable() => true;
}
