namespace Prism.Services.Transform;

/// <summary>
/// Read-model surfacing the Excel + CLIP seeding signals the Transformed stage steers on, resolved
/// once per image from the LAMBDA feature snapshot and the matched FamilyIDRecord — so the transform
/// strategies never recompute them. product = Excel + CLIP (Excel authoritative); background = CLIP.
/// The three behaviour toggles (T-4860) read these; this type only exposes them (T-4820). Absent /
/// UNKNOWN signals are normalised to null so consumers test a single "missing" case.
/// </summary>
public sealed class TransformSeed {
    public string? ProductTypeId { get; }
    public string? ClipProductColor { get; }
    public string? ExcelProductColor { get; }
    public string? BackgroundType { get; }
    public string? BackgroundColor { get; }

    // Excel is authoritative for product colour; CLIP fills in when Excel is silent.
    public string? EffectiveProductColor => this.ExcelProductColor ?? this.ClipProductColor;

    // Background is flat when CLIP measured a solid-colour backdrop (the only "flat" background-type).
    public bool IsBackgroundFlat => string.Equals(this.BackgroundType, "SOLIDCOLOR", StringComparison.OrdinalIgnoreCase);

    private TransformSeed(string? productTypeId, string? clipProductColor, string? excelProductColor, string? backgroundType, string? backgroundColor) {
        this.ProductTypeId = productTypeId;
        this.ClipProductColor = clipProductColor;
        this.ExcelProductColor = excelProductColor;
        this.BackgroundType = backgroundType;
        this.BackgroundColor = backgroundColor;
    }

    public static TransformSeed Resolve(ImageRecord_LAMBDA lambda, FamilyIDRecord? family) {
        return new TransformSeed(
            NullIfAbsent(lambda.ProductTypeId),
            FeatureOrNull(lambda.Features, "product-color"),
            ExcelOrNull(family, "productcolor"),
            FeatureOrNull(lambda.Features, "background-type"),
            FeatureOrNull(lambda.Features, "background-color"));
    }

    private static string? FeatureOrNull(ImageFeatureSnapshot features, string id) => NullIfAbsent(features.GetValue(id));

    private static string? ExcelOrNull(FamilyIDRecord? family, string key) =>
        family is not null && family.CanonicalProperties.TryGetValue(key, out string? value) ? NullIfAbsent(value) : null;

    // ImageFeatureSnapshot.GetValue returns "UNKNOWN" for an unmeasured feature; treat that and blanks as absent.
    private static string? NullIfAbsent(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ? null : value;
}
