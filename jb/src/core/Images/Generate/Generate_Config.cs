/// <summary>
/// Typed generation parameters extracted from the <c>Generation</c> section of <c>Prism_Config.json</c>.
/// </summary>
internal sealed record Generate_Config
{
    /// <summary>Maximum number of non-KO images a family may have before generation is skipped.</summary>
    internal int MinImagesPerFamily   { get; init; }

    /// <summary>Minimum hero image width in pixels required for generation to proceed.</summary>
    internal int InputMinWidthPixels  { get; init; }

    /// <summary>Minimum hero image height in pixels required for generation to proceed.</summary>
    internal int InputMinHeightPixels { get; init; }
}
