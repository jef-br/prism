namespace Prism.Services.Matching;

/// <summary>
/// Tuning values for the classical-CV <see cref="SubjectDetector"/>, bound from the "SubjectDetector"
/// section of ClassifyConfig.json. No defaults — every value must be present in the JSON or fail loud.
/// </summary>
public sealed class SubjectDetectorConfig : IValidatableConfig {
    // BorderRingFraction's upper bound: at 0.5 the top/bottom (or left/right) bands would meet in the
    // middle, leaving no interior — validation bound.
    private const double BorderRingFractionUpperBound = 0.5;

    // Longest side (px) of the analysis image. The detector downscales to this; the mask/box are then
    // rescaled back to the original. Larger preserves fine texture (fabric weave) but costs time.
    public required int MaxAnalysisSize { get; init; }

    // Local-standard-deviation window (px) that measures surface texture.
    public required int TextureWindow { get; init; }

    // High-pass sigma: anything varying more slowly than this is not surface texture (strips shadow penumbra).
    public required double TextureDetailSigma { get; init; }

    // Robust-spread multiples above the border-ring background that count as product.
    public required double OutlierSpreadMultiplier { get; init; }

    // Minimum blob area to keep, as a fraction of image area.
    public required double MinComponentAreaFraction { get; init; }

    // Minimum blob area to keep, as a fraction of the largest blob.
    public required double MinComponentAreaRatio { get; init; }

    // Absolute minimum blob area (px).
    public required double MinComponentAreaPixels { get; init; }

    // A box covering this much of the frame counts as "no detection" (whole frame is the subject).
    public required double WholeFrameFraction { get; init; }

    // Morphological-open size that strips a hard shadow's thin edge from texture-only detection.
    public required int ShadowEdgeKernel { get; init; }

    // Auto-Canny threshold width around the median gradient.
    public required double CannySigma { get; init; }

    // Gap-closing size applied to the Canny edge map before border flood-fill.
    public required int CannyCloseKernel { get; init; }

    // Thin border band, as a fraction of each side, assumed to be background.
    public required double BorderRingFraction { get; init; }

    // Absolute floor on the chroma-distance threshold (Lab units).
    public required double ChromaFloor { get; init; }

    // Absolute floor on the texture threshold.
    public required double TextureFloor { get; init; }

    // CLAHE clip limit / tile size used in throwaway detection preprocessing (bbox accuracy only).
    public required double ClaheClipLimit { get; init; }
    public required int ClaheTileSize { get; init; }

    // Fraction of a canvas edge that must be product for that edge to count as intersected.
    public required double BleedContact { get; init; }

    // Fraction of the mask that must be stripped-thin-line (candidate shadow) for hard-shadow evidence.
    public required double HardShadowEvidenceFraction { get; init; }

    // Config for the non-flat background splitter: case B1: studio sweep, case B2: real-life scene
    // (Mean absolute residual of the border-ring background plane fit.) JB: HIGHLY EXPERIMENTAL
    public required double RealLifeResidualThreshold { get; init; }

    // B1 : morphological-open size (like dust & scratches)
    public required int StudioSweepSpeckleKernel { get; init; }

    // B2 : HeroDetectionOnSteroids, override MaxAnalysisSize and analyze image on high res
    // A busy backdrop benefits from detail a 'flat' studio background does not.
    public required int RealLifeAnalysisSize { get; init; }

    // B2: Stricter minimum blob size (fraction of the largest blob). A real-life scene throws off far more
    // spurious chroma outliers than a sweep, so the bar for "this blob is part of the product" goes up.
    public required double RealLifeMinComponentAreaRatio { get; init; }

    public void Validate() {
        List<string> problems = [];
        this.CollectCoreProblems(problems);
        this.CollectSeededProblems(problems);
        if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
    }

    private void CollectCoreProblems(List<string> problems) {
        if (this.MaxAnalysisSize < 1) problems.Add("SubjectDetector.MaxAnalysisSize must be >= 1");
        if (this.TextureWindow < 1) problems.Add("SubjectDetector.TextureWindow must be >= 1");
        if (this.TextureDetailSigma <= 0.0) problems.Add("SubjectDetector.TextureDetailSigma must be > 0");
        if (this.OutlierSpreadMultiplier <= 0.0) problems.Add("SubjectDetector.OutlierSpreadMultiplier must be > 0");
        if (this.MinComponentAreaFraction is <= 0.0 or >= 1.0) problems.Add("SubjectDetector.MinComponentAreaFraction must be in (0,1)");
        if (this.MinComponentAreaRatio is <= 0.0 or >= 1.0) problems.Add("SubjectDetector.MinComponentAreaRatio must be in (0,1)");
        if (this.MinComponentAreaPixels <= 0.0) problems.Add("SubjectDetector.MinComponentAreaPixels must be > 0");
        if (this.WholeFrameFraction is <= 0.0 or > 1.0) problems.Add("SubjectDetector.WholeFrameFraction must be in (0,1]");
        if (this.ShadowEdgeKernel < 1) problems.Add("SubjectDetector.ShadowEdgeKernel must be >= 1");
        if (this.CannySigma <= 0.0) problems.Add("SubjectDetector.CannySigma must be > 0");
        if (this.CannyCloseKernel < 1) problems.Add("SubjectDetector.CannyCloseKernel must be >= 1");
        if (this.BorderRingFraction is <= 0.0 or >= BorderRingFractionUpperBound) problems.Add("SubjectDetector.BorderRingFraction must be in (0,0.5)");
        if (this.ChromaFloor <= 0.0) problems.Add("SubjectDetector.ChromaFloor must be > 0");
        if (this.TextureFloor <= 0.0) problems.Add("SubjectDetector.TextureFloor must be > 0");
        if (this.ClaheClipLimit <= 0.0) problems.Add("SubjectDetector.ClaheClipLimit must be > 0");
        if (this.ClaheTileSize < 1) problems.Add("SubjectDetector.ClaheTileSize must be >= 1");
        if (this.BleedContact is <= 0.0 or >= 1.0) problems.Add("SubjectDetector.BleedContact must be in (0,1)");
        if (this.HardShadowEvidenceFraction is <= 0.0 or >= 1.0) problems.Add("SubjectDetector.HardShadowEvidenceFraction must be in (0,1)");
    }

    private void CollectSeededProblems(List<string> problems) {
        if (this.RealLifeResidualThreshold <= 0.0) problems.Add("SubjectDetector.RealLifeResidualThreshold must be > 0");
        if (this.StudioSweepSpeckleKernel < 1) problems.Add("SubjectDetector.StudioSweepSpeckleKernel must be >= 1");
        if (this.RealLifeAnalysisSize < 1) problems.Add("SubjectDetector.RealLifeAnalysisSize must be >= 1");
        if (this.RealLifeMinComponentAreaRatio is <= 0.0 or >= 1.0) problems.Add("SubjectDetector.RealLifeMinComponentAreaRatio must be in (0,1)");
    }
}
