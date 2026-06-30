using OpenCvSharp;

namespace Prism.Core;

/// <summary>
/// Centers the salient object on a square canvas and fills the background with a stretched
/// extension of the source edges. Applied to images where the bounding box does not touch any edge.
/// </summary>
public class Tx_CenterAndStretch : IImageTransformation
{
    private readonly double _margin;
    private readonly bool   _headcut;
    private readonly Mat?   _colorMat;

    /// <summary>Creates the transformer with margin fraction, headcut flag, and pre-decoded BGR Mat.</summary>
    public Tx_CenterAndStretch(double margin, bool headcut, Mat? colorMat)
    {
        _margin   = margin;
        _headcut  = headcut;
        _colorMat = colorMat;
    }

    /// <inheritdoc/>
    public ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage)
    {
        if (_headcut && _colorMat is not null)
            Tx_util_HeadCutter.Analyze(InputImage, _colorMat);

        byte[]?     bytes = InputImage.ProcessedBytes;
        BoundingBox bbox  = InputImage.BoundingBox!.Value;   // null-bbox routed to Tx_ProblemImageProcessor

        if (bytes is null)
        {
            InputImage.TransformationResult = new ImageTransformationResult
            {
                Status          = TransformationStatus.Ko,
                TransformerType = nameof(Tx_CenterAndStretch),
                InputWidth      = InputImage.Width,
                InputHeight     = InputImage.Height,
                FailureReason   = "ProcessedBytes is null.",
                SafeSummaryText = "Center-and-stretch skipped: no preprocessed bytes."
            };
            return InputImage;
        }

        (int canvasSize, int srcX, int srcY) = ComputeLayout(bbox, _margin);

        byte[] result = Tx_util_BgStretch.Stretch(bytes, canvasSize, canvasSize, srcX, srcY);
        InputImage.ProcessedBytes = result;

        var warnings = new System.Collections.Generic.List<string>();
        if (_headcut) warnings.Add("Headcut applied.");

        InputImage.TransformationResult = new ImageTransformationResult
        {
            Status               = TransformationStatus.Ok,
            TransformerType      = nameof(Tx_CenterAndStretch),
            InputWidth           = InputImage.Width,
            InputHeight          = InputImage.Height,
            OutputWidth          = canvasSize,
            OutputHeight         = canvasSize,
            BackgroundFillMethod = "background-stretch",
            ScaleFactor          = 1.0,
            Warnings             = [.. warnings],
            SafeSummaryText      = "Center-and-stretch applied."
        };

        return InputImage;
    }

    /// <inheritdoc/>
    public byte[] Process(byte[] arr, int stride, float upscale_factor)
    {
        BoundingBox bbox = FullImageBounds(arr);

        (int canvasSize, int srcX, int srcY) = ComputeLayout(bbox, _margin);

        byte[] stretched = Tx_util_BgStretch.Stretch(arr, canvasSize, canvasSize, srcX, srcY);

        if (upscale_factor is not 0f and not 1f)
        {
            int scaledSide = (int)Math.Round(canvasSize * upscale_factor);
            using Mat canvas = Cv2.ImDecode(stretched, ImreadModes.Color);
            using Mat scaled = new();
            Cv2.Resize(canvas, scaled, new OpenCvSharp.Size(scaledSide, scaledSide),
                interpolation: InterpolationFlags.Lanczos4);
            Cv2.ImEncode(".jpg", scaled, out byte[] scaledBytes);
            return scaledBytes;
        }

        return stretched;
    }

    // Layout

    private static (int canvasSize, int srcX, int srcY) ComputeLayout(BoundingBox bbox, double margin)
    {
        int longestSide = Math.Max(bbox.Width, bbox.Height);
        int marginPx    = (int)Math.Round(longestSide * margin);
        int canvasSize  = longestSide + 2 * marginPx;

        int srcX = canvasSize / 2 - (bbox.X + bbox.Width  / 2);
        int srcY = canvasSize / 2 - (bbox.Y + bbox.Height / 2);

        return (canvasSize, srcX, srcY);
    }

    private static BoundingBox FullImageBounds(byte[] arr)
    {
        using Mat mat = Cv2.ImDecode(arr, ImreadModes.Color);
        int w = mat.Empty() ? 1 : mat.Cols;
        int h = mat.Empty() ? 1 : mat.Rows;
        return new BoundingBox { X = 0, Y = 0, Width = w, Height = h,
                                 Left = 0, Top = 0, Right = w, Bottom = h };
    }
}
