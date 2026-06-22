namespace Prism.Core;

/// <summary>
/// In-process FeatureAnalysis implementation. Delegates to <see cref="ImageFeatureAnalyzer"/> — CPU-only,
/// no model assets. Internal to Matching.
/// </summary>
public sealed class FeatureAnalysisService : IFeatureAnalysisService
{
    /// <inheritdoc/>
    public void Analyze(string normalizedJpgPath, ImageFeatureSnapshot target)
        => ImageFeatureAnalyzer.Analyze(normalizedJpgPath, target);
}
