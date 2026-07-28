namespace Prism.Services.Transform;

/// <summary>
/// The three behaviour toggles the Transformed stage steers on (T-4860), computed from the resolved
/// Excel + CLIP seeding (<see cref="TransformSeed"/>) and the detector result. Pure decisions — no
/// pixel work here. Recorded as transform evidence (T-4870).
/// <list type="bullet">
/// <item><see cref="ProductNearBackground"/>: product colour ≈ background colour — isolation is ambiguous.</item>
/// <item><see cref="NonFlatBackground"/>: measured background is not a solid sweep — hero detection is harder.</item>
/// <item><see cref="ShadowAccounting"/>: the detector found hard-shadow evidence — account for a cast shadow.</item>
/// </list>
/// </summary>
public sealed class TransformToggles {
    public bool ProductNearBackground { get; }
    public bool NonFlatBackground { get; }
    public bool ShadowAccounting { get; }

    private TransformToggles(bool productNearBackground, bool nonFlatBackground, bool shadowAccounting) {
        this.ProductNearBackground = productNearBackground;
        this.NonFlatBackground = nonFlatBackground;
        this.ShadowAccounting = shadowAccounting;
    }

    public static TransformToggles Resolve(TransformSeed? seed, SubjectDetection? subject) {
        bool near = seed?.EffectiveProductColor is { } productColor && seed.BackgroundColor is { } backgroundColor
                    && string.Equals(productColor, backgroundColor, StringComparison.OrdinalIgnoreCase);
        bool nonFlat = seed is { BackgroundType: not null, IsBackgroundFlat: false };
        bool shadow = subject?.HasHardShadowEvidence == true;
        return new TransformToggles(near, nonFlat, shadow);
    }
}
