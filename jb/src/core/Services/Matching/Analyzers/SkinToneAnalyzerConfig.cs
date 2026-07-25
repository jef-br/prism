using Prism.Config;

namespace Prism.Services.Matching;

/// <summary>
/// Multi-tone skin-detection YCbCr chrominance bounds for AnalyzerMath.IsSkinTone, bound from the
/// "SkinTone" section of analyzer_Config.json. No defaults — every value must be present in the JSON
/// or deserialization fails loud.
/// </summary>
public sealed class SkinToneAnalyzerConfig : IValidatableConfig {
    /// <summary>Minimum luma ([0,1]) for a pixel to be considered skin.</summary>
    public required float LumaMin { get; init; }

    /// <summary>Maximum luma ([0,1]) for a pixel to be considered skin.</summary>
    public required float LumaMax { get; init; }

    /// <summary>Minimum Cb chrominance ([0,1]) for a pixel to be considered skin.</summary>
    public required float CbMin { get; init; }

    /// <summary>Maximum Cb chrominance ([0,1]) for a pixel to be considered skin.</summary>
    public required float CbMax { get; init; }

    /// <summary>Minimum Cr chrominance ([0,1]) for a pixel to be considered skin.</summary>
    public required float CrMin { get; init; }

    /// <summary>Maximum Cr chrominance ([0,1]) for a pixel to be considered skin.</summary>
    public required float CrMax { get; init; }

    public void Validate() {
        List<string> problems = [];

        if (this.LumaMin >= this.LumaMax) problems.Add("SkinTone.LumaMin must be < LumaMax");
        if (this.CbMin >= this.CbMax) problems.Add("SkinTone.CbMin must be < CbMax");
        if (this.CrMin >= this.CrMax) problems.Add("SkinTone.CrMin must be < CrMax");

        if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
    }
}
