using System.Collections.Generic;

namespace Prism.Api;

/// <summary>
/// API request JSON shape sent in the multipart request part.
/// </summary>
internal sealed record PrismProcessRequest
{
    public string? ClientRequestToken { get; init; }
    public bool Rename { get; init; } = true;
    public bool Transform { get; init; } = true;
    public bool Generation { get; init; } = true;
    public string Format { get; init; } = "zip";
    public bool ReturnOriginalImages { get; init; }
    public bool SkipClassification { get; init; }
    public IReadOnlyList<string> Input { get; init; } = [];
}
