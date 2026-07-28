namespace Prism.Services.Matching;

/// <summary>
/// Excel + CLIP signals handed to <see cref="SubjectDetector"/> so detection can steer itself before it
/// runs (T-4860 toggles a and b). Resolved in the refinement chain, where the FamilyIDRecord is known and
/// the colour/background analyzers have just measured their features. Deliberately separate from the
/// Transform-stage TransformSeed: this is the Matching-side read-model and must not make Matching depend
/// on Transform. Absent / UNKNOWN signals normalise to null so the detector tests one "missing" case.
/// </summary>
public sealed class SubjectSeedHint {
    public string? EffectiveProductColor { get; }
    public string? BackgroundColor { get; }
    public string? BackgroundType { get; }

    // Product and background read as the same colour, so chroma separation is weak and the subject has to
    // be found on texture instead. This is what earns CLAHE its cost — see IsClaheWorthwhile.
    public bool ProductNearBackground =>
        this.EffectiveProductColor is not null
        && this.BackgroundColor is not null
        && string.Equals(this.EffectiveProductColor, this.BackgroundColor, StringComparison.OrdinalIgnoreCase);

    // SOLIDCOLOR is the only value that means "flat". UNKNOWN is explicitly not flat: an unmeasured
    // background is not a known-simple one, and treating it as flat would skip effort we cannot justify
    // skipping (the inversion the T-4860 review caught on the Transform side).
    public bool IsBackgroundFlat => string.Equals(this.BackgroundType, "SOLIDCOLOR", StringComparison.OrdinalIgnoreCase);

    private SubjectSeedHint(string? effectiveProductColor, string? backgroundColor, string? backgroundType) {
        this.EffectiveProductColor = effectiveProductColor;
        this.BackgroundColor = backgroundColor;
        this.BackgroundType = backgroundType;
    }

    public static SubjectSeedHint Resolve(ImageFeatureSnapshot features, FamilyIDRecord? family) {
        string? excelColor = ExcelOrNull(family, "productcolor");
        string? clipColor = NullIfAbsent(features.GetValue("product-color"));
        return new SubjectSeedHint(
            excelColor ?? clipColor,
            NullIfAbsent(features.GetValue("background-color")),
            NullIfAbsent(features.GetValue("background-type")));
    }

    private static string? ExcelOrNull(FamilyIDRecord? family, string key) =>
        family is not null && family.CanonicalProperties.TryGetValue(key, out string? value) ? NullIfAbsent(value) : null;

    // ImageFeatureSnapshot.GetValue returns "UNKNOWN" for an unmeasured feature; treat that and blanks as absent.
    private static string? NullIfAbsent(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ? null : value;
}
