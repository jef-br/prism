namespace Prism.Core;

/// <summary>
/// Crop-sizing and positioning values consumed by <see cref="ImageTransformer"/> when selecting
/// and constructing a transform strategy. Mirrors fields already loaded onto <c>PrismConfiguration</c>
/// (<c>Transformation.Positioning.Margin</c> and <c>Transformation.Cropping.*</c>), passed as a small
/// typed value here rather than the whole config object because <c>PrismConfiguration</c> lives in
/// <c>Prism.Core</c>, a project that itself depends on <c>Prism.Core.Images.Transform</c> — the
/// dependency cannot run the other way without a circular project reference.
/// </summary>
public readonly record struct CropTransformSettings(
    double WhiteSpaceMargin,
    double CropCoverage,
    double CropExtensionOneSided,
    double CropExtensionBiDirectional);
