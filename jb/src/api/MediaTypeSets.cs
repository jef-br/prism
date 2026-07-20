using System.Collections.Generic;

namespace Prism.Api;

/// <summary>
/// Accepted input extensions grouped by record type, sourced from Prism_Config.json.
/// </summary>
internal sealed record MediaTypeSets(HashSet<string> Images, HashSet<string> Excel, HashSet<string> Zip);
