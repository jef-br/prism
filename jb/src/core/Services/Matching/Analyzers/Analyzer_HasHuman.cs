namespace Prism.Core;

/// <summary>
/// Sets the <c>has-human</c>, <c>human-count</c>, and <c>hero-is-human</c> ImageFeatures from
/// YOLOv8n person detections. Replaces the retired HSV skin-ratio heuristic: a person detection
/// is direct evidence, robust on any background and unaffected by skin-colored products.
/// hero-is-human is derived from dominance — a person box covering enough of the frame means the
/// human wearing the product is the hero; no person at all means the hero cannot be human.
/// Detection absence is weaker evidence than presence, so absence writes use the configured
/// absence confidence, and a stronger existing measurement (e.g. CLIP) is never overwritten.
/// </summary>
internal static class Analyzer_HasHuman
{
    public static void Analyze(IReadOnlyList<YoloDetection> detections, ImageFeatureSnapshot snapshot, YoloAnalyzerConfig cfg)
    {
        List<YoloDetection> persons = [.. detections.Where(d => d.IsPerson && d.Confidence >= cfg.HumanMinConfidence)];

        if (persons.Count > 0)
        {
            float best = persons.Max(d => d.Confidence);
            snapshot.Set("has-human", "true", best, "yolo");
            snapshot.Set("human-count", persons.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), best, "yolo");

            if (persons.Max(d => d.Area) >= cfg.HeroPersonMinArea)
                SetIfStronger(snapshot, "hero-is-human", "TRUE", best);
        }
        else
        {
            snapshot.Set("has-human", "false", cfg.AbsenceConfidence, "yolo");
            snapshot.Set("human-count", "0", cfg.AbsenceConfidence, "yolo");
            SetIfStronger(snapshot, "hero-is-human", "FALSE", cfg.AbsenceConfidence);
        }
    }

    private static void SetIfStronger(ImageFeatureSnapshot snapshot, string featureId, string value, double confidence)
    {
        bool weaker = !snapshot.TryGet(featureId, out ImageFeatureValue? current)
            || current.IsUnknown
            || current.Confidence < confidence;

        if (weaker) snapshot.Set(featureId, value, confidence, "yolo");
    }
}
