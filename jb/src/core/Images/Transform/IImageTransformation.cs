namespace Prism.Core;

/// <summary>Contract for all image transformation strategies.</summary>
public interface IImageTransformation
{
    /// <summary>
    /// Applies the transformation to <paramref name="InputImage"/> using its Lambda record,
    /// records the outcome in <see cref="ImageRecord_LAMBDA.TransformationResult"/>, and returns the record.
    /// </summary>
    ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage);

    /// <summary>
    /// Stateless pixel-only entry point for the webservice path.
    /// Operates on raw bytes without any Lambda record; uses <paramref name="upscale_factor"/>
    /// to scale the result to the requested output size.
    /// </summary>
    byte[] Process(byte[] arr, int stride, float upscale_factor);
}
