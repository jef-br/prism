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
 approximation until a segmentation model (yolov8n-seg) provides pixel masks.
*/

/// <summary>
/// Measures subject-box geometry features. Returns the resolved subject box so the color
/// analyzers can sample the same region.
/// </summary>
internal static class Analyzer_SubjectGeometry
{
    public static SubjectBox? Analyze(Image<Rgba32> image, IReadOnlyList<YoloDetection> detections, ImageFeatureSnapshot snapshot, SubjectGeometryAnalyzerConfig cfg)
    {
        SubjectBox? subject = ResolveSubjectBox(image, detections, cfg);
        if (subject is null) return null;

        double conf = subject.Confidence;
        string source = subject.Source;
        float imageAspect = image.Width / (float)image.Height;
        float boxAspect = subject.Height <= 0f ? 0f : subject.Width * imageAspect / subject.Height;

        snapshot.Set("salient-bbox", FormatBox(subject), conf, source);
        snapshot.Set("image-occupancy", F4(subject.Area), conf, source);
        snapshot.Set("product-coverage-ratio", F4(subject.Area), conf * 0.9, source);
        snapshot.Set("crop-tightness", F4(MathF.Max(subject.Width, subject.Height)), conf, source);
        snapshot.Set("product-aspect-ratio", F4(boxAspect), conf, source);
        snapshot.Set("vertical-centering", F4(1f - 2f * MathF.Abs(subject.CenterY - 0.5f)), conf, source);
        snapshot.Set("horizontal-centering", F4(1f - 2f * MathF.Abs(subject.CenterX - 0.5f)), conf, source);

        return subject;
    }

    private static SubjectBox? ResolveSubjectBox(Image<Rgba32> image, IReadOnlyList<YoloDetection> detections, SubjectGeometryAnalyzerConfig cfg)
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
