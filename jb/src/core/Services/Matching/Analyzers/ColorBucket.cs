namespace Prism.Services.Matching;

/// <summary>
/// One populated quantization bucket from the dominant-color analysis: the mean color of the
/// bucket's pixels ([0,1] channels) and its share of the sampled subject pixels.
/// </summary>
public sealed record ColorBucket(float R, float G, float B, float Share)
{
    /// <summary>The bucket's mean color as a lowercase #rrggbb hex string.</summary>
    public string Hex => $"#{(int)(R * 255):x2}{(int)(G * 255):x2}{(int)(B * 255):x2}";
}
