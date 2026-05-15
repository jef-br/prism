# Image Transform Todo

- [ ] Define salient object bounds output: specify the bounding box fields produced by preprocessing for the main object.
  - Impact:
    - Project progress: High - Object bounds are the core geometry input for crop, resize, fill, and border decisions.
    - Effect on other TODOs: Blocks - It gates crop decisions, border anchors, center-and-stretch behavior, and transform result fields.
  - Industry standard:
    Image preprocessing stages emit normalized bounding boxes with pixel coordinates, confidence, source method, and edge-contact metadata.
  - Recommended solution:
    Emit pixel `x`, `y`, `width`, `height`, normalized coordinates, confidence, detection method, and border intersection flags.
  - Answer:

- [ ] Define background identification output: say how preprocessing describes background color, flatness, and confidence.
  - Impact:
    - Project progress: High - Background identification controls fill, cleanup, and product centering quality.
    - Effect on other TODOs: Blocks - It informs background fill policy, center-and-stretch extension, cleanup, and diagnostics.
  - Industry standard:
    Product image pipelines classify background color, variance, transparency/matte, and confidence before attempting synthetic extension.
  - Recommended solution:
    Emit dominant background color, flatness score, variance, sampled regions, confidence, and whether fill is safe.
  - Answer:

- [ ] Define image type output: say which transform-facing type values are emitted before crop and center logic runs.
  - Impact:
    - Project progress: High - Transform rules depend on whether an image is packshot, clothing, detail, ambiance, or unknown.
    - Effect on other TODOs: Blocks - It links classification values to crop behavior, fill policy, and fallback rules.
  - Industry standard:
    Transform engines consume bounded type labels from classification rather than re-inferring product semantics inside crop code.
  - Recommended solution:
    Pass the canonical image type enum and confidence from classification into transform decisions.
  - Answer:

- [ ] Define transform failure cases: list which transform problems become KO instead of producing an output image.
  - Impact:
    - Project progress: High - Failure policy decides when visual quality is too risky to export.
    - Effect on other TODOs: Blocks - It affects KO reasons, TransformResult, manifest projection, and JSON/zip status.
  - Industry standard:
    Batch image processors distinguish recoverable quality warnings from unrecoverable transform failures and record per-item KO instead of crashing the batch.
  - Recommended solution:
    KO images when decode/normalized input is invalid, object bounds are unusable for required transforms, required output size cannot be met, or fill/crop would create unacceptable artifacts.
  - Answer:

- [ ] Define crop decision output: say how crop coordinates, anchors, and confidence are represented.
  - Impact:
    - Project progress: High - Crop decisions are the main transform artifact that diagnostics and output generation need.
    - Effect on other TODOs: Unblocks - It feeds TransformResult, detail crop behavior, center-and-stretch fallback, and workbench snapshots.
  - Industry standard:
    Transform stages record requested crop, clamped crop, anchor rules, confidence, and warnings so outputs can be audited.
  - Recommended solution:
    Store crop rectangle, anchor edges, rule name, confidence, clamping/fill requirements, and warnings.
  - Answer:

- [ ] Define background fill policy: choose the allowed methods such as edge extension, blur, inpainting, local generation, or solid fill.
  - Impact:
    - Project progress: High - Fill policy controls visual quality, compute cost, and whether external dependencies are needed.
    - Effect on other TODOs: Blocks - It gates detail crop fill, center-and-stretch extension, cleanup, and KO rejection.
  - Industry standard:
    Product-image pipelines use deterministic cheap fills first, then controlled inpainting/generation only when allowed and traceable.
  - Recommended solution:
    Allow edge extension, local blur/clone, solid fill, and optional local inpainting; do not rely on external SaaS generation for core transforms.
  - Answer:

- [ ] Define KO rejection policy for failed background fill: say which fill failures still export and which become KO.
  - Impact:
    - Project progress: High - Fill failure policy protects output quality without stopping the batch.
    - Effect on other TODOs: Unblocks - It affects TransformResult failure reason, manifest KO groups, and output status.
  - Industry standard:
    Media pipelines export with warnings only when artifacts are acceptable under quality thresholds; otherwise they send the item to KO.
  - Recommended solution:
    Export with warning for minor fill imperfections, KO when required output size/margins cannot be achieved or artifacts exceed configured quality thresholds.
  - Answer:

- [ ] Define resize decision output: say how preprocessor reports upscale, downscale, or no-resize decisions.
  - Impact:
    - Project progress: Medium - Resize metadata supports quality control and manifest diagnostics.
    - Effect on other TODOs: Influences - It affects TransformResult, output dimensions, config limits, and workbench display.
  - Industry standard:
    Image transforms record input size, target size, scale factor, interpolation method, and whether quality limits were exceeded.
  - Recommended solution:
    Emit resize mode, scale factor, input/output dimensions, interpolation method, and warning when configured upscale/downscale limits are approached.
  - Answer:

