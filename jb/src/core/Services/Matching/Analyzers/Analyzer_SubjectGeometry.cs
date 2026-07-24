using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Services.Matching;

/*
 Proposed workings
 -----------------
 The subject box drives every geometric feature:
   1. YOLO: the highest-confidence detection is the subject — works on ANY background,
      including lifestyle scenes (no gating).
   2. Fallback: when YOLO sees nothing (products outside the 80 COCO classes, e.g. clothing
      flat lays), the bounding rectangle of pixels far from the border background color.
   3. Neither → all geometry features stay UNKNOWN. Never guess.
 From the box: salient-bbox, image-occupancy (box area fraction), crop-tightness
 (largest box side fraction), product-aspect-ratio (box pixel aspect), vertical/horizontal
 centering (1 at dead center, 0 at the edge), and product-coverage-ratio — a box-area
 approximation until a segmentation model (yolo26s-seg) provides pixel masks.
*/

/// <summary>
/// Measures subject-box geometry features. Returns the resolved subject box so the color
/// analyzers can sample the same region.
/// </summary>
public static class Analyzer_SubjectGeometry
{
    /// <summary>
    /// Thresholds for Analyzer_SubjectGeometry, bound from the "SubjectGeometry" section of
    /// analyzer_Config.json. No defaults — every value must be present in the JSON or deserialization
    /// fails loud.
    /// </summary>
    public sealed class Config : IValidatableConfig
    {
        /// <summary>Euclidean RGB distance ([0,1] channels) from the background estimate above which a pixel counts as foreground.</summary>
        public required float ForegroundColorDistance { get; init; }

        /// <summary>Minimum foreground pixel fraction for the fallback box to be trusted.</summary>
        public required float MinForegroundFraction { get; init; }

        /// <summary>Confidence recorded on features measured from the color-distance fallback box (YOLO boxes carry the detection confidence).</summary>
        public required float FallbackConfidence { get; init; }

        /// <summary>
        /// Confidence discount applied to product-coverage-ratio: box-area coverage is an
        /// approximation until a segmentation model provides pixel masks, see T-2600.
        /// </summary>
        public required float BoxAreaCoverageConfidenceDiscount { get; init; }

        public void Validate()
        {
            if (this.ForegroundColorDistance is <= 0f or >= 1f)
                throw new PrismConfigurationException("SubjectGeometry.ForegroundColorDistance must be in (0,1)");
        }
    }

    public static SubjectBox? Analyze(Image<Rgba32> image, IReadOnlyList<YoloDetection> detections, ImageFeatureSnapshot snapshot, Config cfg)
    {
        SubjectBox? subject = ResolveSubjectBox(image, detections, cfg);
        if (subject is null) return null;

        double conf = subject.Confidence;
        string source = subject.Source;
        float imageAspect = image.Width / (float)image.Height;
        float boxAspect = subject.Height <= 0f ? 0f : subject.Width * imageAspect / subject.Height;

        snapshot.Set("salient-bbox", FormatBox(subject), conf, source);
        snapshot.Set("image-occupancy", F4(subject.Area), conf, source);
        snapshot.Set("product-coverage-ratio", F4(subject.Area), conf * cfg.BoxAreaCoverageConfidenceDiscount, source);
        snapshot.Set("crop-tightness", F4(MathF.Max(subject.Width, subject.Height)), conf, source);
        snapshot.Set("product-aspect-ratio", F4(boxAspect), conf, source);
        // 2f/0.5f: distance-from-center formula (1 - 2*|x-0.5|) — the [0,1] axis's own midline, structural.
#pragma warning disable S109
        snapshot.Set("vertical-centering", F4(1f - 2f * MathF.Abs(subject.CenterY - 0.5f)), conf, source);
        snapshot.Set("horizontal-centering", F4(1f - 2f * MathF.Abs(subject.CenterX - 0.5f)), conf, source);
#pragma warning restore S109

        return subject;
    }

    private static SubjectBox? ResolveSubjectBox(Image<Rgba32> image, IReadOnlyList<YoloDetection> detections, Config cfg)
    {
        YoloDetection? best = detections.OrderByDescending(d => d.Confidence).FirstOrDefault();
        if (best is not null)
            return new SubjectBox(best.X1, best.Y1, best.X2, best.Y2, best.Confidence, "yolo");

        (float r, float g, float b) = AnalyzerMath.EstimateBackgroundColor(image);
        return AnalyzerMath.ComputeForegroundBox(image, (r, g, b), cfg.ForegroundColorDistance, cfg.MinForegroundFraction, cfg.FallbackConfidence);
    }

    private static string FormatBox(SubjectBox box)
        => $"{F4(box.X1)},{F4(box.Y1)},{F4(box.X2)},{F4(box.Y2)}";

    private static string F4(float value)
        => value.ToString("F4", CultureInfo.InvariantCulture);
}
