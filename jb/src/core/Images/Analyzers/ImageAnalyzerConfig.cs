using System.Text.Json;

namespace Prism.Core;

/// <summary>Typed configuration loaded from cfg_ImageAnalyzer.json. One instance per pipeline run.</summary>
internal sealed class ImageAnalyzerConfig
{
    public float MinSkinPixelRatio { get; private set; }
    public float SkinHueMin1 { get; private set; }
    public float SkinHueMax1 { get; private set; }
    public float SkinHueMin2 { get; private set; }
    public float SkinHueMax2 { get; private set; }
    public float SkinSatMin { get; private set; }
    public float SkinSatMax { get; private set; }
    public float SkinValMin { get; private set; }
    public float SkinValMax { get; private set; }

    /// <summary>Loads and validates cfg_ImageAnalyzer.json from the given path.</summary>
    internal static ImageAnalyzerConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new PrismConfigurationException($"cfg_ImageAnalyzer.json not found at: {path}");

        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        using (doc)
        {
            JsonElement root = doc.RootElement;
            JsonElement h = root.GetProperty("HasHuman");
            return new ImageAnalyzerConfig
            {
                MinSkinPixelRatio = (float)h.GetProperty("MinSkinPixelRatio").GetDouble(),
                SkinHueMin1       = (float)h.GetProperty("SkinHueMin1").GetDouble(),
                SkinHueMax1       = (float)h.GetProperty("SkinHueMax1").GetDouble(),
                SkinHueMin2       = (float)h.GetProperty("SkinHueMin2").GetDouble(),
                SkinHueMax2       = (float)h.GetProperty("SkinHueMax2").GetDouble(),
                SkinSatMin        = (float)h.GetProperty("SkinSatMin").GetDouble(),
                SkinSatMax        = (float)h.GetProperty("SkinSatMax").GetDouble(),
                SkinValMin        = (float)h.GetProperty("SkinValMin").GetDouble(),
                SkinValMax        = (float)h.GetProperty("SkinValMax").GetDouble()
            };
        }
    }
}
