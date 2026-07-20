using Microsoft.ML.OnnxRuntime;
using Prism.Services.Upscale;

namespace Prism.Api;

/// <summary>
/// Reports the truth about ONNX Runtime execution providers on this host: the providers compiled into
/// the loaded runtime, and the provider each PRISM inference session actually opens with. Replaces the
/// former hardcoded ["CPU"] health value (T-4100).
/// </summary>
internal static class RuntimeProviderProbe {
    // Execution providers compiled into the loaded ONNX Runtime build. The DirectML package ships
    // DmlExecutionProvider + CPUExecutionProvider; a CPU-only package would report only CPU.
    internal static IReadOnlyList<string> AvailableProviders() {
        try {
            return OrtEnv.Instance().GetAvailableProviders();
        } catch {
            // Graceful degradation: if the ORT env can't be queried, report the always-present CPU EP
            // rather than failing the health endpoint — never let provider reporting break readiness.
            return ["CPUExecutionProvider"];
        }
    }

    // What each session opens with: CLIP, YOLO, and Upscale all construct their InferenceSession via the
    // shared OnnxSessionFactory (T-4110), which binds to the GPU only when a hardware DX12 adapter is
    // present (Upscaler.IsGpuAvailable probes the same GpuProbe check); otherwise all three run CPU.
    internal static IReadOnlyList<string> SessionProviders() {
        string ep = Upscaler.IsGpuAvailable ? "DirectML(GPU)" : "CPU";
        return [$"CLIP={ep}", $"YOLO={ep}", $"Upscale={ep}"];
    }
}
