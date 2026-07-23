using System.Globalization;

namespace Prism.Services.Matching;

/*
 Proposed workings
 -----------------
 YOLO detections carry the object count directly: multiple-products is true when more than
 one non-person detection survives NMS; overlap-count is the number of non-person detection
 pairs whose boxes overlap above the configured IoU. No detections at all → both features
 stay UNKNOWN (many PRISM products fall outside the 80 COCO classes, so absence of
 detections is not evidence of a single product).
*/

/// <summary>
/// Sets <c>multiple-products</c> and <c>overlap-count</c> from YOLO detections.
/// </summary>
public static class Analyzer_MultipleProducts
{
    /// <summary>
    /// Thresholds for Analyzer_MultipleProducts, bound from the "MultipleProducts" section of
    /// analyzer_Config.json. No defaults — every value must be present in the JSON or deserialization
    /// fails loud.
    /// </summary>
    public sealed class Config : IValidatableConfig
    {
        /// <summary>IoU above which two non-person detections count as overlapping.</summary>
        public required float OverlapIou { get; init; }

        /// <summary>Confidence written on multiple-products/overlap-count.</summary>
        public required float Confidence { get; init; }

        public void Validate()
        {
            if (OverlapIou is <= 0f or >= 1f)
                throw new PrismConfigurationException("MultipleProducts.OverlapIou must be in (0,1)");
        }
    }

    public static void Analyze(IReadOnlyList<YoloDetection> detections, ImageFeatureSnapshot snapshot, Config cfg)
    {
        List<YoloDetection> objects = [.. detections.Where(d => !d.IsPerson)];
        if (objects.Count == 0) return;

        int overlaps = 0;
        for (int i = 0; i < objects.Count; i++)
        {
            for (int j = i + 1; j < objects.Count; j++)
            {
                if (IntersectionOverUnion(objects[i], objects[j]) > cfg.OverlapIou) overlaps++;
            }
        }

        snapshot.Set("multiple-products", objects.Count > 1 ? "true" : "false", cfg.Confidence, "yolo");
        snapshot.Set("overlap-count", overlaps.ToString(CultureInfo.InvariantCulture), cfg.Confidence, "yolo");
    }

    private static float IntersectionOverUnion(YoloDetection a, YoloDetection b)
    {
        float ix = MathF.Max(0f, MathF.Min(a.X2, b.X2) - MathF.Max(a.X1, b.X1));
        float iy = MathF.Max(0f, MathF.Min(a.Y2, b.Y2) - MathF.Max(a.Y1, b.Y1));
        float inter = ix * iy;
        float union = a.Area + b.Area - inter;
        return union <= 0f ? 0f : inter / union;
    }
}
