# Upscale Todo

-------
- [ ] Implement Real-ESRGAN x2plus GPU path in `Upscaler_g_p_u.cs`.
  - File: `jb/src/core/Images/Upscale/Upscaler_g_p_u.cs`
  - Block: `RunRealEsrgan()` throws `NotImplementedException`. Two items remain:
    1. **Model loading** — resolve `Images/Upscale/ONNX/Real-ESRGAN_x2plus.onnx` via `PrismConfigLocator.FindModelAsset()` (same pattern as `ClassificationService.cs` lines 144–151) and pass the path into a static `InferenceSession` with DirectML EP.
    2. **Inference body** — implement `RunRealEsrgan`: decode image bytes to BGR float [0,1] → reshape NCHW [1,3,H,W] → `_session.Run(inputs)` → output [1,3,H×2,W×2] → clamp [0,1] → encode JPG. The commented-out `_session` / `CreateSession` in the file show the DirectML session setup.
    ~~NuGet (`Microsoft.ML.OnnxRuntime.DirectML`) — already added.~~
  - Impact:
    - Project progress: Medium — GPU upscaling is fully stubbed and gated; CPU fallback (`Upscaler_c_p_u.cs`) is active until this is wired. Completing it enables faster, higher-quality upscales on machines with a DirectML-compatible GPU.
    - Effect on other TODOs: None known — `GpuProbe.cs` already routes to the correct path.
  - Recommended solution:
    Add the NuGet package, place the `.onnx` model file in the configured assets directory, then implement `RunRealEsrgan` following the step-by-step comment already in the file. Uncomment the `InferenceSession` field and `CreateSession` helper once the package is present. Keep the Lanczos4 top-up in `ApplyLanczos4` unchanged.

  - ANSWER: 
    - The NuGet package has been added.
    - The upscaler onnx model has to be loaded the same way jb\src\core\Services\ClassificationService.cs loads its onnx model. This happens at lines 138-155
    - The ESRGAN is located here: jb\src\core\Images\Upscale\ONNX\Real-ESRGAN_x2plus.onnx
    - inference body has to be implemented.
    - implement `RunRealEsrgan` following the step-by-step comment already in the file.
      - `RunRealEsrgan`: decode image bytes to BGR float [0,1] → reshape NCHW [1,3,H,W] → `_session.Run(inputs)` → output [1,3,H×2,W×2] → clamp [0,1] → encode JPG. The commented-out `_session` / `CreateSession` in the file show the DirectML session setup.
    - Uncomment the `InferenceSession` field and `CreateSession` helper.
    - Keep the Lanczos4 top-up in `ApplyLanczos4` unchanged.
    - ONNX tensor names: input = `"input"`, output = `"output"`.
    - Session init follows the classify pattern: (`jb\src\core\Services\ClassificationService.cs`: Line 138 and onward ) add a static `Initialize(string modelPath)` to `Upscaler_g_p_u`, called from the service layer; throw `PrismConfigurationException` if `FindModelAsset` returns null.
    - `UpscaleService.cs` is currently a thin passthrough (`jb\src\core\Services\UpscaleService.cs`) with no model init. It needs a `Create()` factory (mirroring `ClassificationService.Create()`) that calls `FindModelAsset("Images/Upscale/ONNX/Real-ESRGAN_x2plus.onnx")`, throws `PrismConfigurationException` if null, and calls `Upscaler_g_p_u.Initialize(modelPath)` — but only when `GpuProbe.HasHardwareDirectMLAdapter()` is true (CPU path needs no session).