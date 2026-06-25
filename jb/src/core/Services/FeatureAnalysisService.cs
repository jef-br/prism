using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Prism.Core;

/// <summary>
/// In-process FeatureAnalysis implementation. Delegates to <see cref="ImageFeatureAnalyzer"/> — CPU-only,
/// no model assets. Internal to Matching.
/// </summary>
public sealed class FeatureAnalysisService : IFeatureAnalysisService
{
    /// <inheritdoc/>
    public void Analyze(Image<Rgba32> image, ImageFeatureSnapshot target)
        => ImageFeatureAnalyzer.Analyze(image, target);
}
