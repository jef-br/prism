namespace Prism.Services.Matching;

/// <summary>
/// Sets the <c>has-human</c>, <c>human-count</c>, and <c>hero-is-human</c> ImageFeatures from
/// yolo26s person detections. Replaces the retired HSV skin-ratio heuristic: a person detection
/// is direct evidence, robust on any background and unaffected by skin-colored products.
/// hero-is-human is derived from dominance — a person box covering enough of the frame means the
/// human wearing the product is the hero; no person at all means the hero cannot be human.
/// Detection absence is weaker evidence than presence, so absence writes use the configured
/// absence confidence, and a stronger existing measurement (e.g. CLIP) is never overwritten.
/// </summary>
internal static class Analyzer_HasHuman {
    public static void Analyze(IReadOnlyList<YoloDetection> detections, ImageFeatureSnapshot snapshot, YoloAnalyzerConfig cfg, bool aiDetectionEnabled) {
        // AI detection is off — the "I don't know" default stands. Unlike Analyzer_SubjectGeometry, an
        // empty detection list here is a confident measurement ("YOLO looked and found nobody"), so it
        // cannot double as "YOLO never ran"; only the toggle distinguishes the two. Room here for a
        // manually-authored has-human fallback measurement later; none exists today.
        if (!aiDetectionEnabled) return;

        List<YoloDetection> persons = [.. detections.Where(d => d.IsPerson && d.Confidence >= cfg.HumanMinConfidence)];

        if (persons.Count > 0) {
            float best = persons.Max(d => d.Confidence);
            snapshot.Set("has-human", "true", best, "yolo");
            snapshot.Set("human-count", persons.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), best, "yolo");

            if (persons.Max(d => d.Area) >= cfg.HeroPersonMinArea)
                SetIfStronger(snapshot, "hero-is-human", "TRUE", best);
        }
        else {
            snapshot.Set("has-human", "false", cfg.AbsenceConfidence, "yolo");
            snapshot.Set("human-count", "0", cfg.AbsenceConfidence, "yolo");
            SetIfStronger(snapshot, "hero-is-human", "FALSE", cfg.AbsenceConfidence);
        }
    }

    private static void SetIfStronger(ImageFeatureSnapshot snapshot, string featureId, string value, double confidence) {
        bool weaker = !snapshot.TryGet(featureId, out ImageFeatureValue? current)
            || current.IsUnknown
            || current.Confidence < confidence;

        if (weaker) snapshot.Set(featureId, value, confidence, "yolo");
    }
}
