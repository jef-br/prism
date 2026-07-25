using Xunit;

namespace PrismCoreTests.Upscale;

/// <summary>
/// Unit tests for the pure blend-weight math used by Upscaler.RunTiled to stitch tile outputs
/// without a hard seam: a discard band nearest each internal tile boundary, then a raised-cosine taper
/// up to full weight, while edges facing the true image border always carry full weight.
/// </summary>
public class UpscalerBlendTests {
    private const int OverlapOut = 16;
    private const int DiscardOut = 3;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RampFromEdge_WithinDiscardBand_ReturnsZero(int distFromEdge) {
        float weight = Upscaler.RampFromEdge(distFromEdge, OverlapOut, DiscardOut);

        Assert.Equal(0f, weight);
    }

    [Theory]
    [InlineData(OverlapOut)]
    [InlineData(OverlapOut + 1)]
    [InlineData(OverlapOut + 100)]
    public void RampFromEdge_AtOrBeyondOverlap_ReturnsOne(int distFromEdge) {
        float weight = Upscaler.RampFromEdge(distFromEdge, OverlapOut, DiscardOut);

        Assert.Equal(1f, weight);
    }

    [Fact]
    public void RampFromEdge_WithinRampZone_IsStrictlyBetweenZeroAndOneAndNonDecreasing() {
        float previous = 0f;
        for (int distFromEdge = DiscardOut; distFromEdge < OverlapOut; distFromEdge++) {
            float weight = Upscaler.RampFromEdge(distFromEdge, OverlapOut, DiscardOut);

            Assert.InRange(weight, 0f, 1f);
            Assert.True(weight >= previous, $"Expected non-decreasing ramp at distFromEdge={distFromEdge}.");
            previous = weight;
        }
    }

    [Fact]
    public void AxisWeight_BothEdgesOutward_AlwaysReturnsOne() {
        for (int pos = 0; pos < 50; pos++) {
            float weight = Upscaler.AxisWeight(pos, 50, OverlapOut, DiscardOut, startOutward: true, endOutward: true);

            Assert.Equal(1f, weight);
        }
    }

    [Fact]
    public void AxisWeight_NearSeamFacingStartEdgeWithinDiscardBand_ReturnsZero() {
        float weight = Upscaler.AxisWeight(0, 50, OverlapOut, DiscardOut, startOutward: false, endOutward: true);

        Assert.Equal(0f, weight);
    }

    [Fact]
    public void AxisWeight_DeepInterior_AwayFromBothSeamFacingEdges_ReturnsOne() {
        // 50-long tile, both edges seam-facing (no outward edge): position 25 is >= OverlapOut away from
        // both ends (25 and 24), so it sits in every neighboring tile's trusted interior.
        float weight = Upscaler.AxisWeight(25, 50, OverlapOut, DiscardOut, startOutward: false, endOutward: false);

        Assert.Equal(1f, weight);
    }

    [Fact]
    public void AxisWeight_NeverNegative() {
        for (int pos = 0; pos < 50; pos++) {
            float weight = Upscaler.AxisWeight(pos, 50, OverlapOut, DiscardOut, startOutward: false, endOutward: false);

            Assert.True(weight >= 0f, $"Expected non-negative weight at pos={pos}.");
        }
    }
}
