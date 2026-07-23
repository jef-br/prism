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

    public required Analyzer_Interior.Config Interior { get; init; }
    public required Analyzer_IsIllustration.Config IsIllustration { get; init; }
    public required YoloAnalyzerConfig Yolo { get; init; }
    public required Analyzer_FilenameEvidence.Config Filename { get; init; }
    public required Analyzer_SubjectGeometry.Config SubjectGeometry { get; init; }
    public required ColorAnalyzerConfig Colors { get; init; }
    public required Analyzer_Exposure.Config Exposure { get; init; }
    public required Analyzer_MultipleProducts.Config MultipleProducts { get; init; }
    public required SkinToneAnalyzerConfig SkinTone { get; init; }

    /// <summary>
    /// Loads every analyzer_Config.json section, then composes them. A missing file, a misspelled key,
    /// or an out-of-range value throws here — so calling this at host startup fails the process loud
    /// rather than failing the first image mid-job.
    /// </summary>
    public static AnalyzerParameters FromConfig() => new()
    {
        Interior         = ConfigLoader.Section<Analyzer_Interior.Config>(ConfigFile, "Interior"),
        IsIllustration   = ConfigLoader.Section<Analyzer_IsIllustration.Config>(ConfigFile, "IsIllustration"),
        Yolo             = ConfigLoader.Section<YoloAnalyzerConfig>(ConfigFile, "Yolo"),
        Filename         = ConfigLoader.Section<Analyzer_FilenameEvidence.Config>(ConfigFile, "Filename"),
        SubjectGeometry  = ConfigLoader.Section<Analyzer_SubjectGeometry.Config>(ConfigFile, "SubjectGeometry"),
        Colors           = ConfigLoader.Section<ColorAnalyzerConfig>(ConfigFile, "Colors"),
        Exposure         = ConfigLoader.Section<Analyzer_Exposure.Config>(ConfigFile, "Exposure"),
        MultipleProducts = ConfigLoader.Section<Analyzer_MultipleProducts.Config>(ConfigFile, "MultipleProducts"),
        SkinTone         = ConfigLoader.Section<SkinToneAnalyzerConfig>(ConfigFile, "SkinTone")
    };
}
