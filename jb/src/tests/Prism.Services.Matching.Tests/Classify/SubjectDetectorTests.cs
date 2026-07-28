using OpenCvSharp;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// T-4830: the classical-CV SubjectDetector isolates the product against a studio sweep by chroma +
/// texture. Synthetic cases: a coloured subject on white is boxed inside the frame; a flat frame yields
/// no detection (whole frame, zero confidence); a subject running off an edge sets that intersect flag.
/// </summary>
public class SubjectDetectorTests {
    private static SubjectDetectorConfig Config() => new() {
        MaxAnalysisSize = 1024,
        TextureWindow = 7,
        TextureDetailSigma = 4.0,
        OutlierSpreadMultiplier = 4.0,
        MinComponentAreaFraction = 0.0005,
        MinComponentAreaRatio = 0.05,
        MinComponentAreaPixels = 25.0,
        WholeFrameFraction = 0.985,
        ShadowEdgeKernel = 15,
        CannySigma = 0.33,
        CannyCloseKernel = 5,
        BorderRingFraction = 0.02,
        ChromaFloor = 2.0,
        TextureFloor = 2.0,
        ClaheClipLimit = 2.0,
        ClaheTileSize = 8,
        BleedContact = 0.2,
        // Deliberately below the shipped 0.05 (ClassifyConfig.json): these tests pin the hard-shadow
        // mechanism on synthetic frames, not the calibration. Do not sync this to the shipped value.
        HardShadowEvidenceFraction = 0.01,
        RealLifeResidualThreshold = 3.5,
        StudioSweepSpeckleKernel = 7,
        RealLifeAnalysisSize = 1536,
        RealLifeMinComponentAreaRatio = 0.12
    };

    [Fact]
    public void Detect_ColouredSubjectOnWhite_BoxesInsideFrame_NoIntersects() {
        using Mat img = White(200, 200);
        Cv2.Rectangle(img, new Rect(50, 50, 100, 100), new Scalar(40, 40, 200), thickness: -1);

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.Equal("classical-cv", d.Producer);
        Assert.True(d.Box.Width < 200 && d.Box.Height < 200, "should not be the whole frame");
        // Box should enclose the drawn square (allowing detector margin + morphology growth).
        Assert.True(d.Box.Left <= 55 && d.Box.Right >= 145, $"box {d.Box.Left}..{d.Box.Right} should cover the subject");
        Assert.False(d.IntersectsTop || d.IntersectsBottom || d.IntersectsLeft || d.IntersectsRight);
        Assert.NotNull(d.MaskPng);
        Assert.NotEmpty(d.MaskPng!);
        Assert.True(d.Confidence > 0.2);
    }

    [Fact]
    public void Detect_FlatFrame_YieldsWholeFrameNoDetection() {
        using Mat img = White(200, 200);

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.Equal(200, d.Box.Width);
        Assert.Equal(200, d.Box.Height);
        Assert.Equal(0.0, d.Confidence);
    }

    [Fact]
    public void Detect_SubjectRunningOffBottom_SetsBottomIntersect() {
        using Mat img = White(200, 200);
        Cv2.Rectangle(img, new Rect(50, 120, 100, 80), new Scalar(40, 180, 40), thickness: -1);

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.True(d.IntersectsBottom, "subject runs off the bottom edge");
        Assert.False(d.IntersectsTop, "subject does not reach the top edge");
    }

