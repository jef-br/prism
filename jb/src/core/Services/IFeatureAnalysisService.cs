using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/// <summary>
/// Internal to Matching — not visible to the orchestrator. CPU-only geometric and visual feature
/// extraction (geometry, borders, background, occlusion, skin-tone) written into the image's
/// <see cref="ImageFeatureSnapshot"/>.
/// </summary>
public interface IFeatureAnalysisService
{
    /// <summary>Measures features for the pre-loaded image and records them on <paramref name="target"/>.</summary>
    void Analyze(Image<Rgba32> image, ImageFeatureSnapshot target);
}
