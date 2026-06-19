using System.Text.Json;

/// <summary>
/// Loads <c>ClipPrompts.json</c> and maps each natural-language CLIP prompt to the feature id and
/// value it represents. Replaces the former hard-coded prompt dictionary in the Classified stage —
/// prompts and their feature/value bindings can now change by editing JSON, no recompilation.
/// </summary>
public sealed class ClipPromptCatalog
{
    // prompt label → (feature id, feature value)
    private readonly Dictionary<string, (string Feature, string Value)> byPrompt;

    private ClipPromptCatalog(Dictionary<string, (string Feature, string Value)> byPrompt)
    {
        this.byPrompt = byPrompt;
    }

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads and parses <c>ClipPrompts.json</c>.
    /// Throws <see cref="InvalidOperationException"/> on a missing file or bad structure.
    /// </summary>
    /// <param name="jsonPath">Absolute path to ClipPrompts.json.</param>
    public static ClipPromptCatalog Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new InvalidOperationException($"ClipPrompts.json not found at: {jsonPath}");

        string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);

        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex) {
            throw new InvalidOperationException($"Failed to parse ClipPrompts.json at '{jsonPath}': {ex.Message}", ex);
        }

        using (doc) {
            if (!doc.RootElement.TryGetProperty("prompts", out JsonElement promptsEl) || promptsEl.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"ClipPrompts.json at '{jsonPath}' is missing required 'prompts' array.");

            var map = new Dictionary<string, (string Feature, string Value)>();
            foreach (JsonElement entry in promptsEl.EnumerateArray()) {
                string prompt = entry.TryGetProperty("prompt", out JsonElement p) ? p.GetString() ?? "" : "";
                string feature = entry.TryGetProperty("feature", out JsonElement f) ? f.GetString() ?? "" : "";
                string value = entry.TryGetProperty("value", out JsonElement v) ? v.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(prompt))
                    throw new InvalidOperationException($"ClipPrompts.json at '{jsonPath}' has a prompt entry with no 'prompt' text.");

                map[prompt] = (feature, value);
            }

            return new ClipPromptCatalog(map);
        }
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>All prompt strings to feed the CLIP zero-shot classifier.</summary>
    public string[] BuildPrompts()
        => [.. byPrompt.Keys];

    /// <summary>
    /// Maps a CLIP result label back to its feature id and value.
    /// Returns false for an unrecognised label.
    /// </summary>
    public bool TryResolve(string label, out string feature, out string value)
    {
        if (byPrompt.TryGetValue(label, out (string Feature, string Value) mapping)) {
            feature = mapping.Feature;
            value = mapping.Value;
            return true;
        }

        feature = string.Empty;
        value = string.Empty;
        return false;
    }
}
