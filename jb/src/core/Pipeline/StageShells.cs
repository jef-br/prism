/// <summary>
/// Shell delegate for the Imported stage.
/// Receives raw input records; normalizes images, unpacks zips, and parses Excel into the IEM.
/// Real implementation lives in <c>Importer.cs</c> and <c>ZipHandler.cs</c>.
/// </summary>
internal static class ImportStageShell
{
    /// <summary>
    /// Runs the Imported stage for a job context.
    /// T-400 will replace this body with real Importer delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-400: delegate to Importer.cs and ZipHandler.cs
        context.MarkStageCompleted(PipelineStageNames.Imported);
    }
}

/// <summary>
/// Shell delegate for the Classified stage.
/// Deduplicates images by visual hash then applies CLIP ONNX classification.
/// Real implementation lives in <c>ImageClassifier.cs</c>.
/// </summary>
internal static class ClassifyStageShell
{
    /// <summary>
    /// Runs the Classified stage for a job context.
    /// T-410 will replace this body with real ImageClassifier delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-410: delegate to ImageClassifier.cs
        context.MarkStageCompleted(PipelineStageNames.Classified);
    }
}

/// <summary>
/// Shell delegate for the Matched stage.
/// Tokenizes each image and resolves a FamilyID above threshold using the matcher waterfall.
/// Real implementation lives in <c>ImageMatcher.cs</c>.
/// </summary>
internal static class MatchStageShell
{
    /// <summary>
    /// Runs the Matched stage for a job context.
    /// T-420 will replace this body with real ImageMatcher delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-420: delegate to ImageMatcher.cs
        context.MarkStageCompleted(PipelineStageNames.Matched);
    }
}

/// <summary>
/// Shell delegate for the Ordered stage.
/// Assigns det-order indices per FamilyID using classification labels and filename tokens.
/// Real implementation lives in <c>ImageOrderer.cs</c>.
/// </summary>
internal static class OrderStageShell
{
    /// <summary>
    /// Runs the Ordered stage for a job context.
    /// T-430 will replace this body with real ImageOrderer delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-430: delegate to ImageOrderer.cs
        context.MarkStageCompleted(PipelineStageNames.Ordered);
    }
}

/// <summary>
/// Shell delegate for the Renamed stage.
/// Collapses FamilyID and det-order into the final output filename.
/// </summary>
internal static class RenameStageShell
{
    /// <summary>
    /// Runs the Renamed stage for a job context.
    /// T-440 will replace this body with real rename logic.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-440: apply FamilyID_det# rename to each accepted ImageRecord_LAMBDA
        context.MarkStageCompleted(PipelineStageNames.Renamed);
    }
}

/// <summary>
/// Shell delegate for the Generated stage.
/// For families below minimum image count, copies the hero image and creates generated variants.
/// Real implementation lives in the generation module.
/// </summary>
internal static class GenerateStageShell
{
    /// <summary>
    /// Runs the Generated stage for a job context.
    /// T-450 will replace this body with real generation delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-450: delegate to generation module when Parameters.Generation is true
        context.MarkStageCompleted(PipelineStageNames.Generated);
    }
}

/// <summary>
/// Shell delegate for the Transformed stage.
/// Applies visual transformations per ImageNGP state.
/// Real implementation lives in <c>ImageTransformer.cs</c>.
/// </summary>
internal static class TransformStageShell
{
    /// <summary>
    /// Runs the Transformed stage for a job context.
    /// T-460 will replace this body with real ImageTransformer delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-460: delegate to ImageTransformer.cs when Parameters.Transform is true
        context.MarkStageCompleted(PipelineStageNames.Transformed);
    }
}

/// <summary>
/// Shell delegate for the Exported stage.
/// Packages all output images with manifest.json into the requested output format.
/// Real implementation lives in <c>Exporter.cs</c>.
/// </summary>
internal static class ExportStageShell
{
    /// <summary>
    /// Runs the Exported stage for a job context.
    /// T-470 will replace this body with real Exporter delegation.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    /// <param name="configuration">Validated PRISM configuration.</param>
    internal static void Run(PipelineContext context, PrismConfiguration configuration)
    {
        // TODO T-470: delegate to Exporter.cs
        context.MarkStageCompleted(PipelineStageNames.Exported);
    }
}
