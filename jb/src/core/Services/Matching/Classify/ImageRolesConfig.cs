using System.Text.Json.Serialization;

namespace Prism.Services.Matching;

/// <summary>
/// Top-level structure of <c>ImageRoles.json</c>.
/// </summary>
public sealed class ImageRolesConfig {
    /// <summary>
    /// Ordered list of phenotype rules. Evaluation stops at the first matching rule.
    /// Order in this list determines priority: more specific phenotypes come first.
    /// </summary>
    [JsonPropertyName("phenotypes")]
    public PhenotypeRule[] Phenotypes { get; init; } = [];
}
