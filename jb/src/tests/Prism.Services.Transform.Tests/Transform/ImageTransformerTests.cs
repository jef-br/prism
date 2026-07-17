using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Unit tests for <see cref="ImageTransformer"/> transform routing and
/// <see cref="TransformService"/> skip-flag behaviour.
/// Transform routing tests call <see cref="ImageTransformer.TransformImage"/> directly with an
/// injected <see cref="TransformParameters"/> bundle.
/// Service behaviour tests verify the skip path and the OkTransformed count via the typed result.
/// </summary>
public class ImageTransformerTests
{
    // NOTE: ImageTransformer.BypassPhenotypes is currently ON (temporary PoC gate). While on,
    // SelectedPhenotype does not affect routing — only salient-bbox and edge intersects do.
    // These tests assert the gate-on behavior. See jb/src/core/Images/Classify/jbtodo.md.

    // Mirrors the shipped transform_Config.json. Built here rather than loaded from disk: the routing
    // tests below construct Tx_ classes through ImageTransformer and need a bundle whose values they
    // control, not whatever the deployed config happens to say.
    private static readonly TransformParameters Parameters = new()
    {
        Crop                   = new() { WhiteSpaceMargin = 0.042, CropCoverage = 0.8, CropExtensionOneSided = 0.14, CropExtensionBiDirectional = 0.25 },
        ProblemImageProcessor  = new() { MinInputPx = 570, MinOutputPx = 800, MaxUpscale = 1.42 },
        BgStretch              = new() { Tier1MaxRatio = 1.25f, Tier2MaxRatio = 1.42f, Tier4MinRatio = 2.50f, FeatherPx = 16 },
        DetailCropper          = new() { AdjacentCropCap = 0.14 },
        LowContrastEnhancement = new() { ClipLimit = 2.0, TileSize = 8 },
        HeadCutter             = new() { FaceHeightCutFactor = 0.75 }
    };

    //  Routing — problem processor