- [ ] Define classification tag output: say which labels from matching/classification are available to transform decisions.
  - Impact:
    - Project progress: Medium - Tags improve transform choices but should not replace core geometry.
    - Effect on other TODOs: Influences - It connects image-label outputs, classification traits, and transform decisions.
  - Industry standard:
    Pipelines pass selected semantic labels with confidence into transform stages as optional decision modifiers.
  - Recommended solution:
    Expose bounded tags for image type, orientation, human/head visibility, product color/material hints, and background traits.
  - Answer:

- [ ] Define detail crop edge anchor behavior: say how touched image borders lock crop coordinates.
  - Impact:
    - Project progress: Medium - Edge anchors prevent cropped subjects from being incorrectly centered away from true edges.
    - Effect on other TODOs: Influences - It depends on border intersection detection and feeds crop decision output.
  - Industry standard:
    Crop systems preserve edge contact when content is intentionally or inherently clipped at the source boundary.
  - Recommended solution:
    Lock crop movement away from any touched edge unless fill policy explicitly allows extending that side.
  - Answer:

- [ ] Define detail crop saliency map behavior: say how the most important object region influences square crop placement.
  - Impact:
    - Project progress: Medium - Saliency improves detail crops but depends on object bounds and crop policy.
    - Effect on other TODOs: Influences - It affects crop anchors, greedy crop behavior, and transform diagnostics.
  - Industry standard:
    Detail crop algorithms use saliency or attention maps to keep the highest-value region inside the target aspect ratio.
  - Recommended solution:
    Center the square crop on the dominant saliency region while respecting edge anchors and minimum content retention.
  - Answer:

- [ ] Define detail crop headcut behavior: say how the optional headcut mode changes top crop placement.
  - Impact:
    - Project progress: Medium - Headcut rules affect fashion/clothing outputs and must be predictable.
    - Effect on other TODOs: Influences - It depends on human/head traits, top-edge intersection, and crop anchors.
  - Industry standard:
    Apparel pipelines make person-specific crop rules explicit and gated by confidence to avoid accidental face/head removal.
  - Recommended solution:
    Apply headcut only when configured and human/head evidence meets threshold; otherwise preserve the detected head region.
  - Answer:

- [ ] Define detail crop greedy crop behavior: say how much original content to keep when no headcut is requested.
  - Impact:
    - Project progress: Medium - Greedy crop behavior controls balance between detail focus and preserving context.
    - Effect on other TODOs: Influences - It uses saliency, crop decision output, and fill policy.
  - Industry standard:
    Crop systems define minimum content retention and padding rules so outputs are consistent across image sets.
  - Recommended solution:
    Keep as much original content as possible while meeting target aspect ratio and configured margin, using fill only when needed.
  - Answer:

- [ ] Define detail crop fill policy: say how missing pixels are created when the crop extends beyond the original image.
  - Impact:
    - Project progress: Medium - Detail crop fill must align with global background fill quality rules.
    - Effect on other TODOs: Influences - It depends on background fill policy and KO rejection rules.
  - Industry standard:
    Missing pixels outside a crop are generated with the least destructive background-compatible method and recorded as a transform operation.
  - Recommended solution:
    Use the global fill policy in priority order and record fill method, confidence, and warnings in TransformResult.
  - Answer:

- [ ] Define center-and-stretch background extension: say how new background pixels are filled around centered objects.
  - Impact:
    - Project progress: Medium - Centering is a core packshot requirement and needs deterministic extension behavior.
    - Effect on other TODOs: Influences - It uses background identification, fill policy, cleanup, and fallback behavior.
  - Industry standard:
    Product image centering extends background from trusted edge or background samples and preserves object geometry.
  - Recommended solution:
    Center the object using crop/expand geometry and fill new pixels from validated background samples using the configured fill method.
  - Answer:

- [ ] Define center-and-stretch cleanup method: choose the cleanup technique used after stretching or filling background.
  - Impact:
    - Project progress: Medium - Cleanup improves visual quality but follows fill method selection.
    - Effect on other TODOs: Influences - It affects background extension diagnostics and KO policy.
  - Industry standard:
    Post-fill cleanup uses local smoothing, feathering, or inpainting only near seams while preserving subject pixels.
  - Recommended solution:
    Use feathering and local smoothing by default, with optional inpainting for artifacts above a configured threshold.
  - Answer:

- [ ] Define center-and-stretch fallback behavior: say what happens when object bounds or background fill fails.
  - Impact:
    - Project progress: Medium - Fallback behavior prevents transform exceptions from becoming full-batch failures.
    - Effect on other TODOs: Influences - It aligns transform failure cases, KO policy, and manifest warnings.
  - Industry standard:
    Pipelines use deterministic fallback paths for low-confidence geometry and escalate to per-item failure when output quality cannot be guaranteed.
  - Recommended solution:
    Fall back to conservative resize/crop when bounds are weak, export with warning for acceptable results, and KO when required transform constraints cannot be met.
  - Answer:
