namespace Prism.Services.Matching;

/// <summary>
/// The Classify chain's parameter bundle: the ClassifyConfig.json sections owned by this project,
/// already loaded and validated. Not a config loader and not a deserialization target — FromConfig
/// pulls each section independently through ConfigLoader (each one self-validating), then bundles the
/// results. Built once at service construction, never per image.
/// ImagePreProcessor.Config is deliberately excluded: ImagePreProcessor.cs compiles into Prism.Core,
/// which this project does not reference, so it loads its ClassifyConfig.json section directly instead
/// of riding this bundle.
/// </summary>
public sealed class ClassifyParameters {
    internal const string ConfigFile = "ClassifyConfig.json";

    public required ImageFeatureAnalyzer.Config ImageFeatureAnalyzer { get; init; }
    public required SubjectEdgeDetector.Config SubjectEdgeDetector { get; init; }
    public required VisualHasher.Config VisualHasher { get; init; }

    /// <summary>
    /// Loads every ClassifyConfig.json section this bundle owns, then composes them. A missing file, a
    /// misspelled key, or an out-of-range value throws here — so calling this at host startup fails the
    /// process loud rather than failing the first image mid-job.
    /// </summary>
    public static ClassifyParameters FromConfig() => new() {
        ImageFeatureAnalyzer = ConfigLoader.Section<ImageFeatureAnalyzer.Config>(ConfigFile, "ImageFeatureAnalyzer"),
        SubjectEdgeDetector = ConfigLoader.Section<SubjectEdgeDetector.Config>(ConfigFile, "SubjectEdgeDetector"),
        VisualHasher = ConfigLoader.Section<VisualHasher.Config>(ConfigFile, "VisualHasher")
    };
}
