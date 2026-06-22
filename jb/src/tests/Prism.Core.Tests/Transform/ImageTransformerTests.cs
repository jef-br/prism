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
    // ─── Routing — problem processor ─────────────────────────────────────────

    [Fact]
    public void TransformImage_NoPhenotype_RoutesToProblemImageProcessor()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null);

        ImageTransformer.TransformImage(lambda);

        Assert.Equal(nameof(Tx_ProblemImageProcessor), lambda.TransformationResult?.TransformerType);
    }

    [Fact]
    public void TransformImage_ProblemProcessor_HasWarning()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null);

        ImageTransformer.TransformImage(lambda);

        Assert.NotEmpty(lambda.TransformationResult?.Warnings ?? []);
    }

    // ─── Routing — detail cropper ─────────────────────────────────────────────

    [Fact]
    public void TransformImage_CloseupImage_RoutesToDetailCropper()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "closeup-image");

        ImageTransformer.TransformImage(lambda);

        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult?.TransformerType);
    }

    [Fact]
    public void TransformImage_ModelDetailCloseup_RoutesToDetailCropper()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "model-detail-closeup");

        ImageTransformer.TransformImage(lambda);

        Assert.Equal(nameof(Tx_DetailCropper), lambda.TransformationResult?.TransformerType);
    }

    // ─── Routing — center and stretch ────────────────────────────────────────

    [Fact]
    public void TransformImage_GenericPhenotype_RoutesToCenterAndStretch()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "packshot-front");

        ImageTransformer.TransformImage(lambda);

        Assert.Equal(nameof(Tx_CenterAndStretch), lambda.TransformationResult?.TransformerType);
    }

    [Fact]
    public void TransformImage_LifestylePhenotype_RoutesToCenterAndStretch()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "lifestyle-context");

        ImageTransformer.TransformImage(lambda);

        Assert.Equal(nameof(Tx_CenterAndStretch), lambda.TransformationResult?.TransformerType);
    }

    // ─── Gated status ────────────────────────────────────────────────────────

    [Fact]
    public void TransformImage_ProcessorUnavailable_StatusIsGated()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "packshot-front");

        ImageTransformer.TransformImage(lambda);

        Assert.Equal(TransformationStatus.Gated, lambda.TransformationResult?.Status);
    }

    [Fact]
    public void TransformImage_ResultAttachedToInputLambda()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: null);

        ImageRecord_LAMBDA returned = ImageTransformer.TransformImage(lambda);

        Assert.Same(lambda, returned);
        Assert.NotNull(lambda.TransformationResult);
    }

    // ─── Input dimensions recorded ───────────────────────────────────────────

    [Fact]
    public void TransformImage_InputDimensionsRecordedOnResult()
    {
        ImageRecord_LAMBDA lambda = MakeLambda("img.jpg", phenotype: "packshot-front", width: 1200, height: 1600);

        ImageTransformer.TransformImage(lambda);

        Assert.Equal(1200, lambda.TransformationResult?.InputWidth);
        Assert.Equal(1600, lambda.TransformationResult?.InputHeight);
    }

    // ─── Service — transform disabled ────────────────────────────────────────

    [Fact]
    public async Task Service_TransformDisabled_AllNonKoImagesAreSkipped()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("a.jpg", "FAM001", phenotype: "packshot-front"),
            MakeLambda("b.jpg", "FAM001", phenotype: null)
        ]);

        await new TransformService().TransformAsync(matched, transformEnabled: false, null, default);

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

        await new TransformService().TransformAsync(matched, transformEnabled: false, null, default);

        Assert.Null(matched.LambdaRecords[0].TransformationResult);
    }

    [Fact]
    public async Task Service_TransformDisabled_OkTransformedCountIsZero()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("a.jpg", "FAM001", phenotype: "packshot-front")
        ]);

        TransformResult result = await new TransformService().TransformAsync(matched, transformEnabled: false, null, default);

        Assert.Equal(0, result.OkTransformedCount);
    }

    // ─── Service — transform enabled ──────────────────────────────────────────

    [Fact]
    public async Task Service_TransformEnabled_OkTransformedCountMatchesNonKoImages()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("a.jpg", "FAM001", phenotype: "packshot-front"),
            MakeLambda("b.jpg", "FAM001", phenotype: null),
            MakeLambda("ko.jpg", "FAM002", phenotype: null, isKo: true)
        ]);

        TransformResult result = await new TransformService().TransformAsync(matched, transformEnabled: true, null, default);

        Assert.Equal(2, result.OkTransformedCount);
    }

    [Fact]
    public async Task Service_TransformEnabled_KoImagesNotTransformed()
    {
        MatchingResult matched = MakeMatching(
        [
            MakeLambda("ko.jpg", "FAM001", phenotype: null, isKo: true)
        ]);

        TransformResult result = await new TransformService().TransformAsync(matched, transformEnabled: true, null, default);

        Assert.Null(matched.LambdaRecords[0].TransformationResult);
        Assert.Equal(0, result.OkTransformedCount);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static ImageRecord_LAMBDA MakeLambda(
        string filename,
        string? phenotype,
        int width  = 1000,
        int height = 1000,
        bool isKo  = false)
    {
        return new ImageRecord_LAMBDA
        {
            InitialFullName   = filename,
            Width             = width,
            Height            = height,
            SelectedPhenotype = phenotype,
            IsKo              = isKo,
            KoReasonCode      = isKo ? "TEST_KO" : null
        };
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
