namespace Prism.Services.Matching;

/*
 Proposed workings
 -----------------
 The subject detector already separates a cast shadow from the product: it keys on chroma and
 texture and never on lightness, and the thin, texture-only, chroma-unsupported lines it strips
 by shape ARE the hard-shadow signature. That stripped fraction is measured during detection and
 carried on SubjectDetection.HasHardShadowEvidence. This analyzer only publishes that measurement
 as a feature — it re-measures nothing.

 No detection (no subject on the record) leaves the feature unset, which reads as UNKNOWN. That is
 correct and deliberate: "we never looked" is not the same claim as "there is no shadow", and a
 phenotype rule requiring shadow-present must not be satisfied by an image nobody measured.
*/

/// <summary>
/// Sets <c>shadow-present</c> from the subject detector's candidate-hard-shadow evidence.
/// </summary>
public static class Analyzer_ShadowPresence {
    /// <summary>
    /// Confidence for Analyzer_ShadowPresence, bound from the "ShadowPresence" section of
    /// analyzer_Config.json. No defaults — every value must be present in the JSON or deserialization
    /// fails loud.
    /// </summary>
    public sealed class Config : IValidatableConfig {
        /// <summary>Confidence written on shadow-present.</summary>
        public required float Confidence { get; init; }

        public void Validate() {
            if (this.Confidence is <= 0f or > 1f)
                throw new PrismConfigurationException("ShadowPresence.Confidence must be in (0,1]");
        }
    }

    public static void Analyze(SubjectDetection? subject, ImageFeatureSnapshot snapshot, Config cfg) {
        if (subject is null) return;

        // The alpha producer measures geometry from a transparency channel, which carries no shadow
        // information at all. Publishing "false" off an alpha detection would be inventing evidence.
        if (string.Equals(subject.Producer, "alpha", StringComparison.OrdinalIgnoreCase)) return;

        snapshot.Set("shadow-present", subject.HasHardShadowEvidence ? "true" : "false", cfg.Confidence, "subject-detector");
    }
}
