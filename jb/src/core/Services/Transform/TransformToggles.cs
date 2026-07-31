namespace Prism.Services.Transform;

/// <summary>
/// The three behaviour toggles the Transformed stage steers on (T-4860), computed from the resolved
/// Excel + CLIP seeding (<see cref="TransformSeed"/>) and the detector result. Pure decisions — no
/// pixel work here. Recorded as transform evidence (T-4870).
/// <list type="bullet">
/// <item><see cref="ProductNearBackground"/>: product colour ≈ background colour — isolation is ambiguous.</item>
/// <item><see cref="NonFlatBackground"/>: background is not measured as a solid sweep — SOLIDCOLOR is the
/// only flat case, so REALLIFE and UNKNOWN/absent both count as non-flat — hero detection is harder.</item>
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

    public static TransformToggles Resolve(TransformSeed? seed, SubjectDetectionResult? subject) {
        bool near = seed?.EffectiveProductColor is { } productColor && seed.BackgroundColor is { } backgroundColor
                    && string.Equals(productColor, backgroundColor, StringComparison.OrdinalIgnoreCase);
        // T-4860: only a measured SOLIDCOLOR background is flat. UNKNOWN/absent is not known to be flat,
        // so it must not read the same as SOLIDCOLOR — collapse everything that isn't confirmed flat to true.
        bool nonFlat = seed?.IsBackgroundFlat != true;
        bool shadow = subject?.HasHardShadowEvidence == true;
        return new TransformToggles(near, nonFlat, shadow);
    }
}
