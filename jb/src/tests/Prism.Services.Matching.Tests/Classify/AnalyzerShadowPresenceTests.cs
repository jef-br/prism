using Prism.Services.Matching;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Pins <c>shadow-present</c> publication, including the branch that must stay silent: an alpha-derived
/// detection measures geometry from a transparency channel, which carries no shadow information at all,
/// so publishing either verdict off it would be fabricating evidence.
/// </summary>
public class AnalyzerShadowPresenceTests {
    private static Analyzer_ShadowPresence.Config Config() =>
        ConfigLoader.Section<Analyzer_ShadowPresence.Config>("analyzer_Config.json", "ShadowPresence");

    private static SubjectDetectionResult Detection(string producer, bool hardShadow) => new() {
        Producer = producer,
        HasHardShadowEvidence = hardShadow,
        Confidence = 1.0
    };

    [Fact]
    public void ClassicalCvDetection_WithHardShadow_PublishesTrue() {
        ImageFeatureSnapshot snapshot = new();

        Analyzer_ShadowPresence.Analyze(Detection("classical-cv", hardShadow: true), snapshot, Config());

        Assert.Equal("true", snapshot.GetValue("shadow-present"));
    }

    [Fact]
    public void ClassicalCvDetection_WithoutHardShadow_PublishesFalse() {
        ImageFeatureSnapshot snapshot = new();

        Analyzer_ShadowPresence.Analyze(Detection("classical-cv", hardShadow: false), snapshot, Config());

        Assert.Equal("false", snapshot.GetValue("shadow-present"));
    }

    [Fact]
    public void AlphaDetection_LeavesFeatureUnset_NeverFabricatesEvidence() {
        // Alpha carries no shadow information. Publishing "false" here would look like a measurement and
        // would feed a fabricated signal into the Transform shadow toggle. Unset reads as UNKNOWN, which
        // is the honest answer: nobody looked.
        ImageFeatureSnapshot snapshot = new();

        Analyzer_ShadowPresence.Analyze(Detection("alpha", hardShadow: false), snapshot, Config());

        Assert.Equal("UNKNOWN", snapshot.GetValue("shadow-present"));
    }

    [Fact]
    public void NoDetection_LeavesFeatureUnset() {
        ImageFeatureSnapshot snapshot = new();

        Analyzer_ShadowPresence.Analyze(null, snapshot, Config());

        Assert.Equal("UNKNOWN", snapshot.GetValue("shadow-present"));
    }
}
