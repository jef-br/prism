namespace Prism.Services.Matching;

/// <summary>
/// One populated quantization bucket from the dominant-color analysis: the mean color of the
/// bucket's pixels ([0,1] channels) and its share of the sampled subject pixels.
/// </summary>
public sealed record ColorBucket(float R, float G, float B, float Share) {

    /// <summary>The bucket's mean color as a lowercase #rrggbb hex string.</summary>
    // 255 is the max value of an 8-bit color channel — structural, never tuned.
#pragma warning disable S109
    public string Hex => $"#{(int)(R * 255f):x2}{(int)(G * 255f):x2}{(int)(B * 255f):x2}";
#pragma warning restore S109
}
