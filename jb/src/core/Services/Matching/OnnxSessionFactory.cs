using Microsoft.ML.OnnxRuntime;

namespace Prism.Services.Matching;

/// <summary>
/// Sole construction path for every ONNX <see cref="InferenceSession"/> in PRISM (T-4110). CPU is the
/// mandatory baseline — DirectML is appended automatically when <see cref="GpuProbe"/> finds a hardware
/// adapter, otherwise the session runs on CPU. Every model-running component (CLIP, YOLO, Upscale, and
/// any future analyzer/transformer) must call <see cref="Create"/> instead of constructing
/// <see cref="SessionOptions"/>/<see cref="InferenceSession"/> directly — enforced by the conventions hook.
/// </summary>
internal static class OnnxSessionFactory {
    internal static InferenceSession Create(string modelPath) {
        var opts = new SessionOptions();
        if (GpuProbe.HasHardwareDirectMLAdapter())
            opts.AppendExecutionProvider_DML(0);
        return new InferenceSession(modelPath, opts);
    }
}
