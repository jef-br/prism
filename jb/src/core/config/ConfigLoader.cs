using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Prism.Core;

namespace Prism.Config;

/// <summary>
/// Generic section-aware JSON config loader for all PRISM services. Finds a config file by name
/// across the standard search locations, deserializes either the whole file or one named top-level
/// section with required-member enforcement (no in-code defaults — a missing or misspelled key fails
/// loud at load time), and caches the parsed result per (type, path, section, file timestamp) so an
/// edited file is re-parsed on next use while unchanged files parse once per process. Compiles into
/// the shared contracts assembly so engine projects can load their own config without referencing
/// Prism.Core.
/// <para>
/// Every config failure throws <see cref="PrismConfigurationException"/> — the single fail-loud type
/// for PRISM-owned configuration across the whole codebase (T-4560). It derives from
/// <see cref="InvalidOperationException"/>, so existing catch sites keep working.
/// </para>
/// </summary>
public static class ConfigLoader {
    private static readonly ConcurrentDictionary<string, object> cache = new();

    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static T Section<T>(string configFileName, string sectionName) where T : class {
        string path = RequireFile(configFileName);
        string key = BuildKey(typeof(T), path, sectionName);
        return (T)cache.GetOrAdd(key, _ => LoadSection<T>(path, sectionName));
    }

    public static T Root<T>(string configFileName) where T : class {
        string path = RequireFile(configFileName);
        string key = BuildKey(typeof(T), path, string.Empty);
        return (T)cache.GetOrAdd(key, _ => Materialize<T>(File.ReadAllText(path), path, null));
    }

    public static string RequireFile(string configFileName) {
        return FindFile(configFileName)
            ?? throw new PrismConfigurationException(
                $"Config file '{configFileName}' not found. Searched: {string.Join("; ", CandidatePaths(configFileName))}");
    }

    public static string? FindFile(string configFileName) {
        foreach (string candidate in CandidatePaths(configFileName))
            if (File.Exists(candidate)) return candidate;
        return null;
    }

    private static IEnumerable<string> CandidatePaths(string configFileName) {
        yield return Path.Combine(AppContext.BaseDirectory, "config", configFileName);
        yield return Path.Combine(AppContext.BaseDirectory, configFileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), "config", configFileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), "..", "core", "config", configFileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), "jb", "src", "core", "config", configFileName);
        // Dev/test convenience: the single source-tree copy, found by walking up from the binary.
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            yield return Path.Combine(dir.FullName, "jb", "src", "core", "config", configFileName);
    }

    private static T LoadSection<T>(string path, string sectionName) {
        using JsonDocument doc = ParseDocument(path);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new PrismConfigurationException($"Config file {path} must contain a JSON object at the root.");

        foreach (JsonProperty property in doc.RootElement.EnumerateObject())
            if (string.Equals(property.Name, sectionName, StringComparison.OrdinalIgnoreCase))
                return Materialize<T>(property.Value.GetRawText(), path, sectionName);

        string available = string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name));
        throw new PrismConfigurationException(
            $"Section '{sectionName}' not found in {path}. Available sections: {available}.");
    }

    private static JsonDocument ParseDocument(string path) {
        try {
            return JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        } catch (JsonException ex) {
            throw new PrismConfigurationException($"Config file {path} is not valid JSON: {ex.Message}", ex);
        }
    }

    private static T Materialize<T>(string json, string path, string? sectionName) {
        string origin = sectionName is null ? path : $"section '{sectionName}' of {path}";
        T config;
        try {
            config = JsonSerializer.Deserialize<T>(json, SerializerOptions)
                ?? throw new PrismConfigurationException($"Failed to deserialize {origin}.");
        } catch (JsonException ex) {
            throw new PrismConfigurationException($"Cannot load {origin}: {ex.Message}", ex);
        }

        if (config is IValidatableConfig validatable) validatable.Validate();
        return config;
    }

    private static string BuildKey(Type type, string path, string section) {
        StringBuilder key = new(type.FullName);
        key.Append('|').Append(path).Append('#').Append(section).Append('@');
        key.Append(File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L);
        return key.ToString();
    }
}
