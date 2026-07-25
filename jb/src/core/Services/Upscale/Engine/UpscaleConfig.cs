using System.Text.Json;

namespace Prism.Services.Upscale;

/// <summary>Typed configuration loaded from cfg_Upscale.json — tile overlap and discard-band sizing for Upscaler.RunTiled.</summary>
internal sealed record UpscaleConfig {
    /// <summary>Source pixels of overlap reserved on each side of an internal tile seam for the discard band plus the blend ramp.</summary>
    public int TileOverlapPixels { get; init; }

    /// <summary>Source pixels nearest each internal seam, within the overlap, that are fully discarded before blending starts.</summary>
    public int DiscardBandPixels { get; init; }

    /// <summary>Loads and parses cfg_Upscale.json from the given path.</summary>
    internal static UpscaleConfig Load(string configPath) {
        if (!File.Exists(configPath))
            throw new PrismConfigurationException($"cfg_Upscale.json was not found at: {configPath}");

        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(configPath));
        using (doc) {
            JsonElement tiling = doc.RootElement.GetProperty("Tiling");
            return new UpscaleConfig {
                TileOverlapPixels = tiling.GetProperty("TileOverlapPixels").GetInt32(),
                DiscardBandPixels = tiling.GetProperty("DiscardBandPixels").GetInt32()
            };
        }
    }
}
