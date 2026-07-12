namespace Prism.Config;

/// <summary>
/// Resolves large model assets (ONNX models, tokenizer files) that are deliberately not copied into
/// every build output. Resolution order: beside the deployed config (a production deployment ships
/// the model with Prism_Config.json), then the PRISM_ONNX_MODEL_DIR environment override, then the
/// single source-tree copy under jb/src/core/ found by walking up from the binary — a dev/test
/// convenience so multi-MB models are never duplicated per project or per test run.
/// </summary>
public static class ModelAssetLocator {
    public static string? Find(string relativePathFromCore) {
        string? configPath = ConfigLoader.FindFile("Prism_Config.json");
        if (configPath is not null) {
            string besideConfig = Path.Combine(Path.GetDirectoryName(configPath)!, relativePathFromCore);
            if (File.Exists(besideConfig)) return besideConfig;
        }

        string? modelRoot = Environment.GetEnvironmentVariable("PRISM_ONNX_MODEL_DIR");
        if (!string.IsNullOrWhiteSpace(modelRoot)) {
            string overridden = Path.Combine(modelRoot, relativePathFromCore);
            if (File.Exists(overridden)) return overridden;
        }

        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) {
            string candidate = Path.Combine(dir.FullName, "jb", "src", "core", relativePathFromCore);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