    [Fact]
    public void TransformImage_NoBbox_RoutesToProblemImageProcessor()
    {
        // No salient-bbox set → bbox guard fires regardless of phenotype.
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null);

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(nameof(Tx_ProblemImageProcessor), lambda.OutputRecord?.TransformerType);
    }

    [Fact]
    public void TransformImage_ProblemProcessor_HasWarning()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null);

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.NotEmpty(lambda.OutputRecord?.Warnings ?? []);
    }

    //  Routing — phenotype is bypassed

    [Fact]
    public void TransformImage_CloseupWithBboxAndIntersect_RoutesToCropSquare()
    {
        // Gate on: DetailCropper is unreachable; closeup + intersect falls back to the square crop.
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "closeup-image", hasBbox: true, intersects: true);

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(nameof(Tx_CropSquare), lambda.OutputRecord?.TransformerType);
    }

    [Fact]
    public void TransformImage_BboxAndIntersect_RoutesToCropSquare()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null, hasBbox: true, intersects: true);

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(nameof(Tx_CropSquare), lambda.OutputRecord?.TransformerType);
    }

    //  Routing — center and stretch

    [Fact]
    public void TransformImage_BboxNoIntersect_RoutesToCenterAndStretch()
    {
        // Phenotype is irrelevant while bypassing; bbox present + no edge intersect → CenterAndStretch.
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null, hasBbox: true);

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(nameof(Tx_CenterAndStretch), lambda.OutputRecord?.TransformerType);
    }

    [Fact]
    public void TransformImage_PhenotypeDoesNotChangeRouting_WhileBypassed()
    {
        // Same geometry, different phenotypes → identical routing while the gate is on.
        ImageRecord_LAMBDA closeup = MakeLambda("a.jpg", phenotype: "closeup-image",  hasBbox: true);
        ImageRecord_LAMBDA generic = MakeLambda("b.jpg", phenotype: "packshot-front", hasBbox: true);

        ImageTransformer.TransformImage(closeup, null, false, Parameters);
        ImageTransformer.TransformImage(generic, null, false, Parameters);

        Assert.Equal(nameof(Tx_CenterAndStretch), closeup.OutputRecord?.TransformerType);
        Assert.Equal(nameof(Tx_CenterAndStretch), generic.OutputRecord?.TransformerType);
    }

    [Fact]
    public void TransformImage_ResultAttachedToInputLambda()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null);

        ImageRecord_LAMBDA returned = ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Same(lambda, returned);
        Assert.NotNull(lambda.OutputRecord);
    }

    //  Input dimensions recorded

    [Fact]
    public void TransformImage_InputDimensionsRecordedOnResult()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "packshot-front", width: 1200, height: 1600);

        ImageTransformer.TransformImage(lambda, null, false, Parameters);

        Assert.Equal(1200, lambda.OutputRecord?.InputWidth);
        Assert.Equal(1600, lambda.OutputRecord?.InputHeight);
    }

    //  Service — transform disabled 

    [Fact]
    public async Task Service_TransformDisabled_AllNonKoImagesAreSkipped()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("a.jpg", "FAM001", phenotype: "packshot-front"),
            MakeLambda("b.jpg", "FAM001", phenotype: null)
        ]);

        await new TransformService().TransformAsync(matched, transformEnabled: false, headcut: false, null, default);

        Assert.All(matched.LambdaRecords, r =>
            Assert.Equal(TransformationStatus.Skipped, r.OutputRecord?.TransformStatus));
    }

    [Fact]
    public async Task Service_TransformDisabled_KoImagesNotTouched()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("ko.jpg", "FAM001", phenotype: null, isKo: true)
        ]);

        await new TransformService().TransformAsync(matched, transformEnabled: false, headcut: false, null, default);

        Assert.Null(matched.LambdaRecords[0].OutputRecord);
    }

    [Fact]
    public async Task Service_TransformDisabled_OkTransformedCountIsZero()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("a.jpg", "FAM001", phenotype: "packshot-front")
        ]);

        TransformResult result = await new TransformService().TransformAsync(matched, transformEnabled: false, headcut: false, null, default);

        Assert.Equal(0, result.OkTransformedCount);
    }

    //  Service — transform enabled 

    [Fact]
    public async Task Service_TransformEnabled_OkTransformedCountMatchesNonKoImages()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("a.jpg", "FAM001", phenotype: "packshot-front"),
            MakeLambda("b.jpg", "FAM001", phenotype: null),
            MakeLambda("ko.jpg", "FAM002", phenotype: null, isKo: true)
        ]);

        TransformResult result = await new TransformService().TransformAsync(matched, transformEnabled: true, headcut: false, null, default);

        Assert.Equal(2, result.OkTransformedCount);
    }

    [Fact]
    public async Task Service_TransformEnabled_KoImagesNotTransformed()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("ko.jpg", "FAM001", phenotype: null, isKo: true)
        ]);

        TransformResult result = await new TransformService().TransformAsync(matched, transformEnabled: true, headcut: false, null, default);

        Assert.Null(matched.LambdaRecords[0].OutputRecord);
        Assert.Equal(0, result.OkTransformedCount);
    }

    //  Helpers 

    private static ImageRecord_LAMBDA MakeLambda(
        string filename,
        string? phenotype,
        int width      = 1000,
        int height     = 1000,
        bool isKo      = false,
        bool hasBbox   = false,
        bool intersects = false)
    {
        ImageRecord_LAMBDA lambda = new()
        {
            InitialFullName   = filename,
            Width             = width,
            Height            = height,
            SelectedPhenotype = phenotype,
            IsKo              = isKo,
            KoReasonCode      = isKo ? "TEST_KO" : null
        };

        if (hasBbox)
            lambda.BoundingBox = new BoundingBox { X = 100, Y = 100, Width = 800, Height = 800,
                                                   Left = 100, Top = 100, Right = 900, Bottom = 900 };
        if (intersects)
            lambda.Features.Set("intersects-top", "true", 1.0, "test");

        return lambda;
    }

    private static ImageRecord_LAMBDA MakeLambda(
        string filename,
        string family,
        string? phenotype,
        int width  = 1000,
        int height = 1000,
        bool isKo  = false)
    {
        ImageRecord_LAMBDA lambda = MakeLambda(filename, phenotype, width, height, isKo);
        lambda.Family = family;
        return lambda;
    }

    /// <summary>
    /// Builds a minimal <see cref="MatchingResult"/> carrying the given LAMBDA records, with an empty
    /// <see cref="IngestResult"/> — enough for the Transform service, which only reads the LAMBDA list.
    /// </summary>
    private static MatchingResult MakeMatching(IReadOnlyList<ImageRecord_LAMBDA> images)
    {
        IngestResult ingest = new()
        {
            JobID            = Guid.NewGuid(),
            Parameters       = new PrismProcessingParameters { Format = "json" },
            NormalizedImages = [],
            FamilyRecords    = [],
            JobTempFolder    = string.Empty
        };

        return new MatchingResult
        {
            Ingest        = ingest,
            LambdaRecords = [.. images]
        };
    }
}
