namespace Prism.Core;

/// <param name="ImageBytes">Raw JPEG bytes to upscale.</param>
/// <param name="ScaleFactor">Multiplicative scale factor (e.g. 1.42 for 42% enlargement).</param>
public sealed record UpscaleRequest( byte[] ImageBytes, double ScaleFactor );
