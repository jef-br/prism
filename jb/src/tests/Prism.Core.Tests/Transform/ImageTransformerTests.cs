using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Unit tests for <see cref="ImageTransformer"/> transform routing and
/// <see cref="TransformService"/> skip-flag behaviour.
/// Transform routing tests call <see cref="ImageTransformer.TransformImage"/> directly
/// (no configuration dependency — processor gating is unconditional in this build).
/// Service behaviour tests verify the skip path and the OkTransformed count via the typed result.
/// </summary>
public class ImageTransformerTests
{
    // NOTE: ImageTransformer.BypassPhenotypes is currently ON (temporary PoC gate). While on,
    // SelectedPhenotype does not affect routing — only salient-bbox and edge intersects do.
    // These tests assert the gate-on behavior. See jb/src/core/Images/Classify/jbtodo.md.

    private static readonly CropTransformSettings Settings = new(
        WhiteSpaceMargin: 0.042, CropCoverage: 0.8, CropExtensionOneSided: 0.14, CropExtensionBiDirectional: 0.25);

    //  Routing — problem processor

    [Fact]
    public void TransformImage_NoBbox_RoutesToProblemImageProcessor()
    {
        // No salient-bbox set → bbox guard fires regardless of phenotype.
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null);

        ImageTransformer.TransformImage(lambda, null, Settings, false);

        Assert.Equal(nameof(Tx_ProblemImageProcessor), lambda.TransformationResult?.TransformerType);
    }

    [Fact]
    public void TransformImage_ProblemProcessor_HasWarning()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null);

        ImageTransformer.TransformImage(lambda, null, Settings, false);

        Assert.NotEmpty(lambda.TransformationResult?.Warnings ?? []);
    }

    //  Routing — phenotype is bypassed

    [Fact]
    public void TransformImage_CloseupWithBboxAndIntersect_RoutesToCropSquare()
    {
        // Gate on: DetailCropper is unreachable; closeup + intersect falls back to the square crop.
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "closeup-image", hasBbox: true, intersects: true);

        ImageTransformer.TransformImage(lambda, null, Settings, false);

        Assert.Equal(nameof(Tx_CropSquare), lambda.TransformationResult?.TransformerType);
    }

    [Fact]
    public void TransformImage_BboxAndIntersect_RoutesToCropSquare()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null, hasBbox: true, intersects: true);

        ImageTransformer.TransformImage(lambda, null, Settings, false);

        Assert.Equal(nameof(Tx_CropSquare), lambda.TransformationResult?.TransformerType);
    }

    //  Routing — center and stretch

    [Fact]
    public void TransformImage_BboxNoIntersect_RoutesToCenterAndStretch()
    {
        // Phenotype is irrelevant while bypassing; bbox present + no edge intersect → CenterAndStretch.
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null, hasBbox: true);

        ImageTransformer.TransformImage(lambda, null, Settings, false);

        Assert.Equal(nameof(Tx_CenterAndStretch), lambda.TransformationResult?.TransformerType);
    }

    [Fact]
    public void TransformImage_PhenotypeDoesNotChangeRouting_WhileBypassed()
    {
        // Same geometry, different phenotypes → identical routing while the gate is on.
        ImageRecord_LAMBDA closeup = MakeLambda("a.jpg", phenotype: "closeup-image",  hasBbox: true);
        ImageRecord_LAMBDA generic = MakeLambda("b.jpg", phenotype: "packshot-front", hasBbox: true);

        ImageTransformer.TransformImage(closeup, null, Settings, false);
        ImageTransformer.TransformImage(generic, null, Settings, false);

        Assert.Equal(nameof(Tx_CenterAndStretch), closeup.TransformationResult?.TransformerType);
        Assert.Equal(nameof(Tx_CenterAndStretch), generic.TransformationResult?.TransformerType);
    }

    [Fact]
    public void TransformImage_ResultAttachedToInputLambda()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null);

        ImageRecord_LAMBDA returned = ImageTransformer.TransformImage(lambda, null, Settings, false);

        Assert.Same(lambda, returned);
        Assert.NotNull(lambda.TransformationResult);
    }

    //  Input dimensions recorded

    [Fact]
    public void TransformImage_InputDimensionsRecordedOnResult()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "packshot-front", width: 1200, height: 1600);

        ImageTransformer.TransformImage(lambda, null, Settings, false);

        Assert.Equal(1200, lambda.TransformationResult?.InputWidth);
        Assert.Equal(1600, lambda.TransformationResult?.InputHeight);
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
            Assert.Equal(TransformationStatus.Skipped, r.TransformationResult?.Status));
    }

    [Fact]
    public async Task Service_TransformDisabled_KoImagesNotTouched()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("ko.jpg", "FAM001", phenotype: null, isKo: true)
        ]);

        await new TransformService().TransformAsync(matched, transformEnabled: false, headcut: false, null, default);

        Assert.Null(matched.LambdaRecords[0].TransformationResult);
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

        Assert.Null(matched.LambdaRecords[0].TransformationResult);
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
