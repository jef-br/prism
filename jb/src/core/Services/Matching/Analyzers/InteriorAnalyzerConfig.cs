using Prism.Config;

namespace Prism.Services.Matching;

/// <summary>
/// Thresholds for Analyzer_Interior, bound from the "Interior" section of analyzer_Config.json.
/// No defaults — every value must be present in the JSON or deserialization fails loud.
/// </summary>
public sealed class InteriorAnalyzerConfig : IValidatableConfig
{
    /// <summary>Minimum fraction of image area an interior region must cover.</summary>
    public required float MinAreaFraction { get; init; }

    /// <summary>Edge strength threshold on the [0,1] gradient scale (30/255 by default).</summary>
    public required float MinEdgeStrength { get; init; }

    /// <summary>Interior texture must be at least this much smoother than its surroundings.</summary>
    public required float TextureDiffMin { get; init; }

    public void Validate()
    {
        List<string> problems = [];

        if (MinAreaFraction is <= 0f or >= 1f) problems.Add("Interior.MinAreaFraction must be in (0,1)");
        if (MinEdgeStrength <= 0f) problems.Add("Interior.MinEdgeStrength must be > 0");
        if (TextureDiffMin <= 0f) problems.Add("Interior.TextureDiffMin must be > 0");

        if (problems.Count > 0) throw new InvalidOperationException(string.Join("; ", problems));
    }
}
