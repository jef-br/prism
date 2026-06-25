# Upscale Todo

-------
- [ ] Implement Real-ESRGAN x2plus GPU path in `Upscaler_g_p_u.cs`.
  - File: `jb/src/core/Images/Upscale/Upscaler_g_p_u.cs`
  - Block: `RunRealEsrgan()` throws `NotImplementedException`. Three things are missing:
    1. **NuGet** — add `Microsoft.ML.OnnxRuntime.DirectML` to `Prism.Core.Images.Upscale.csproj`.
    2. **Model file** — download `real-esrgan-x2plus.onnx` and add its path to the upscale config (or `Prism_Config.json`).
    3. **Inference body** — implement `RunRealEsrgan`: decode image bytes to BGR float [0,1] → reshape NCHW [1,3,H,W] → `_session.Run(inputs)` → output [1,3,H×2,W×2] → clamp [0,1] → encode JPG. The commented-out `_session` / `CreateSession` in the file show the DirectML session setup.
  - Impact:
    - Project progress: Medium — GPU upscaling is fully stubbed and gated; CPU fallback (`Upscaler_c_p_u.cs`) is active until this is wired. Completing it enables faster, higher-quality upscales on machines with a DirectML-compatible GPU.
    - Effect on other TODOs: None known — `GpuProbe.cs` already routes to the correct path.
  - Recommended solution:
    Add the NuGet package, place the `.onnx` model file in the configured assets directory, then implement `RunRealEsrgan` following the step-by-step comment already in the file. Uncomment the `InferenceSession` field and `CreateSession` helper once the package is present. Keep the Lanczos4 top-up in `ApplyLanczos4` unchanged.
