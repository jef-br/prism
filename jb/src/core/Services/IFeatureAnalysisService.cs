namespace Prism.Core;

/// <summary>
/// Internal to Matching — not visible to the orchestrator. CPU-only geometric and visual feature
/// extraction (geometry, borders, background, occlusion, skin-tone) written into the image's
/// <see cref="ImageFeatureSnapshot"/>.
/// </summary>
public interface IFeatureAnalysisService
{
    /// <summary>Measures features for the normalized JPEG and records them on <paramref name="target"/>.</summary>
    void Analyze(string normalizedJpgPath, ImageFeatureSnapshot target);
}
