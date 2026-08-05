using Xunit;

namespace PrismCoreTests.Analyzers;

/// <summary>
/// T-5000: the filename orientation analyzer must read camera views, not garment nouns. Every
/// filename below is a real stem taken from <c>test/datasets</c> — the false positives are the ones
/// the T-4970 second pass found by hand, the true positives are the convention a real customer batch
/// (VINGINO79, 2,847 images) actually uses. Runs against the shipped analyzer_Config.json so a
/// vocabulary edit that breaks the contract fails here rather than on a customer batch.
/// </summary>
public class Analyzer_FilenameEvidenceTests {
    private static readonly Analyzer_FilenameEvidence.Config Cfg = AnalyzerParameters.FromConfig().Filename;
    private static readonly ProductTypeResolver Resolver = ProductTypeResolver.Load(ConfigLoader.RequireFile("ProductTypeMap.json"));

    //  True positives — the trailing-token convention

    [Theory]
    [InlineData("C153KB300004_Hanae_Sangria Sunset_FRONT.png", "FRONT")]
    [InlineData("C153KB300007_Hayata_Deep Black_BACK.png", "BACK")]
    [InlineData("C153KU420009_Kendall_Twill sand_FRONT.png", "FRONT")]
    [InlineData("25W_538_back.jpg", "BACK")]
    [InlineData("triggered_ghost_back.jpg", "BACK")]
    [InlineData("triggered_ghost-front.jpg", "FRONT")]
    public void Analyze_TrailingOrientationToken_WritesOrientation(string filename, string expected) {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = filename };

        Analyzer_FilenameEvidence.Analyze(lambda, Resolver, Cfg);

        Assert.Equal(expected, lambda.Features.GetValue("hero-orientation"));
    }

    //  False positives — garment nouns and product-name compounds

    [Theory]
    [InlineData("F-MODE-GO-ADJUSTABLE-FLATFORM-BACK-STRAP-SANDALS-ALL-BLACK_IV9-090_1.png")]  // back-strap, the part
    [InlineData("freya_top_cinzia_skirt_F.jpg")]                                             // the garment is a top
    [InlineData("freya_top_cinzia_skirt_B.jpg")]
    [InlineData("Malibu_ivory_TOP (1).jpg")]                                                 // bikini top, the piece
    [InlineData("Malibu_ivory_BOTTOM (2).jpg")]                                              // bikini bottoms
    [InlineData("Alba_ivory_B - BOTTOM (1).jpg")]
    public void Analyze_MidNameOrientationToken_WritesNothing(string filename) {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = filename };

        Analyzer_FilenameEvidence.Analyze(lambda, Resolver, Cfg);

        Assert.Equal("UNKNOWN", lambda.Features.GetValue("hero-orientation"));
    }

    [Fact]
    public void Analyze_OrientationWordInsideColourName_DoesNotOutrankTheTrailingView() {
        // Three VINGINO79 files carry the colour "Deep Back". Scanning tokens left-to-right and
        // stopping at the first hit labelled this front shot BACK; the trailing rule reads the view.
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "WO25KB420009_Clive_Deep Back_FRONT.png" };

        Analyzer_FilenameEvidence.Analyze(lambda, Resolver, Cfg);

        Assert.Equal("FRONT", lambda.Features.GetValue("hero-orientation"));
    }

    [Fact]
    public void Analyze_OrientationWordInColourName_WithNonViewSuffix_WritesNothing() {
        // Same colour name, but the trailing token is a detail marker, so there is no view to read.
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "WO25KB420009_Clive_Deep Back_DETAIL1.png" };

        Analyzer_FilenameEvidence.Analyze(lambda, Resolver, Cfg);

        Assert.Equal("UNKNOWN", lambda.Features.GetValue("hero-orientation"));
    }

    //  Precedence contract — a stronger measurement is never overwritten

    [Fact]
    public void Analyze_StrongerExistingMeasurement_IsNotOverwritten() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "25W_538_back.jpg" };
        lambda.Features.Set("hero-orientation", "FRONT", 0.95, "clip");

        Analyzer_FilenameEvidence.Analyze(lambda, Resolver, Cfg);

        Assert.Equal("FRONT", lambda.Features.GetValue("hero-orientation"));
    }

    [Fact]
    public void Analyze_WeakerExistingMeasurement_IsOverwritten() {
        ImageRecord_LAMBDA lambda = new() { InitialFullName = "25W_538_back.jpg" };
        lambda.Features.Set("hero-orientation", "FRONT", 0.20, "clip");

        Analyzer_FilenameEvidence.Analyze(lambda, Resolver, Cfg);

        Assert.Equal("BACK", lambda.Features.GetValue("hero-orientation"));
    }

    //  Config contract

    [Fact]
    public void Config_MiscasedTokenKey_FailsLoud() {
        // Filename tokens keep their original case and are lowercased for lookup, so a capitalised
        // key would sit in the JSON matching nothing. That must be a load error, not a silent no-op.
        Analyzer_FilenameEvidence.Config cfg = new() {
            OrientationConfidence = 0.60f,
            OrientationTokens = new Dictionary<string, string> { ["Front"] = "FRONT" }
        };

        PrismConfigurationException ex = Assert.Throws<PrismConfigurationException>(cfg.Validate);
        Assert.Contains("Front", ex.Message);
    }

    [Fact]
    public void Config_EmptyTokenMap_FailsLoud() {
        Analyzer_FilenameEvidence.Config cfg = new() {
            OrientationConfidence = 0.60f,
            OrientationTokens = []
        };

        Assert.Throws<PrismConfigurationException>(cfg.Validate);
    }

    [Fact]
    public void Config_ShippedTokenMap_IsAllLowercase() {
        Assert.All(Cfg.OrientationTokens.Keys, k => Assert.Equal(k.ToLowerInvariant(), k));
    }
}
