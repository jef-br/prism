# Upscale — open decisions

## Should Real-ESRGAN run on CPU when no GPU is present?

- **Question:** Upscale is the only model-running component that never loads its model without a GPU. `UpscaleService.Create` initializes Real-ESRGAN only when `ImageUpscaler.IsGpuAvailable`; CPU-only boxes silently get Lanczos4 capped ×1.42 instead (`Upscaler_c_p_u`). CLIP and YOLO, by contrast, run their models on CPU via `OnnxSessionFactory` when no adapter exists (T-4110).
- **Status quo rationale:** documented policy says "No GPU→CPU fallback path required — CPU-only is the supported configuration," and tiled Real-ESRGAN (64×64 tiles, ×2) on the CPU EP is likely far too slow for batch pipelines — possibly minutes per image.
- **What changed:** before T-4110 this was a technical constraint (`Upscaler_g_p_u` appended DML unconditionally, so a CPU session wasn't even possible). Now the factory is EP-agnostic, so CPU Real-ESRGAN is one gate change away: drop the `IsGpuAvailable` conditions in `UpscaleService.Create` and `ImageUpscaler.Upscale` and the same session runs on CPU. The blocker is purely the speed/quality trade, which is a product call.
- **Options:** (a) keep as is — accept the quality gap on CPU-only hosts, document it as deliberate in `PRISM-model-runtime.md`; (b) run the model on CPU always — same output everywhere, measure per-image cost on a representative batch first; (c) config flag (e.g. `Upscale.AllowCpuModel`) defaulting to off.
- **Recommendation:** (a) unless a real CPU-only deployment complains about upscale quality; if measured, record the per-image CPU timing here before deciding.
- **Answer:**
