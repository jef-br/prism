using Prism.Services.Matching;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Pins <c>shadow-present</c> publication from the subject detector's hard-shadow evidence.
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
    public void NoDetection_LeavesFeatureUnset() {
        ImageFeatureSnapshot snapshot = new();

        Analyzer_ShadowPresence.Analyze(null, snapshot, Config());

        Assert.Equal("UNKNOWN", snapshot.GetValue("shadow-present"));
    }
}
