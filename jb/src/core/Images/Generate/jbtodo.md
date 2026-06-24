# Generated Stage Todo

-------
- [ ] Implement image generation backend: the Generated stage must produce real output images.
  - File: `jb/src/core/Images/Generate/ImageGenerator.cs` — `GenerationBackendAvailable()` returns `false`; all qualified families receive `GenerationStatus.Gated` and no images are produced.
  
- Current behavior: The generation decision shell correctly identifies which families qualify (below `MinImagesPerFamily` threshold, hero meets minimum quality). No inference runs. Every generated record has `Status = Gated`.
  - Required: Implement the inference call inside the `if (GenerationBackendAvailable())` block. Connect `GenerationBackendAvailable()` to a real health check. Download the result image, write it to a temp path, and populate `ImageRecord_GENERATED` with a real `OutputPath`, `Status = Ok`, and generation metadata.
  - Recommended on-premises GenAI options:
    - **ComfyUI + Flux.1-schnell** *(recommended first choice)*: Open weights, runs on-prem. Flux.1-schnell is the fast variant (4-step distillation), suitable for production throughput. Requires ≥ 12 GB VRAM. ComfyUI exposes a REST API (`POST /prompt`, `GET /history/{prompt_id}`, `GET /view?filename=...`) that PRISM can call via `HttpClient`. No data leaves the building.
    - **ComfyUI + Stable Diffusion XL** *(broader hardware support)*: Runs on ≥ 8 GB VRAM. Lower visual quality than Flux for photorealistic product images but wider hardware compatibility. Same ComfyUI REST API surface as Flux. Good fallback when GPU budget is constrained.
    - **ComfyUI + Flux.1-dev** *(highest quality, highest cost)*: Full Flux model, open weights. Requires ≥ 24 GB VRAM. Best output quality for realistic product photography, but impractical for batch throughput without a dedicated high-VRAM server.
    - **AUTOMATIC1111 (stable-diffusion-webui)**: Alternative to ComfyUI, exposes a REST API at `/sdapi/v1/txt2img` and `/sdapi/v1/img2img`. Supports the same SDXL and SD 1.5 checkpoints. Less flexible than ComfyUI for workflow composition but simpler API surface.
  - Recommended integration approach:
    1. Add a `Generation.Backend` section to `Prism_Config.json`: endpoint URL, model name, timeout seconds, max retries.
    2. Implement `GenerationBackendAvailable()` as an HTTP GET to the ComfyUI `/system_stats` endpoint with a short timeout (≤ 2 s).
    3. Build a `ComfyUiClient` class (separate from `ImageGenerator`) that submits a workflow JSON, polls `/history/{id}` until completion, and downloads the output image.
    4. Map the downloaded image to `ImageRecord_GENERATED` with `Status = Ok`, `OutputPath` set to the temp file, and `Method = DetailCrop` or the appropriate generation method.
    5. On timeout or error: set `Status = Ko`, record the failure reason, do not KO the whole job.

  - Fix: After selecting a backend, implement `ComfyUiClient`, wire it into `ImageGenerator.Run`, replace `GenerationBackendAvailable() => false` with the health check, and add integration tests using a local ComfyUI instance.
