using Prism.Config;

namespace Prism.Services.Transform;

/// <summary>
/// The Transform stage's parameter bundle: the transform_Config.json sections, already loaded and
/// validated, composed into one value that <see cref="ImageTransformer"/> and the Tx_ strategies read
/// from. Not a config loader and not a deserialization target — <see cref="FromConfig"/> pulls each
/// section independently through <see cref="ConfigLoader"/> (each one self-validating), then bundles
/// the results. Callers build it once per stage run, never per image.
/// </summary>
public sealed class TransformParameters
{
    internal const string ConfigFile = "transform_Config.json";

    public required CropTransformSettings Crop { get; init; }
    public required ProblemImageProcessorConfig ProblemImageProcessor { get; init; }
    public required BgStretchConfig BgStretch { get; init; }
    public required DetailCropperConfig DetailCropper { get; init; }
    public required LowContrastEnhancementConfig LowContrastEnhancement { get; init; }
    public required HeadCutterConfig HeadCutter { get; init; }
    public required OutputConfig Output { get; init; }

    /// <summary>
    /// Loads every transform_Config.json section, then composes them. A missing file, a misspelled
    /// key, or an out-of-range value throws here — so calling this at host startup fails the process
    /// loud rather than failing the first job mid-flight.
    /// </summary>
    public static TransformParameters FromConfig() => new()
    {
        Crop                  = ConfigLoader.Section<CropTransformSettings>(ConfigFile, "Crop"),
        ProblemImageProcessor = ConfigLoader.Section<ProblemImageProcessorConfig>(ConfigFile, "ProblemImageProcessor"),
        BgStretch             = ConfigLoader.Section<BgStretchConfig>(ConfigFile, "BgStretch"),
        DetailCropper         = ConfigLoader.Section<DetailCropperConfig>(ConfigFile, "DetailCropper"),
        LowContrastEnhancement = ConfigLoader.Section<LowContrastEnhancementConfig>(ConfigFile, "LowContrastEnhancement"),
        HeadCutter            = ConfigLoader.Section<HeadCutterConfig>(ConfigFile, "HeadCutter"),
        Output                = ConfigLoader.Section<OutputConfig>(ConfigFile, "Output")
    };
}
