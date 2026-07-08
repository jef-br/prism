using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prism.Lib.Ingress;

/// <summary>
/// Typed representation of HostRules.json.
/// Controls which URL schemes, hosts, and network ranges are permitted during remote fetch operations.
/// </summary>
internal sealed record HostRules_Config
{
    internal string[] AllowedSchemes { get; init; } = [];
    internal string[] BlockedSchemes { get; init; } = [];
    internal string[] BlockedHostPatterns { get; init; } = [];
    internal bool AllowGenericDirectFileRedirects { get; init; }
    internal bool AllowFetcherOwnedRedirects { get; init; }
    internal bool AllowPrivate { get; init; }
    internal bool AllowLinkLocal { get; init; }
    internal bool AllowLoopback { get; init; }
    internal bool RejectAnyLoopbackDnsResult { get; init; }
    internal int ConnectSeconds { get; init; }
    internal int ResponseHeaderSeconds { get; init; }
    internal int IdleReadSeconds { get; init; }
    internal int TotalFetchSeconds { get; init; }
    internal bool AllowLocalhost { get; init; }

    /// <summary>
    /// Loads and parses HostRules.json from <paramref name="configDirectory"/>.
    /// Throws <see cref="PrismConfigurationException"/> if the file is absent or malformed.
    /// </summary>
    internal static HostRules_Config Load(string configDirectory)
    {
        string path = Path.Combine(configDirectory, "HostRules.json");

        if (!File.Exists(path)) {
            throw new PrismConfigurationException($"HostRules.json was not found at: {path}");
        }

        string json;
        try {
            json = File.ReadAllText(path);
        } catch (Exception ex) {
            throw new PrismConfigurationException($"HostRules.json could not be read at: {path}", ex);
        }

        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(json);
        } catch (JsonException ex) {
            throw new PrismConfigurationException($"HostRules.json is not valid JSON: {ex.Message}", ex);
        }

        using (doc) {
            return Parse(doc.RootElement, path);
        }
    }

    private static HostRules_Config Parse(JsonElement root, string path)
    {
        HostRules_Config cfg = new();

        cfg = cfg with {
            AllowedSchemes             = ReadStringArray(root, path, "allowedSchemes"),
            BlockedSchemes             = ReadStringArray(root, path, "blockedSchemes"),
            BlockedHostPatterns        = ReadStringArray(root, path, "blockedHostPatterns"),
            AllowGenericDirectFileRedirects = ReadBool(root, path, "redirects", "allowGenericDirectFileRedirects"),
            AllowFetcherOwnedRedirects = ReadBool(root, path, "redirects", "allowFetcherOwnedRedirects"),
            AllowPrivate               = ReadBool(root, path, "networkRanges", "allowPrivate"),
            AllowLinkLocal             = ReadBool(root, path, "networkRanges", "allowLinkLocal"),
            AllowLoopback              = ReadBool(root, path, "networkRanges", "allowLoopback"),
            RejectAnyLoopbackDnsResult = ReadBool(root, path, "networkRanges", "rejectAnyLoopbackDnsResult"),
            ConnectSeconds             = ReadInt(root, path, "timeouts", "connectSeconds"),
            ResponseHeaderSeconds      = ReadInt(root, path, "timeouts", "responseHeaderSeconds"),
            IdleReadSeconds            = ReadInt(root, path, "timeouts", "idleReadSeconds"),
            TotalFetchSeconds          = ReadInt(root, path, "timeouts", "totalFetchSeconds"),
            AllowLocalhost             = ReadBool(root, path, "testing", "allowLocalhost")
        };

        return cfg;
    }

    private static string[] ReadStringArray(JsonElement root, string path, string key)
    {
        if (!root.TryGetProperty(key, out JsonElement el) || el.ValueKind != JsonValueKind.Array) {
            return [];
        }

        List<string> values = [];
        foreach (JsonElement item in el.EnumerateArray()) {
            if (item.ValueKind == JsonValueKind.String) {
                values.Add(item.GetString()!);
            }
        }

        return [.. values];
    }

    private static bool ReadBool(JsonElement root, string path, string section, string key)
    {
        if (!root.TryGetProperty(section, out JsonElement sec)) {
            throw new PrismConfigurationException($"HostRules.json at '{path}': missing section '{section}'.");
        }

        if (!sec.TryGetProperty(key, out JsonElement el)
            || (el.ValueKind != JsonValueKind.True && el.ValueKind != JsonValueKind.False)) {
            throw new PrismConfigurationException($"HostRules.json at '{path}': missing or invalid boolean '{section}.{key}'.");
        }

        return el.GetBoolean();
    }

    private static int ReadInt(JsonElement root, string path, string section, string key)
    {
        if (!root.TryGetProperty(section, out JsonElement sec)) {
            throw new PrismConfigurationException($"HostRules.json at '{path}': missing section '{section}'.");
        }

        if (!sec.TryGetProperty(key, out JsonElement el)
            || el.ValueKind != JsonValueKind.Number
            || !el.TryGetInt32(out int val)) {
            throw new PrismConfigurationException($"HostRules.json at '{path}': missing or invalid integer '{section}.{key}'.");
        }

        return val;
    }
}
