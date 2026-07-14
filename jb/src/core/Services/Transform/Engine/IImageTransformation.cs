namespace Prism.Services.Transform;

/// <summary>Contract for all image transformation strategies.</summary>
public interface IImageTransformation
{
    /// <summary>
    /// Pipeline-internal entry point. Called once per image during the Transformed stage, after
    /// ImagePreProcessor has already populated <paramref name="InputImage"/>.BoundingBox,
    /// .Features (intersects-top/bottom/left/right, has-human, etc.), and .ProcessedBytes.
    /// Reads everything it needs directly from InputImage and records the outcome on
    /// InputImage.OutputRecord. This is the path ImageTransformer always uses today.
    /// </summary>
    ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage);

    /// <summary>
    /// Stateless, standalone entry point for callers that only have raw image bytes and no
    /// pipeline-managed lambda record — e.g. a future webservice caller. Signature is fixed by
    /// this project's dual-interface contract (see AGENTFEEDBACK.md); <paramref name="lambda"/>
    /// is an additive optional parameter, not a break of that contract. When a caller happens to
    /// already have a lambda record (e.g. an internal caller that already ran ImagePreProcessor),
    /// pass it to reuse its BoundingBox/Features instead of recomputing them from <paramref
    /// name="arr"/>. When null, this method independently derives whatever bounding-box/geometry
    /// information it needs from arr itself.
    /// </summary>
    byte[] Process(byte[] arr, int stride, float upscale_factor, ImageRecord_LAMBDA? lambda = null);
}
