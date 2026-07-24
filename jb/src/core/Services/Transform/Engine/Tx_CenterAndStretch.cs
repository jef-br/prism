using OpenCvSharp;

namespace Prism.Services.Transform;

/// <summary>
/// Centers the salient object on a square canvas and fills the background with a stretched
/// extension of the source edges. Applied to images where the bounding box does not touch any edge.
/// </summary>
public class Tx_CenterAndStretch : IImageTransformation
{
    private readonly double _margin;
    private readonly bool   _headcut;
    private readonly Mat?   _colorMat;
    private readonly BgStretchConfig _bgStretch;
    private readonly HeadCutterConfig _headCutter;

    /// <summary>Creates the transformer with margin fraction, headcut flag, pre-decoded BGR Mat, and config sections.</summary>
    public Tx_CenterAndStretch(double margin, bool headcut, Mat? colorMat, BgStretchConfig bgStretch, HeadCutterConfig headCutter)
    {
        this._margin     = margin;
        this._headcut    = headcut;
        this._colorMat   = colorMat;
        this._bgStretch  = bgStretch;
        this._headCutter = headCutter;
    }

    /// <inheritdoc/>
    public ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage)
    {
        if (this._headcut && this._colorMat is not null)
            Tx_util_HeadCutter.Analyze(InputImage, this._colorMat, this._headCutter);

        byte[]?     bytes = InputImage.ProcessedBytes;
        BoundingBox bbox  = InputImage.BoundingBox!.Value;   // null-bbox routed to Tx_ProblemImageProcessor

        if (bytes is null)
        {
            InputImage.OutputRecord = new ImageRecord_OUTPUT
            {
                TransformStatus = TransformationStatus.Ko,
                TransformerType = nameof(Tx_CenterAndStretch),
                InputWidth      = InputImage.Width,
                InputHeight     = InputImage.Height,
                FailureReason   = "ProcessedBytes is null.",
                SafeSummaryText = "Center-and-stretch skipped: no preprocessed bytes."
            };
            return InputImage;
        }

        (byte[] result, int canvasSize, double scaleFactor) = CropResizeAndStretch(bytes, bbox, this._margin, this._bgStretch);
        InputImage.ProcessedBytes = result;

        var warnings = new System.Collections.Generic.List<string>();
        if (this._headcut) warnings.Add("Headcut applied.");

        InputImage.OutputRecord = new ImageRecord_OUTPUT
        {
            TransformStatus      = TransformationStatus.Ok,
            TransformerType      = nameof(Tx_CenterAndStretch),
            InputWidth           = InputImage.Width,
            InputHeight          = InputImage.Height,
            OutputWidth          = canvasSize,
            OutputHeight         = canvasSize,
            BackgroundFillMethod = "background-stretch",
            ResizeMode           = ResizeModeFor(scaleFactor),
            ScaleFactor          = scaleFactor,
            Warnings             = [.. warnings],
            SafeSummaryText      = "Center-and-stretch applied."
        };

        return InputImage;
    }

    /// <inheritdoc/>
    public byte[] Process(byte[] arr, int stride, float upscale_factor, ImageRecord_LAMBDA? lambda = null)
    {
        BoundingBox bbox = FullImageBounds(arr);

        (byte[] result, int canvasSize, _) = CropResizeAndStretch(arr, bbox, this._margin, this._bgStretch);

        if (upscale_factor is not 0f and not 1f)
        {
            int scaledSide = (int)Math.Round(canvasSize * upscale_factor);
            using Mat canvas = Cv2.ImDecode(result, ImreadModes.Color);
            using Mat scaled = new();
            Cv2.Resize(canvas, scaled, new OpenCvSharp.Size(scaledSide, scaledSide),
                interpolation: InterpolationFlags.Lanczos4);
            Cv2.ImEncode(".jpg", scaled, out byte[] scaledBytes);
            return scaledBytes;
        }

        return result;
    }

    // Layout
    //
    // Crops to the bounding box, resizes it to fit the margin-adjusted target size (preserving
    // aspect ratio), then centers it on the final square canvas and stretches the background to
    // fill the remainder. Every offset fed to Tx_util_BgStretch is non-negative by construction:
    // the resized product's longer side always equals finalBboxSize, which is always strictly
    // less than canvasSize (since margin > 0), so canvasSize - resizedSize is always positive.
    //
    // canvasSize itself: raw = longestSide * (1 + 2*margin), floored, rounded down to the nearest
    // even number, then reduced by 2px (antialiasing safety margin) — confirmed against a known
    // worked example (bbox longest side 1800, margin 0.042 -> canvasSize 1948).

    private static (byte[] result, int canvasSize, double scaleFactor) CropResizeAndStretch(
        byte[] sourceJpeg, BoundingBox bbox, double margin, BgStretchConfig bgStretch)
    {
        using Mat decoded = Cv2.ImDecode(sourceJpeg, ImreadModes.Color);
        using Mat cropped = decoded.SubMat(new Rect(bbox.X, bbox.Y, bbox.Width, bbox.Height));

        int longestSide  = Math.Max(bbox.Width, bbox.Height);
        int flooredRaw   = (int)Math.Floor(longestSide * (1.0 + 2.0 * margin));
        int evenRaw      = flooredRaw - (flooredRaw % 2);
        int canvasSize   = evenRaw - 2;

        double finalBboxSize = canvasSize * (1.0 - 2.0 * margin);
        double scaleFactor   = finalBboxSize / longestSide;

        int resizedW = Math.Max(1, (int)Math.Round(bbox.Width  * scaleFactor));
        int resizedH = Math.Max(1, (int)Math.Round(bbox.Height * scaleFactor));

        using Mat resized = new();
        Cv2.Resize(cropped, resized, new OpenCvSharp.Size(resizedW, resizedH), interpolation: InterpolationFlags.Lanczos4);

        int srcX = (canvasSize - resizedW) / 2;
        int srcY = (canvasSize - resizedH) / 2;

        Cv2.ImEncode(".jpg", resized, out byte[] resizedJpeg);
        byte[] result = Tx_util_BgStretch.Stretch(resizedJpeg, canvasSize, canvasSize, srcX, srcY, bgStretch);

        return (result, canvasSize, scaleFactor);
    }

    private static string ResizeModeFor(double scaleFactor) =>
        scaleFactor < 1.0 ? "downscale" : scaleFactor > 1.0 ? "upscale" : "none";

    private static BoundingBox FullImageBounds(byte[] arr)
    {
        using Mat mat = Cv2.ImDecode(arr, ImreadModes.Color);
        int w = mat.Empty() ? 1 : mat.Cols;
        int h = mat.Empty() ? 1 : mat.Rows;
        return new BoundingBox { X = 0, Y = 0, Width = w, Height = h,
                                 Left = 0, Top = 0, Right = w, Bottom = h };
    }
}