    [Fact]
    public void Detect_WhiteOnWhiteWithFineTexture_BoxesTextureRegion_NoIntersects() {
        // White-on-white isolation via texture: subject and background differ only in surface texture,
        // not colour or brightness. A white rectangle filled with surface texture (e.g., woven fabric)
        // on a flat white background. The detector uses local-std-dev of a high-pass filtered image.
        using Mat img = White(200, 200);

        // Create subject region with texture variation: 80x80 rect at (60,60) with alternating lines
        // simulating fabric weave. Achromatic throughout — the subject differs from the background in
        // surface texture only, never in colour, which is the whole point of this case.
        //
        // Amplitude is 60 grey levels (195 vs 255), and that is a measured constraint, not a free choice:
        // detection opens with Cv2.BilateralFilter(bgr, denoised, 5, 40, 40), and a bilateral filter
        // smooths away variation below its sigmaColor. At an amplitude of 15 (240 vs 255) the weave is
        // erased before the texture measure ever sees it and the detector correctly-but-unhelpfully
        // reports whole-frame. The sensitivity floor that implies for real low-contrast fabric is a real
        // open question — see [[T-4948]]. Do not lower this amplitude to make an assertion pass.
        for (int y = 60; y < 140; y++) {
            for (int x = 60; x < 140; x++) {
                // Checkerboard-like texture: alternating stripes ~2 pixels wide
                byte val = ((y + x) % 4 < 2) ? (byte)195 : (byte)255;
                // Vec3b, never Scalar: Mat.Set<T> writes sizeof(T) bytes into the pixel, and Scalar is
                // four doubles (32 bytes) against a 3-byte CV_8UC3 pixel — a 29-byte overrun per call
                // that corrupts the native heap and crashes the test host at the end of the buffer.
                img.Set(y, x, new Vec3b(val, val, val));
            }
        }

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.Equal("classical-cv", d.Producer);
        Assert.False(d.IsWholeFrameFallback, "texture-only region should not fall back to whole frame");
        Assert.True(d.Box.Width < 200 && d.Box.Height < 200, "box should be meaningfully smaller than frame");
        Assert.True(d.Box.Width > 30 && d.Box.Height > 30, "box should capture the textured region, not vanish");
        // Detector morphology (opening, closing) can expand/shrink by ~15px per side; allow generous bounds
        Assert.True(d.Box.Left >= 45 && d.Box.Right <= 160, $"box {d.Box.Left}-{d.Box.Right} should span texture region with morphology margin");
        Assert.True(d.Box.Top >= 45 && d.Box.Bottom <= 160, $"box {d.Box.Top}-{d.Box.Bottom} should span texture region with morphology margin");
        Assert.False(d.IntersectsTop || d.IntersectsBottom || d.IntersectsLeft || d.IntersectsRight);
        Assert.True(d.Confidence > 0.2);
    }

    [Fact]
    public void Detect_UniformGradientBackdrop_LowTextureThreshold_NoDetection() {
        // The defining invariant: a smooth luminance gradient (like a curved backdrop or floor)
        // with NO sharp edges and NO chroma shift is not subject. This exercises the background-plane-fit
        // path: the detector fits a luminance plane to the border ring and subtracts it. A uniform
        // gradient has no residual texture after plane subtraction, so it should not be detected.
        using Mat img = new(200, 200, MatType.CV_8UC3);

        // Create a uniform luminance gradient across the entire image (no boundaries, no edge texture).
        // Gradient runs from dark (80) at left to bright (220) at right, smoothly.
        for (int y = 0; y < 200; y++) {
            for (int x = 0; x < 200; x++) {
                // Linear ramp from 80 at x=0 to 220 at x=199
                byte val = (byte)Math.Clamp(80 + (x / 199.0) * (220 - 80), 0, 255);
                img.Set(y, x, new Vec3b(val, val, val));   // Vec3b (3 bytes), not Scalar (32) — see above
            }
        }

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.Equal("classical-cv", d.Producer);
        // Pure gradient with no edges, texture, or chroma should not be detected.
        // The background plane fit will model the ramp, leaving near-zero residuals.
        Assert.True(d.IsWholeFrameFallback, "uniform gradient backdrop with no texture should yield whole-frame");
        Assert.Equal(0.0, d.Confidence);
    }

