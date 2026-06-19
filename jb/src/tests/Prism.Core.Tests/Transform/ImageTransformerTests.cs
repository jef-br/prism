using Xunit;

namespace PrismCoreTests.Transform;

/// <summary>
/// Unit tests for <see cref="ImageTransformer"/> transform routing and
/// <see cref="ShellStage_Transform"/> skip-flag behaviour.
/// Transform routing tests call <see cref="ImageTransformer.TransformImage"/> directly
/// (no configuration dependency — processor gating is unconditional in this build).
/// Shell behaviour tests verify the skip path and context counter via the full shell signature.
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

    // ─── Shell — transform disabled ──────────────────────────────────────────

    [Fact]
    public void Shell_TransformDisabled_AllNonKoImagesAreSkipped()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("a.jpg", "FAM001", phenotype: "packshot-front"),
            MakeLambda("b.jpg", "FAM001", phenotype: null)
        ],
        transformEnabled: false);

        ShellStage_Transform.Run(context, LoadConfig());

        Assert.All(context.LambdaRecords, r =>
            Assert.Equal(TransformationStatus.Skipped, r.TransformationResult?.Status));
    }

    [Fact]
    public void Shell_TransformDisabled_KoImagesNotTouched()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("ko.jpg", "FAM001", phenotype: null, isKo: true)
        ],
        transformEnabled: false);

        ShellStage_Transform.Run(context, LoadConfig());

        Assert.Null(context.LambdaRecords[0].TransformationResult);
    }

    [Fact]
    public void Shell_TransformDisabled_OkTransformedCountIsZero()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("a.jpg", "FAM001", phenotype: "packshot-front")
        ],
        transformEnabled: false);

        ShellStage_Transform.Run(context, LoadConfig());

        Assert.Equal(0, context.OkTransformedCount);
    }

    // ─── Shell — transform enabled ────────────────────────────────────────────

    [Fact]
    public void Shell_TransformEnabled_OkTransformedCountMatchesNonKoImages()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("a.jpg", "FAM001", phenotype: "packshot-front"),
            MakeLambda("b.jpg", "FAM001", phenotype: null),
            MakeLambda("ko.jpg", "FAM002", phenotype: null, isKo: true)
        ]);

        ShellStage_Transform.Run(context, LoadConfig());

        Assert.Equal(2, context.OkTransformedCount);
    }

    [Fact]
    public void Shell_TransformEnabled_KoImagesNotTransformed()
    {
        PipelineContext context = MakeContext(
        [
            MakeLambda("ko.jpg", "FAM001", phenotype: null, isKo: true)
        ]);

        ShellStage_Transform.Run(context, LoadConfig());

        Assert.Null(context.LambdaRecords[0].TransformationResult);
        Assert.Equal(0, context.OkTransformedCount);
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

    private static PipelineContext MakeContext(
        IReadOnlyList<ImageRecord_LAMBDA> images,
        bool transformEnabled = true)
    {
        PipelineContext context = new(
            Guid.NewGuid(),
            imageRecords:   [],
            excelRecords:   [],
            zipFileRecords: [],
            parameters:     new PrismProcessingParameters { Format = "json", Transform = transformEnabled },
            startedAt:      DateTimeOffset.UtcNow);

        foreach (ImageRecord_LAMBDA img in images)
            context.LambdaRecords.Add(img);

        return context;
    }

    private static PrismConfiguration LoadConfig()
    {
        string? configPath = PrismConfigLocator.FindPrismConfigPath();
        return PrismConfiguration.Load(configPath!);
    }
}
