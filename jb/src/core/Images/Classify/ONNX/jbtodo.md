# ONNX Todo

- [ ] Define ONNX model ownership rules: say which class loads model files and checks that required assets exist.
  - Impact:
    - Project progress: High - Model ownership determines readiness, diagnostics, and failure behavior for vision features.
    - Effect on other TODOs: Blocks - It gates model provenance, checksum validation, runtime provider policy, and health reporting.
  - Industry standard:
    ML inference services centralize model loading, validation, and asset checks in a dedicated provider rather than scattering file access through business logic.
  - Recommended solution:
    Create an ONNX model provider/registry class that owns asset discovery, validation, session creation, and readiness reporting.
  - Answer:

- [ ] Define ONNX session lifetime policy: say whether sessions are shared per application, per batch, or per worker.
  - Impact:
    - Project progress: High - Session lifetime controls startup cost, memory pressure, and concurrency behavior.
    - Effect on other TODOs: Unblocks - It affects resource initialization/disposal, runtime provider policy, and fallback behavior.
  - Industry standard:
    Inference pipelines reuse expensive model sessions across jobs when models are immutable and thread-safety is understood, while keeping per-job state separate.
  - Recommended solution:
    Keep ONNX sessions application-scoped and reusable, with per-batch input/output buffers isolated by worker.
  - Answer:

- [ ] Define ONNX runtime provider policy: say whether CPU is always supported and when GPU providers may be used.
  - Impact:
    - Project progress: High - Provider policy determines portability across laptops, servers, and future GPU workers.
    - Effect on other TODOs: Influences - It affects fallback behavior, health checks, and performance diagnostics.
  - Industry standard:
    Production inference supports CPU as a baseline and enables GPU providers through explicit configuration and readiness checks.
  - Recommended solution:
    Require CPU support, allow configured GPU providers when available, and fail readiness only if no configured required provider can run.
  - Answer:

- [ ] Define ONNX fallback behavior without GPU: say how Prism behaves on local servers and laptops with CPU only.
  - Impact:
    - Project progress: Medium - CPU fallback improves developer and desktop usability while preserving output semantics.
    - Effect on other TODOs: Influences - It ties into provider policy, health reporting, and performance expectations.
  - Industry standard:
    ML pipelines degrade to CPU for correctness when latency is acceptable, and surface performance warnings rather than silently disabling inference.
  - Recommended solution:
    Run on CPU when GPU is unavailable, emit a diagnostic warning, and keep model-dependent stages enabled unless CPU performance exceeds configured limits.
  - Answer:

- [ ] Define ONNX diagnostic logging policy: say when tensor names, shapes, scores, and timing are logged.
  - Impact:
    - Project progress: Medium - Diagnostic logging makes inference failures debuggable without bloating normal batch output.
    - Effect on other TODOs: Influences - It supports model asset validation, prompt thresholds, workbench diagnostics, and health checks.
  - Industry standard:
    Inference services log model metadata and timing at startup or debug level, while per-item scores are sampled or attached only to bounded diagnostics.
  - Recommended solution:
    Log tensor names/shapes at model load, stage timing per batch, and per-image scores only when diagnostics are enabled.
  - Answer:
