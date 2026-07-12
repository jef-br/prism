using Prism.Config;

namespace Prism.Services.Matching;

/// <summary>
/// The analyzer chain's parameter bundle: the analyzer_Config.json sections, already loaded and
/// validated, composed into one value that <see cref="ImageFeatureAnalyzer"/> unpacks per analyzer.
/// Not a config loader and not a deserialization target — <see cref="FromConfig"/> pulls each section
/// independently through <see cref="ConfigLoader"/> (each one self-validating), then bundles the
/// results. Built once at service construction, never per image.
/// </summary>
public sealed class AnalyzerParameters
{
    internal const string ConfigFile = "analyzer_Config.json";

    public required InteriorAnalyzerConfig Interior { get; init; }
    public required IllustrationAnalyzerConfig IsIllustration { get; init; }
    public required YoloAnalyzerConfig Yolo { get; init; }
    public required FilenameAnalyzerConfig Filename { get; init; }
    public required SubjectGeometryAnalyzerConfig SubjectGeometry { get; init; }
    public required ColorAnalyzerConfig Colors { get; init; }
    public required ExposureAnalyzerConfig Exposure { get; init; }
    public required MultipleProductsAnalyzerConfig MultipleProducts { get; init; }

    /// <summary>
    /// Loads every analyzer_Config.json section, then composes them. A missing file, a misspelled key,
    /// or an out-of-range value throws here — so calling this at host startup fails the process loud
    /// rather than failing the first image mid-job.
    /// </summary>
    public static AnalyzerParameters FromConfig() => new()
    {
        Interior         = ConfigLoader.Section<InteriorAnalyzerConfig>(ConfigFile, "Interior"),
        IsIllustration   = ConfigLoader.Section<IllustrationAnalyzerConfig>(ConfigFile, "IsIllustration"),
        Yolo             = ConfigLoader.Section<YoloAnalyzerConfig>(ConfigFile, "Yolo"),
        Filename         = ConfigLoader.Section<FilenameAnalyzerConfig>(ConfigFile, "Filename"),
        SubjectGeometry  = ConfigLoader.Section<SubjectGeometryAnalyzerConfig>(ConfigFile, "SubjectGeometry"),
        Colors           = ConfigLoader.Section<ColorAnalyzerConfig>(ConfigFile, "Colors"),
        Exposure         = ConfigLoader.Section<ExposureAnalyzerConfig>(ConfigFile, "Exposure"),
        MultipleProducts = ConfigLoader.Section<MultipleProductsAnalyzerConfig>(ConfigFile, "MultipleProducts")
    };
}
