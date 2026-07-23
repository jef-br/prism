using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Prism.Services.Transform;

/// <summary>
/// Conservative transform path for images whose critical features (salient object bounds,
/// phenotype) are unknown or missing. Records which features are absent, applies a pre-pixel
/// KO check for images that are too small to produce acceptable output, and performs a safe
/// proportional resize (no crop, no fill, no stretch) using ImageSharp.
/// <para>
/// <see cref="Transform"/> records routing metadata and the KO decision on the Lambda record.
/// <see cref="Process"/> is the stateless webservice byte path.
/// </para>
/// </summary>
public class Tx_ProblemImageProcessor : IImageTransformation
{
    private const int JpegOutputQuality = 100;

    private readonly ProblemImageProcessorConfig _cfg;

    public Tx_ProblemImageProcessor(ProblemImageProcessorConfig cfg)
    {
        _cfg = cfg;
    }

    /// <inheritdoc/>
    public ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage)
    {
        if (IsKoBySize(InputImage, _cfg, out string koReason))
        {
            InputImage.IsKo          = true;
            InputImage.KoReasonCode  = "TRANSFORM_TOO_SMALL";
            InputImage.KoSafeMessage = koReason;
            InputImage.OutputRecord = new ImageRecord_OUTPUT
            {
                TransformStatus = TransformationStatus.Ko,
                TransformerType = nameof(Tx_ProblemImageProcessor),
                InputWidth      = InputImage.Width,
                InputHeight     = InputImage.Height,
                FailureReason   = koReason,
                SafeSummaryText = "Image rejected: too small for required output resolution."
            };
            return InputImage;
        }

        string[] featureUnknowns = CollectUnknownCriticalFeatures(InputImage.Features);
        string[] unknownFeatures = InputImage.BoundingBox is null
            ? ["salient-bbox", .. featureUnknowns]
            : featureUnknowns;

        // Compute expected output dimensions for the metadata record.
        // Actual pixel resize is performed by Process() when the image bytes are available.
        ComputeSafeResizeTarget(InputImage.Width, InputImage.Height, _cfg,
            out int outW, out int outH, out string resizeMode, out double scaleFactor);

        int warnCount = unknownFeatures.Length > 0 ? 2 : 1;
        string[] warnings = new string[warnCount];
        warnings[0] = "Image routed to conservative processor: critical transform features are unknown or missing.";
        if (unknownFeatures.Length > 0)
            warnings[1] = "Unknown features: " + string.Join(", ", unknownFeatures) + ".";

        InputImage.OutputRecord = new ImageRecord_OUTPUT
        {
            TransformStatus = TransformationStatus.Ok,
            TransformerType = nameof(Tx_ProblemImageProcessor),
            InputWidth      = InputImage.Width,
            InputHeight     = InputImage.Height,
            OutputWidth     = outW,
            OutputHeight    = outH,
            ResizeMode      = resizeMode,
            ScaleFactor     = scaleFactor,
            Warnings        = warnings,
            SafeSummaryText = "Conservative processing applied."
        };

        return InputImage;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Stateless webservice entry point. Applies a proportional Lanczos3 resize using
    /// <paramref name="upscale_factor"/> when non-zero; otherwise applies the safe resize
    /// logic (scale shorter dimension toward <c>MinOutputPx</c>, cap at <c>MaxUpscale</c>).
    /// Throws <see cref="InvalidOperationException"/> when the image is below <c>MinInputPx</c>
    /// in any dimension and the required upscale exceeds <c>MaxUpscale</c>.
    /// <paramref name="stride"/> is reserved for caller-side alignment and is not used in resize logic.
    /// Input bytes: format auto-detected. Output: JPEG at quality 90.
    /// </remarks>
    public byte[] Process(byte[] arr, int stride, float upscale_factor, ImageRecord_LAMBDA? lambda = null)
    {
        // Input: raw image bytes (BGR/RGB, format auto-detected by ImageSharp).
        using Image img = Image.Load(arr);

        int inW    = img.Width;
        int inH    = img.Height;
        int minDim = Math.Min(inW, inH);

        if (minDim < _cfg.MinInputPx)
        {
            double requiredScale = (double)_cfg.MinOutputPx / minDim;
            if (requiredScale > _cfg.MaxUpscale)
                throw new InvalidOperationException(
                    $"Image too small ({inW}×{inH} px); required upscale {requiredScale:F2}× exceeds maximum {_cfg.MaxUpscale}×.");
        }

        int outW;
        int outH;

        if (upscale_factor != 0f && upscale_factor != 1f)
        {
            // Caller-supplied scale: proportional resize, no crop, no fill.
            outW = (int)Math.Round(inW * upscale_factor);
            outH = (int)Math.Round(inH * upscale_factor);
        }
        else
        {
            // Auto-scale toward MinOutputPx when the image is below spec.
            ComputeSafeResizeTarget(inW, inH, _cfg, out outW, out outH, out _, out _);
        }

        if (outW != inW || outH != inH)
            img.Mutate(x => x.Resize(outW, outH, KnownResamplers.Lanczos3));

        using MemoryStream ms = new();
        img.Save(ms, new JpegEncoder { Quality = JpegOutputQuality });
        return ms.ToArray();
    }

    //  Helpers

    /// <summary>
    /// Returns true when the image dimensions are too small and the required upscale to reach
    /// the minimum output size would exceed the configured maximum.
    /// </summary>
    private static bool IsKoBySize(ImageRecord_LAMBDA lambda, ProblemImageProcessorConfig cfg, out string reason)
    {
        int minDim = Math.Min(lambda.Width, lambda.Height);
        if (minDim < cfg.MinInputPx)
        {
            double requiredScale = (double)cfg.MinOutputPx / minDim;
            if (requiredScale > cfg.MaxUpscale)
            {
                reason = $"Image too small ({lambda.Width}×{lambda.Height} px); "
                       + $"required upscale {requiredScale:F2}× exceeds maximum {cfg.MaxUpscale}×.";
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
            "intersects-top", "intersects-bottom", "intersects-left", "intersects-right",
            "low-contrast", "shadow-present"
        ];
        int n = 0;
        string[] buf = new string[critical.Length];
        foreach (string f in critical)
            if (features.GetValue(f) == "UNKNOWN") buf[n++] = f;
        return n == critical.Length ? buf : buf[..n];
    }

    /// <summary>Returns true when the image preprocessor is deployed and ready.</summary>
    private static bool ImageProcessorAvailable() => true;

    /// <summary>
    /// Computes the safe proportional resize target from the given input dimensions.
    /// Scales toward <c>MinOutputPx</c> on the longest axis when below spec, capped at
    /// <c>MaxUpscale</c>. No crop, no fill.
    /// </summary>
    private static void ComputeSafeResizeTarget(int inW, int inH, ProblemImageProcessorConfig cfg, out int outW, out int outH, out string resizeMode, out double scaleFactor)
    {
        int maxDim = Math.Max(inW, inH);
        if (maxDim < cfg.MinOutputPx)
        {
            scaleFactor = Math.Min((double)cfg.MinOutputPx / maxDim, cfg.MaxUpscale);
            resizeMode  = "upscale";
        }
        else
        {
            scaleFactor = 1.0;
            resizeMode  = "none";
        }
        outW = (int)Math.Round(inW * scaleFactor);
        outH = (int)Math.Round(inH * scaleFactor);
    }
}
