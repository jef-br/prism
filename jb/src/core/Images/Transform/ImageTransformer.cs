/// <summary>
/// Entry point for the Transformed stage. Routes each image to the appropriate
/// <see cref="IImageTransformation"/> strategy based on the image's assigned phenotype
/// and measured ImageFeatures, then records the transform decision on the lambda record.
/// </summary>
/// <remarks>
/// Images with no assigned phenotype or with unknown critical features are routed to
/// <see cref="Tx_ProblemImageProcessor"/> for conservative handling.
/// Close-up and detail images go to <see cref="Tx_DetailCropper"/>.
/// All other phenotyped images go to <see cref="Tx_CenterAndStretch"/>.
/// Actual pixel processing in each strategy is gated on preprocessor availability.
/// </remarks>
public static class ImageTransformer
{
    /// <summary>
    /// Routes the image to the appropriate transform strategy, records the decision on
    /// <see cref="ImageRecord_LAMBDA.TransformationResult"/>, and returns the updated record.
    /// </summary>
    /// <param name="lambda">Image record as it exists after the Generated stage.</param>
    /// <returns>The same record instance with <see cref="ImageRecord_LAMBDA.TransformationResult"/> set.</returns>
    public static ImageRecord_LAMBDA TransformImage(ImageRecord_LAMBDA lambda)
    {
        IImageTransformation transformer = SelectTransformer(lambda);
        return transformer.Transform(lambda);
    }

    // ─── Strategy selection ───────────────────────────────────────────────────

    /// <summary>
    /// Selects the transform strategy based on the image's assigned phenotype.
    /// Missing phenotype (unknown critical features) routes to the problem processor.
    /// </summary>
    private static IImageTransformation SelectTransformer(ImageRecord_LAMBDA lambda) =>
        lambda.SelectedPhenotype switch
        {
            "closeup-image" or "model-detail-closeup" => new Tx_DetailCropper(),
            null                                       => new Tx_ProblemImageProcessor(),
            _                                          => new Tx_CenterAndStretch()
        };
}