    [Fact]
    public void Detect_ProductWithHardShadowEdge_HasHardShadowEvidence() {
        // Hard-shadow evidence flag: the detector identifies the presence of thin, texture-only edges
        // (characteristic of hard-edged cast shadows: chroma-unsupported texture at a boundary).
        // Build a clear product blob with an adjacent hard-edged shadow boundary.
        using Mat img = White(200, 200);

        // Product: 60x60 green blob at (50, 50)
        Cv2.Rectangle(img, new Rect(50, 50, 60, 60), new Scalar(40, 180, 40), thickness: -1);

        // Hard shadow edge: a thin (3 pixel) dark line directly below the product, same hue as background.
        // This is a hard-edged shadow boundary: texture (the edge) but no chroma difference from background.
        Cv2.Rectangle(img, new Rect(50, 110, 60, 3), new Scalar(80, 80, 80), thickness: -1);

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.Equal("classical-cv", d.Producer);
        Assert.False(d.IsWholeFrameFallback, "should detect the green product");

        // Verify product is covered by the box:
        Assert.True(d.Box.Left <= 60, $"box left {d.Box.Left} should cover product");
        Assert.True(d.Box.Right >= 100, $"box right {d.Box.Right} should cover product");
        Assert.True(d.Box.Top <= 60, $"box top {d.Box.Top} should cover product");

        // Hard-shadow evidence: a thin texture-only edge (the 3px dark line with no chroma) should be
        // detected as a hard-shadow candidate. When morphopen strips this thin edge, it triggers
        // HasHardShadowEvidence.
        Assert.True(d.HasHardShadowEvidence, "thin hard-edged shadow should trigger hard-shadow evidence");
        Assert.True(d.Confidence > 0.2);

        // The defining invariant of the whole port: the shadow is a pure lightness change, and lightness is
        // never a detection criterion, so the shadow strip at y 110..113 must fall OUTSIDE the box. Product
        // ends at y=110; allow a few px of morphological slack but nothing that swallows the strip. A
        // detector that keyed on lightness would return a box reaching ~113 and fail here.
        Assert.True(d.Box.Bottom < 113, $"box bottom {d.Box.Bottom} reaches the cast shadow at y=110..113 — shadow was not excluded");
    }

    [Fact]
    public void Detect_GradientBackground_BoxesProductNotRamp_NoIntersects() {
        // Gradient background: the background colour is fitted as a plane over the border ring to handle
        // a backdrop curving into a floor. This test exercises that path — a smooth linear luminance ramp
        // across the frame, with a product blob on top. The detector should box the product and not treat
        // the ramp itself as subject.
        using Mat img = new(200, 200, MatType.CV_8UC3);

        // Create a linear gradient background: dark at left (100,100,100), bright at right (220,220,220)
        for (int y = 0; y < 200; y++) {
            for (int x = 0; x < 200; x++) {
                // Linear interpolation: at x=0 => 100, at x=199 => 220
                byte val = (byte)(100 + (x / 199.0) * 120);
                img.Set(y, x, new Vec3b(val, val, val));   // Vec3b (3 bytes), not Scalar (32) — see above
            }
        }

        // Product: 60x60 colored blob at (70, 70) — saturated red to stand out against ramp
        Cv2.Rectangle(img, new Rect(70, 70, 60, 60), new Scalar(40, 60, 200), thickness: -1);

        SubjectDetection d = new SubjectDetector(Config()).Detect(img);

        Assert.Equal("classical-cv", d.Producer);
        Assert.False(d.IsWholeFrameFallback, "should detect product despite gradient background");

        // Product region is roughly x [70, 130], y [70, 130]. Box should enclose it.
        Assert.True(d.Box.Left <= 80, $"box left {d.Box.Left} should cover product start");
        Assert.True(d.Box.Right >= 120, $"box right {d.Box.Right} should cover product end");
        Assert.True(d.Box.Top <= 80, $"box top {d.Box.Top} should cover product start");
        Assert.True(d.Box.Bottom >= 120, $"box bottom {d.Box.Bottom} should cover product end");

        // Assert the box is not the whole frame and not enormous (detector should not treat ramp as subject)
        Assert.True(d.Box.Width < 180, "box should not be near-frame-width; ramp should not be subject");
        Assert.True(d.Box.Height < 180, "box should not be near-frame-height; ramp should not be subject");

        Assert.False(d.IntersectsTop || d.IntersectsBottom || d.IntersectsLeft || d.IntersectsRight);
        Assert.True(d.Confidence > 0.2);
    }

    private static Mat White(int w, int h) => new(h, w, MatType.CV_8UC3, new Scalar(255, 255, 255));
}
