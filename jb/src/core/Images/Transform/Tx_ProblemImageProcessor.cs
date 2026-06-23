namespace Prism.Core;

/// <summary>
/// Conservative transform path for images whose critical features (salient object bounds,
/// phenotype) are unknown or missing. Records which features are absent, applies a pre-pixel
/// KO check for images that are too small to produce acceptable output, and defers pixel work
/// until the preprocessor is available.
/// Pixel processing is gated behind <see cref="ImageProcessorAvailable"/>.
/// </summary>
public class Tx_ProblemImageProcessor : IImageTransformation
{
    private const int    MinInputPx  = 570;
    private const int    MinOutputPx = 800;
    private const double MaxUpscale  = 1.42;

    /// <inheritdoc/>
    public ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage)
    {
        if (IsKoBySize(InputImage, out string koReason))
        {
            InputImage.IsKo          = true;
            InputImage.KoReasonCode  = "TRANSFORM_TOO_SMALL";
            InputImage.KoSafeMessage = koReason;
            InputImage.TransformationResult = new ImageTransformationResult
            {
                Status          = TransformationStatus.Ko,
                TransformerType = nameof(Tx_ProblemImageProcessor),
                InputWidth      = InputImage.Width,
                InputHeight     = InputImage.Height,
                FailureReason   = koReason,
                SafeSummaryText = "Image rejected: too small for required output resolution."
            };
            return InputImage;
        }

        string[] unknownFeatures = CollectUnknownCriticalFeatures(InputImage.Features);

        TransformationStatus status = ImageProcessorAvailable()
            ? TransformationStatus.Ok
            : TransformationStatus.Gated;

        int warnCount = unknownFeatures.Length > 0 ? 2 : 1;
        string[] warnings = new string[warnCount];
        warnings[0] = "Image routed to conservative processor: critical transform features are unknown or missing.";
        if (unknownFeatures.Length > 0)
            warnings[1] = "Unknown features: " + string.Join(", ", unknownFeatures) + ".";

        InputImage.TransformationResult = new ImageTransformationResult
        {
            Status          = status,
            TransformerType = nameof(Tx_ProblemImageProcessor),
            InputWidth      = InputImage.Width,
            InputHeight     = InputImage.Height,
            Warnings        = warnings,
            SafeSummaryText = status == TransformationStatus.Gated
                ? "Preprocessor unavailable; conservative processing deferred."
                : "Conservative processing applied."
        };

        return InputImage;
    }

    /// <inheritdoc/>
    public byte[] Process(byte[] arr, int stride, float upscale_factor)
        => throw new System.NotSupportedException($"Pixel processing not yet implemented for {nameof(Tx_ProblemImageProcessor)}.");

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the image dimensions are too small and the required upscale to reach
    /// the minimum output size would exceed the configured maximum.
    /// </summary>
    private static bool IsKoBySize(ImageRecord_LAMBDA lambda, out string reason)
    {
        int minDim = System.Math.Min(lambda.Width, lambda.Height);
        if (minDim < MinInputPx)
        {
            double requiredScale = (double)MinOutputPx / minDim;
            if (requiredScale > MaxUpscale)
            {
                reason = $"Image too small ({lambda.Width}×{lambda.Height} px); "
                       + $"required upscale {requiredScale:F2}× exceeds maximum {MaxUpscale}×.";
                return true;
            }
        }
        reason = string.Empty;
        return false;
    }

    /// <summary>Returns the transform-critical feature ids whose current value is UNKNOWN.</summary>
    private static string[] CollectUnknownCriticalFeatures(ImageFeatureSnapshot features)
    {
        string[] critical = [
            "salient-bbox",
            "intersects-top", "intersects-bottom", "intersects-left", "intersects-right",
            "low-contrast", "shadow-present"
        ];
        int n = 0;
        string[] buf = new string[critical.Length];
        foreach (string f in critical)
            if (features.GetValue(f) == "UNKNOWN") buf[n++] = f;
        return n == critical.Length ? buf : buf[..n];
    }

    /// <summary>Returns true when the salient-object preprocessor is deployed and ready.</summary>
    private static bool ImageProcessorAvailable() => false;
}
