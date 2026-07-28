using System.Text.Json;
using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// T-4810: SubjectDetection travels on ImageRecord_LAMBDA across the matching→transform service
/// boundary. Guards that it round-trips through System.Text.Json intact — including the byte[] mask
/// (base64) and every flag — so a producer's result is not silently lost over HTTP.
/// </summary>
public class SubjectDetectionSerializationTests {
    [Fact]
    public void SubjectDetection_RoundTripsThroughSystemTextJson() {
        SubjectDetection original = new() {
            Box = new BoundingBox { X = 10, Y = 20, Width = 30, Height = 40, Left = 10, Top = 20, Right = 40, Bottom = 60 },
            MaskPng = [1, 2, 3, 4, 5],
            IntersectsTop = true,
            IntersectsRight = true,
            HasHardShadowEvidence = true,
            Confidence = 0.83,
            Producer = "classical-cv"
        };

        string json = JsonSerializer.Serialize(original);
        SubjectDetection? back = JsonSerializer.Deserialize<SubjectDetection>(json);

        Assert.NotNull(back);
        Assert.Equal(30, back!.Box.Width);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, back.MaskPng);
        Assert.True(back.IntersectsTop);
        Assert.True(back.IntersectsRight);
        Assert.False(back.IntersectsLeft);
        Assert.True(back.HasHardShadowEvidence);
        Assert.Equal(0.83, back.Confidence, 3);
        Assert.Equal("classical-cv", back.Producer);
        Assert.Equal(2, back.TouchedEdgeCount);
    }

    [Fact]
    public void SubjectDetection_SurvivesOnLambdaRecord() {
        ImageRecord_LAMBDA lambda = new() {
            InitialFullName = "img.jpg",
            Subject = new SubjectDetection { Producer = "alpha", Confidence = 1.0, MaskPng = [9, 9, 9] }
        };

        string json = JsonSerializer.Serialize(lambda);
        ImageRecord_LAMBDA? back = JsonSerializer.Deserialize<ImageRecord_LAMBDA>(json);

        Assert.NotNull(back?.Subject);
        Assert.Equal("alpha", back!.Subject!.Producer);
        Assert.Equal(new byte[] { 9, 9, 9 }, back.Subject.MaskPng);
    }
}
